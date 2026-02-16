using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局库存：包装 Inventory，提供批量加/扣/检查，并发出 OnChanged 事件。
/// 增强：可选容量系统 + 仓库容量加成注册。
/// </summary>
public class GlobalInventory : MonoBehaviour, IInventorySystem
{
    public static GlobalInventory Instance { get; private set; }

    [Header("Init (Edit-time)")]
    public List<ResourceAmount> initialContents = new();

    [Header("Currency")]
    [SerializeField] private float money = 1000f;

    [Header("Capacity (optional)")]
    [Tooltip("开启后：Add/AddBatch 会受容量限制；超过容量的部分会被丢弃（并可选择打印警告）。")]
    public bool useCapacity = true;

    [Tooltip("每种资源的基础容量（不含仓库加成）。")]
    public int baseCapacityPerResource = 100;

    [Serializable]
    public struct ResourceCapacityOverride
    {
        public ResourceDefinition res;
        public int capacity;
    }

    [Tooltip("对特定资源设置不同的基础容量（覆盖 baseCapacityPerResource）。")]
    public List<ResourceCapacityOverride> capacityOverrides = new();

    [Tooltip("超过容量时是否打印警告日志。")]
    public bool warnOnOverflow = false;

    [Header("Debug (optional)")]
    public bool debugLifecycle = false;

    private readonly Inventory _inv = new();

    // 资源变化事件：res, oldValue, newValue
    public event Action<ResourceDefinition, int, int> OnChanged;

    private bool _initialized;

    // ===== 仓库/容量提供者 =====
    public interface IStorageProvider
    {
        int GetExtraCapacity(ResourceDefinition res);
    }

    private readonly List<IStorageProvider> _storageProviders = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (debugLifecycle)
                Debug.LogWarning($"[GlobalInventory] Duplicate detected -> destroy self. self={name} id={GetInstanceID()} scene={gameObject.scene.name}");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (debugLifecycle)
            Debug.Log($"[GlobalInventory] Awake. self={name} id={GetInstanceID()} scene={gameObject.scene.name}");

        // 你如果希望跨场景保留库存就打开这行
        // DontDestroyOnLoad(gameObject);

