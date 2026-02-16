using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ProducerBuilding : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("可以是 GlobalInventory 或 BaseInventory")]
    public MonoBehaviour inventoryComponent;

    [Header("Input (per second) - optional")]
    public List<ResourceAmount> inputsPerSecond = new();

    [Header("Output (per second)")]
    public List<ResourceAmount> outputsPerSecond = new();

    [Header("Workers")]
    [Tooltip("当前分配的工人数（由 Worksite 通过 SetWorkersFromWorksite 写入）")]
    [Min(0)] public int assignedWorkers = 0;
    [Min(0)] public int maxWorkers = 5;

    [Range(0f, 1f)]
    public float efficiencyWithoutWorkers = 0.2f;

    [Range(0f, 5f)]
    public float efficiencyAtFullWorkers = 1.0f;

    [Header("Resource Zone")]
    [Tooltip("建筑定义，用于资源区兼容性检查（拖入 BuildableDefinition 资产）")]
    public BuildableDefinition buildingDefinition;

    [Header("Card Slots")]
    public bool useCardSlots = true;
    public Worksite worksite;

    [Header("Unified Modifiers (NEW)")]
    public BuildingModifiers modifiers;

    [Header("Capacity behavior")]
    public bool pauseWhenFull = true;
    public bool warnWhenBlocked = false;

#if UNITY_EDITOR
    [Header("Runtime Debug (Editor Only)")]
    [SerializeField] private float cardSpeedMulRuntime = 1f;
    [SerializeField] private float cardInputMulRuntime = 1f;
    [SerializeField] private bool hasResourceZoneBonus = false;