        InitIfNeeded();
    }

    private void OnDestroy()
    {
        if (debugLifecycle)
            Debug.Log($"[GlobalInventory] OnDestroy. self={name} id={GetInstanceID()} scene={gameObject.scene.name}");
    }

    private void InitIfNeeded()
    {
        if (_initialized) return;
        _initialized = true;

        if (initialContents == null) return;

        foreach (var ra in initialContents)
        {
            if (ra.res == null) continue;
            if (ra.amount <= 0) continue;
            Add(ra.res, ra.amount);
        }
    }

    // ===== IInventorySystem Implementation: Currency =====

    public float Money => money;

    public bool CanAfford(float cost)
    {
        return money >= cost;
    }

    public bool TrySpendMoney(float amount)
    {
        if (amount <= 0f) return false;
        if (money < amount) return false;

        money -= amount;
        return true;
    }

    public void AddMoney(float amount)
    {
        if (amount <= 0f) return;
        money += amount;
    }

    // ===== 仓库注册 =====

    public void RegisterStorage(IStorageProvider provider)
    {
        if (provider == null) return;
        if (_storageProviders.Contains(provider)) return;
        _storageProviders.Add(provider);
    }

    public void UnregisterStorage(IStorageProvider provider)
    {
        if (provider == null) return;
        _storageProviders.Remove(provider);
    }

    // ===== 容量查询 =====

    public int Get(ResourceDefinition res)
    {
        if (res == null) return 0;
        return _inv.Get(res);
    }

    public int GetCapacity(ResourceDefinition res)
    {
        if (res == null) return 0;
        if (!useCapacity) return int.MaxValue;

        int cap = Mathf.Max(0, baseCapacityPerResource);

        if (capacityOverrides != null)
        {
            for (int i = 0; i < capacityOverrides.Count; i++)
            {
                var o = capacityOverrides[i];
                if (o.res == res)
                {
                    cap = Mathf.Max(0, o.capacity);
                    break;
                }
            }
        }

        // 仓库加成
        for (int i = 0; i < _storageProviders.Count; i++)
        {
            var p = _storageProviders[i];
            if (p == null) continue;
            cap += Mathf.Max(0, p.GetExtraCapacity(res));
        }

        return Mathf.Max(0, cap);
    }

    public int GetFreeSpace(ResourceDefinition res)
    {
        if (res == null) return 0;
        if (!useCapacity) return int.MaxValue;

        int cap = GetCapacity(res);
        int cur = Get(res);
        return Mathf.Max(0, cap - cur);
    }

    public bool CanAdd(ResourceDefinition res, int amount)
    {
        if (res == null) return false;
        if (amount <= 0) return true;
        if (!useCapacity) return true;

        return GetFreeSpace(res) >= amount;
    }

    // ===== 基础操作（受容量影响的 Add） =====

    public void Add(ResourceDefinition res, int amount)
    {
        if (res == null) return;
        if (amount <= 0) return;

        if (useCapacity)
        {
            int free = GetFreeSpace(res);
            if (free <= 0)
            {
                if (warnOnOverflow)
                    Debug.LogWarning($"[GlobalInventory] Add blocked (full). res={res.displayName}, add={amount}, cur={Get(res)}, cap={GetCapacity(res)}");
                return;
            }

            if (amount > free)
            {
                if (warnOnOverflow)
                    Debug.LogWarning($"[GlobalInventory] Add clamped. res={res.displayName}, add={amount}->{free}, cur={Get(res)}, cap={GetCapacity(res)}");
                amount = free;
            }
        }

        int oldV = _inv.Get(res);
        _inv.Add(res, amount);
        int newV = _inv.Get(res);

        if (newV != oldV)
            OnChanged?.Invoke(res, oldV, newV);
    }

    public bool TryRemove(ResourceDefinition res, int amount)
    {
        if (res == null) return false;
        if (amount <= 0) return true;

        int oldV = _inv.Get(res);
        if (!_inv.TryRemove(res, amount)) return false;
        int newV = _inv.Get(res);

        if (newV != oldV)
            OnChanged?.Invoke(res, oldV, newV);

        return true;
    }

    public bool CanRemove(ResourceDefinition res, int amount)
    {
        if (res == null) return false;
        if (amount <= 0) return true;
        return _inv.CanRemove(res, amount);
    }

    /// <summary>
    /// 等效 Set：不用 Inventory.Set。通过 Add/TryRemove 把数量调整到 newValue。
    /// </summary>
    public bool SetCount(ResourceDefinition res, int newValue)
    {
        if (res == null) return false;
        newValue = Mathf.Max(0, newValue);

        if (useCapacity)
            newValue = Mathf.Min(newValue, GetCapacity(res));

        int oldV = _inv.Get(res);
        if (oldV == newValue) return true;

        int delta = newValue - oldV;

        if (delta > 0)
        {
            Add(res, delta);
        }
        else
        {
            if (!TryRemove(res, -delta))
                return false;
        }

        return _inv.Get(res) == newValue;
    }

    // ===== 批量 API（建造/贸易/生产会用） =====

    public bool CanAffordBatch(List<ResourceAmount> batch, int multiplier = 1)
    {
        if (batch == null) return true;
        multiplier = Mathf.Max(1, multiplier);

        foreach (var ra in batch)
        {
            if (ra.res == null) continue;
            int need = Mathf.Max(0, ra.amount) * multiplier;
            if (need <= 0) continue;

            if (!_inv.CanRemove(ra.res, need))
                return false;
        }
        return true;
    }

    public bool TryConsumeBatch(List<ResourceAmount> batch, int multiplier = 1)
    {
        if (batch == null) return true;
        multiplier = Mathf.Max(1, multiplier);

        if (!CanAffordBatch(batch, multiplier))
            return false;

        foreach (var ra in batch)
        {
            if (ra.res == null) continue;
            int need = Mathf.Max(0, ra.amount) * multiplier;
            if (need <= 0) continue;

            TryRemove(ra.res, need);
        }
        return true;
    }

    public bool CanAddBatch(List<ResourceAmount> batch, int multiplier = 1)
    {
        if (batch == null) return true;
        multiplier = Mathf.Max(1, multiplier);
        if (!useCapacity) return true;

        foreach (var ra in batch)
        {
            if (ra.res == null) continue;
            int add = Mathf.Max(0, ra.amount) * multiplier;
            if (add <= 0) continue;

            if (GetFreeSpace(ra.res) < add)
                return false;
        }
        return true;
    }

    /// <summary>
    /// 尝试批量添加：容量不足则返回 false，并且不添加。
    /// </summary>
    public bool TryAddBatch(List<ResourceAmount> batch, int multiplier = 1)
    {
        if (batch == null) return true;
        multiplier = Mathf.Max(1, multiplier);

        if (!CanAddBatch(batch, multiplier))
            return false;

        AddBatch(batch, multiplier);
        return true;
    }

    public void AddBatch(List<ResourceAmount> batch, int multiplier = 1)
    {
        if (batch == null) return;
        multiplier = Mathf.Max(1, multiplier);

        foreach (var ra in batch)
        {
            if (ra.res == null) continue;
            int add = Mathf.Max(0, ra.amount) * multiplier;
            if (add <= 0) continue;

            Add(ra.res, add);
        }
    }

    // ===== IInventorySystem Interface Adapters =====

    /// <summary>
    /// IInventorySystem: GetAmount with float return type
    /// </summary>
    public float GetAmount(ResourceDefinition res)
    {
        return Get(res);
    }

    /// <summary>
    /// IInventorySystem: Add with float amount parameter
    /// </summary>
    public void Add(ResourceDefinition res, float amount)
    {
        Add(res, Mathf.RoundToInt(amount));
    }

    /// <summary>
    /// IInventorySystem: TryConsume with float amount parameter
    /// </summary>
    public bool TryConsume(ResourceDefinition res, float amount)
    {
        return TryRemove(res, Mathf.RoundToInt(amount));
    }

    /// <summary>
    /// IInventorySystem: CanAdd with float amount parameter
    /// </summary>
    public bool CanAdd(ResourceDefinition res, float amount)
    {
        return CanAdd(res, Mathf.RoundToInt(amount));
    }

    /// <summary>
    /// IInventorySystem: AddBatch with float multiplier
    /// </summary>
    public void AddBatch(List<ResourceAmount> items, float multiplier = 1f)
    {
        AddBatch(items, Mathf.RoundToInt(multiplier));
    }

    /// <summary>
    /// IInventorySystem: TryConsumeBatch with float multiplier
    /// </summary>
    public bool TryConsumeBatch(List<ResourceAmount> items, float multiplier = 1f)
    {
        return TryConsumeBatch(items, Mathf.RoundToInt(multiplier));
    }

    /// <summary>
    /// IInventorySystem: TryAddBatch with float multiplier
    /// </summary>
    public bool TryAddBatch(List<ResourceAmount> items, float multiplier = 1f)
    {
        return TryAddBatch(items, Mathf.RoundToInt(multiplier));
    }

    /// <summary>
    /// IInventorySystem: HasResources - check if we have enough resources
    /// </summary>
    public bool HasResources(List<ResourceAmount> items, float multiplier = 1f)
    {
        return CanAffordBatch(items, Mathf.RoundToInt(multiplier));
    }

    /// <summary>
    /// IInventorySystem: CanAffordBatch with float multiplier
    /// </summary>
    public bool CanAffordBatch(List<ResourceAmount> items, float multiplier = 1f)
    {
        return CanAffordBatch(items, Mathf.RoundToInt(multiplier));
    }

    /// <summary>
    /// IInventorySystem: CanAddBatch with float multiplier
    /// </summary>
    public bool CanAddBatch(List<ResourceAmount> items, float multiplier = 1f)
    {
        return CanAddBatch(items, Mathf.RoundToInt(multiplier));
    }

    /// <summary>
    /// IInventorySystem: TotalCapacity property
    /// </summary>
    public int TotalCapacity
    {
        get
        {
            // Return sum of all resource capacities (simplified)
            // For a more accurate value, iterate all ResourceDefinitions
            return baseCapacityPerResource * 100; // Placeholder
        }
    }

    /// <summary>
    /// IInventorySystem: UsedCapacity property
    /// </summary>
    public int UsedCapacity
    {
        get
        {
            // Calculate total used capacity across all resources
            int total = 0;
            if (_inv != null)
            {
                var allRes = _inv.GetAll();
                foreach (var kvp in allRes)
                {
                    total += kvp.Value;
                }
            }
            return total;
        }
    }

    /// <summary>
    /// IInventorySystem: FreeCapacity property
    /// </summary>
    public int FreeCapacity
    {
        get
        {
            return TotalCapacity - UsedCapacity;
        }
    }

    /// <summary>
    /// IInventorySystem: UseCapacity property
    /// </summary>
    public bool UseCapacity => useCapacity;

    /// <summary>
    /// IInventorySystem: RegisterStorageProvider - for global IStorageProvider interface
    /// This is a separate implementation from the internal RegisterStorage method
    /// </summary>
    void IInventorySystem.RegisterStorageProvider(global::IStorageProvider provider)
    {
        // Global IStorageProvider has ProvidedCapacity property
        // We need to adapt it to work with GlobalInventory's system
        // For now, this is a no-op since GlobalInventory uses its own nested IStorageProvider
        // In a full implementation, you could create an adapter
    }

    /// <summary>
    /// IInventorySystem: UnregisterStorageProvider - for global IStorageProvider interface
    /// </summary>
    void IInventorySystem.UnregisterStorageProvider(global::IStorageProvider provider)
    {
        // Global IStorageProvider has ProvidedCapacity property
        // For now, this is a no-op since GlobalInventory uses its own nested IStorageProvider
    }

    /// <summary>
    /// IInventorySystem: MarkCapacityDirty - marks capacity as needing recalculation
    /// </summary>
    public void MarkCapacityDirty()
    {
        // GlobalInventory calculates capacity on-demand, so no caching to invalidate
        // This is a no-op for GlobalInventory
    }
}