#endif

    // === HUD 需要的公开属性 ===
    public enum ProducerState { Running, Blocked, Idle }

    public ProducerState State { get; private set; } = ProducerState.Idle;
    public string BlockReason { get; private set; } = "";
    public float LastEfficiency { get; private set; } = 0f;

    /// <summary>是否在兼容的资源区内（HUD可读取显示加成信息）</summary>
    public bool HasResourceZoneBonus => _zoneType != null;
    /// <summary>当前资源区类型（可为null）</summary>
    public ResourceZoneType ActiveResourceZone => _zoneType;

    private readonly Dictionary<ResourceDefinition, float> _inCarry = new();
    private readonly Dictionary<ResourceDefinition, float> _outCarry = new();

    // 品质资源和副产品的carry系统（与 _outCarry 模式一致）
    private readonly Dictionary<ResourceDefinition, float> _qualityCarry = new();
    private readonly Dictionary<ResourceDefinition, float> _byproductCarry = new();

    // 资源区缓存
    private BaseInstance _ownerBase;
    private ResourceZoneType _zoneType;
    private bool _zoneChecked = false;

    // Runtime property to get inventory as interface
    private IInventorySystem _inventory;
    public IInventorySystem Inventory
    {
        get
        {
            if (_inventory == null && inventoryComponent != null)
                _inventory = inventoryComponent as IInventorySystem;
            return _inventory;
        }
    }

    /// <summary>
    /// 运行时更新库存引用（由 BaseSceneLoader 在加载基地时调用）
    /// </summary>
    public void SetInventory(MonoBehaviour newInventory)
    {
        inventoryComponent = newInventory;
        _inventory = newInventory as IInventorySystem;
    }

    private bool _inventoryWarned = false;

    private void Awake()
    {
        // Try to get inventory from component
        if (inventoryComponent != null)
        {
            _inventory = inventoryComponent as IInventorySystem;
        }

        // Fallback to GlobalInventory for backward compatibility
        if (_inventory == null)
        {
            var globalInv = GlobalInventory.Instance;
            if (globalInv != null)
            {
                inventoryComponent = globalInv;
                _inventory = globalInv as IInventorySystem;
            }
        }

        // 不在 Awake 中报错 — 库存可能稍后由 BaseSceneLoader.RebindAllBuildingInventories() 绑定

        if (worksite == null) worksite = GetComponentInChildren<Worksite>(true);

        // 统一修正器：优先找现成的，没有就自动加一个
        if (modifiers == null)
            modifiers = GetComponentInParent<BuildingModifiers>() ?? GetComponentInChildren<BuildingModifiers>(true);

        if (modifiers == null)
        {
            modifiers = gameObject.AddComponent<BuildingModifiers>();
            modifiers.worksite = worksite;
        }

        // 获取所属基地引用（用于资源区检查）
        _ownerBase = GetComponentInParent<BaseInstance>();

        ClampWorkers();
    }

    private void OnValidate()
    {
        ClampWorkers();
    }

    private void Update()
    {
        if (Inventory == null)
        {
            // 延迟尝试绑定库存（BaseSceneLoader 可能在 Awake 之后才调用 RebindAllBuildingInventories）
            TryLateBindInventory();
            if (Inventory == null) return;
        }

        // 延迟检查资源区（确保所有系统初始化完成）
        if (!_zoneChecked)
            CheckResourceZone();

        TickProduction(Time.deltaTime);
    }

    /// <summary>
    /// 延迟绑定库存：Awake 时找不到库存的情况下，在 Update 中再尝试一次
    /// </summary>
    private void TryLateBindInventory()
    {
        // 已经绑定了
        if (_inventory != null) return;

        // 尝试 GlobalInventory
        var globalInv = GlobalInventory.Instance;
        if (globalInv != null)
        {
            inventoryComponent = globalInv;
            _inventory = globalInv as IInventorySystem;
            return;
        }

        // 尝试场景中的 BaseInventory
        var baseInv = FindObjectOfType<BaseInventory>();
        if (baseInv != null)
        {
            inventoryComponent = baseInv;
            _inventory = baseInv as IInventorySystem;
            return;
        }

        // 仍然找不到，只警告一次
        if (!_inventoryWarned)
        {
            _inventoryWarned = true;
            Debug.LogWarning($"[ProducerBuilding] '{name}' 未找到库存系统，生产暂停。等待 RebindAllBuildingInventories 或手动绑定。", this);
        }
    }

    /// <summary>
    /// 检查该建筑是否在兼容的资源区内
    /// </summary>
    private void CheckResourceZone()
    {
        _zoneChecked = true;
        _zoneType = null;

        if (_ownerBase == null)
            _ownerBase = GetComponentInParent<BaseInstance>();

        if (_ownerBase == null || _ownerBase.ResourceZone == null)
            return;

        var zone = _ownerBase.ResourceZone;

        // 尝试自动获取 BuildableDefinition（如果没有手动拖入）
        if (buildingDefinition == null)
        {
            var buildableInst = GetComponentInParent<BuildableInstance>();
            if (buildableInst != null)
                buildingDefinition = buildableInst.def;
        }

        if (buildingDefinition != null && zone.IsBuildingCompatible(buildingDefinition))
        {
            _zoneType = zone;
            Debug.Log($"[ProducerBuilding] '{name}' 位于兼容资源区 '{zone.displayName}'，品质资源概率={zone.qualityResourceChance:P0}");
        }

#if UNITY_EDITOR
        hasResourceZoneBonus = _zoneType != null;
#endif
    }

    /// <summary>
    /// 强制重新检查资源区（基地移动或资源区变化时调用）
    /// </summary>
    public void RefreshResourceZone()
    {
        _zoneChecked = false;
    }

    public void NotifyCardSlotsChanged()
    {
        // 现在由 BuildingModifiers 监听 worksite.OnSlotsChanged 自动 dirty
    }

    public void SetWorkersFromWorksite(int presentWorkers)
    {
        assignedWorkers = presentWorkers;
        ClampWorkers();
    }

    private void ClampWorkers()
    {
        if (maxWorkers < 0) maxWorkers = 0;
        if (assignedWorkers < 0) assignedWorkers = 0;
        if (maxWorkers > 0 && assignedWorkers > maxWorkers) assignedWorkers = maxWorkers;
    }

    private float GetEfficiency01toN()
    {
        if (maxWorkers <= 0) return Mathf.Max(0f, efficiencyWithoutWorkers);

        float t = Mathf.Clamp01(assignedWorkers / (float)maxWorkers);
        float eff = Mathf.Lerp(efficiencyWithoutWorkers, efficiencyAtFullWorkers, t);
        return Mathf.Max(0f, eff);
    }

    private void ComputeCardMultipliers(out float speedMul, out float inputMul)
    {
        speedMul = 1f;
        inputMul = 1f;

        if (!useCardSlots) return;
        if (modifiers == null) return;

        speedMul = modifiers.GetMul(CardModifier.ModifierType.Producer_SpeedMul, 0.1f);
        inputMul = modifiers.GetMul(CardModifier.ModifierType.Producer_InputMul, 0.1f);
    }

    private void TickProduction(float dt)
    {
        if (dt <= 0f) return;

        if ((outputsPerSecond == null || outputsPerSecond.Count == 0) &&
            (inputsPerSecond == null || inputsPerSecond.Count == 0))
        {
            State = ProducerState.Idle;
            BlockReason = "No inputs/outputs configured";
            LastEfficiency = 0f;
            return;
        }

        ComputeCardMultipliers(out float speedMul, out float inputMul);

#if UNITY_EDITOR
        cardSpeedMulRuntime = speedMul;
        cardInputMulRuntime = inputMul;
#endif

        float eff = GetEfficiency01toN();
        LastEfficiency = eff;

        if (eff <= 0f)
        {
            State = ProducerState.Idle;
            BlockReason = "Efficiency is zero";
            return;
        }

        // 资源区效率加成（仅影响产出速度，不影响输入消耗）
        float zoneMul = _zoneType != null ? _zoneType.efficiencyBonus : 1f;

        float outEff = eff * speedMul * zoneMul;
        float inEff = eff * speedMul * inputMul;

        var inBatch = BuildBatch(inputsPerSecond, dt, inEff, _inCarry);
        var outBatch = BuildBatch(outputsPerSecond, dt, outEff, _outCarry);

        bool hasIn = inBatch != null && inBatch.Count > 0;
        bool hasOut = outBatch != null && outBatch.Count > 0;
        if (!hasIn && !hasOut)
        {
            State = ProducerState.Running;
            BlockReason = "";
            return;
        }

        if (hasIn && !Inventory.CanAffordBatch(inBatch, 1))
        {
            State = ProducerState.Blocked;
            BlockReason = "Not enough inputs";
            if (warnWhenBlocked) Debug.LogWarning($"[ProducerBuilding] Blocked: not enough inputs. ({name})");
            return;
        }

        if (pauseWhenFull && hasOut && Inventory.UseCapacity)
        {
            if (!Inventory.CanAddBatch(outBatch, 1))
            {
                State = ProducerState.Blocked;
                BlockReason = "Output storage full";
                if (warnWhenBlocked) Debug.LogWarning($"[ProducerBuilding] Blocked: output storage full. ({name})");
                return;
            }
        }

        if (hasIn)
        {
            if (!Inventory.TryConsumeBatch(inBatch, 1))
            {
                State = ProducerState.Blocked;
                BlockReason = "Consume failed unexpectedly";
                if (warnWhenBlocked) Debug.LogWarning($"[ProducerBuilding] Unexpected: consume failed after check. ({name})");
                return;
            }
        }

        if (hasOut)
        {
            if (pauseWhenFull && Inventory.UseCapacity)
            {
                if (!Inventory.TryAddBatch(outBatch, 1))
                {
                    if (warnWhenBlocked) Debug.LogWarning($"[ProducerBuilding] Unexpected: add failed after capacity check. ({name})");
                }
            }
            else
            {
                Inventory.AddBatch(outBatch, 1);
            }

            // 资源区品质资源产出
            if (_zoneType != null)
            {
                ProduceQualityResources(dt, outEff);
            }
        }

        State = ProducerState.Running;
        BlockReason = "";
    }

    /// <summary>
    /// 资源区品质资源产出逻辑
    /// 在兼容的资源区内，按 qualityResourceChance 概率额外产出优质资源变体
    /// 并按 byproductChance 概率产出副产品（稀有金属等）
    /// </summary>
    private void ProduceQualityResources(float dt, float outEff)
    {
        if (_zoneType == null || Inventory == null) return;

        float qualityChance = _zoneType.qualityResourceChance;
        float byproductChance = _zoneType.byproductChance;

        for (int i = 0; i < outputsPerSecond.Count; i++)
        {
            var ra = outputsPerSecond[i];
            if (ra.res == null || ra.amount <= 0) continue;

            // === 品质资源产出 ===
            // 如果该资源有优质变体，按概率产出优质版本
            if (ra.res.qualityVariant != null && qualityChance > 0f)
            {
                var qualityRes = ra.res.qualityVariant;
                float qualityRate = ra.amount * qualityChance; // 每秒品质产出量

                _qualityCarry.TryGetValue(qualityRes, out float qc);
                qc += qualityRate * dt * outEff;

                int qWhole = Mathf.FloorToInt(qc);
                qc -= qWhole;
                _qualityCarry[qualityRes] = qc;

                if (qWhole > 0)
                {
                    Inventory.AddBatch(new List<ResourceAmount>
                    {
                        new ResourceAmount { res = qualityRes, amount = qWhole }
                    }, 1);
                }
            }

            // === 副产品产出 ===
            // 使用优质资源加工时，有概率产出稀有副产品
            if (ra.res.HasByproducts && byproductChance > 0f)
            {
                foreach (var byproduct in ra.res.possibleByproducts)
                {
                    if (byproduct == null) continue;

                    // 副产品产出率 = 基础产出率 * 副产品概率 * 资源自身副产品基础概率
                    float bpRate = ra.amount * byproductChance * ra.res.byproductBaseChance;
                    if (bpRate <= 0f) continue;

                    _byproductCarry.TryGetValue(byproduct, out float bc);
                    bc += bpRate * dt * outEff;

                    int bpWhole = Mathf.FloorToInt(bc);
                    bc -= bpWhole;
                    _byproductCarry[byproduct] = bc;

                    if (bpWhole > 0)
                    {
                        Inventory.AddBatch(new List<ResourceAmount>
                        {
                            new ResourceAmount { res = byproduct, amount = bpWhole }
                        }, 1);
                    }
                }
            }
        }
    }

    private static List<ResourceAmount> BuildBatch(
        List<ResourceAmount> perSecond,
        float dt,
        float eff,
        Dictionary<ResourceDefinition, float> carry)
    {
        if (perSecond == null || perSecond.Count == 0) return null;

        List<ResourceAmount> batch = null;

        for (int i = 0; i < perSecond.Count; i++)
        {
            var ra = perSecond[i];
            if (ra.res == null) continue;

            int baseRate = Mathf.Max(0, ra.amount);
            if (baseRate <= 0) continue;

            float add = baseRate * dt * eff;

            carry.TryGetValue(ra.res, out float c);
            c += add;

            int whole = Mathf.FloorToInt(c);
            c -= whole;

            carry[ra.res] = c;

            if (whole <= 0) continue;

            batch ??= new List<ResourceAmount>(4);
            batch.Add(new ResourceAmount { res = ra.res, amount = whole });
        }

        return batch;
    }
}
