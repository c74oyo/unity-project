using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 养成系统初始化器 — 确保玩家开局拥有角色、商店有商品
/// 挂在 World Map 场景（与 WorldMapInitializer 同级）
///
/// 逻辑：
/// - 如果存档已有数据 → 不做任何事（由 BaseManager pending 链路恢复）
/// - 如果是新游戏（无存档 / 背包为空）→ 自动发放初始角色 + 初始道具
/// - 商店商品由 ItemShopManager 的 Inspector 列表配置，不需要代码初始化
/// </summary>
public class ProgressionInitializer : MonoBehaviour
{
    [Header("初始角色（新游戏自动加入花名册）")]
    [Tooltip("拖入新玩家默认拥有的 CharacterCard")]
    public List<CharacterCard> starterCharacters = new();

    [Header("初始道具（新游戏自动发放到背包）")]
    public List<StarterItem> starterItems = new();

    [Header("初始装备（新游戏自动发放到背包）")]
    public List<StarterEquip> starterEquipment = new();

    [Header("自动装备（发放后自动给第一个角色穿上）")]
    [Tooltip("开启后，初始装备的第一件武器和第一件护甲会自动穿给第一个初始角色")]
    public bool autoEquipFirst = true;

    [Header("初始金钱（新游戏给活跃基地加钱，0=不加。BaseManager 已有初始金钱设置则填0）")]
    public float starterMoney = 0f;

    [Header("Settings")]
    [Tooltip("延迟初始化（等待 Manager 就绪）")]
    public float initDelay = 0.2f;

    [Tooltip("是否在控制台输出初始化日志")]
    public bool verbose = true;

    // ============ Lifecycle ============

    private void Start()
    {
        Invoke(nameof(TryInitialize), initDelay);
    }

    private void TryInitialize()
    {
        // 检查必要的 Manager 是否已初始化
        if (CharacterProgressionManager.Instance == null)
        {
            Debug.LogError("[ProgressionInit] CharacterProgressionManager not found! " +
                "请在场景中创建 GameObject 并挂上 CharacterProgressionManager 组件。");
            return;
        }
        if (PlayerInventoryManager.Instance == null)
        {
            Debug.LogError("[ProgressionInit] PlayerInventoryManager not found! " +
                "请在场景中创建 GameObject 并挂上 PlayerInventoryManager 组件。");
            return;
        }

        // 检查是否已有存档数据（角色列表非空 = 非新游戏）
        var prog = CharacterProgressionManager.Instance;
        if (prog.GetOwnedUnits().Count > 0)
        {
            if (verbose)
                Debug.Log("[ProgressionInit] Save data detected, skipping starter grants.");
            return;
        }

        if (verbose)
            Debug.Log("[ProgressionInit] New game detected, granting starter content...");

        GrantStarterCharacters();
        GrantStarterItems();
        var grantedEquipInstanceIds = GrantStarterEquipment();
        GrantStarterMoney();

        // 自动给第一个角色穿上初始装备
        if (autoEquipFirst)
            AutoEquipFirstCharacter(grantedEquipInstanceIds);

        if (verbose)
            Debug.Log($"[ProgressionInit] Starter content granted! " +
                $"Characters: {starterCharacters.Count}, Items: {starterItems.Count}, Equipment: {starterEquipment.Count}");
    }

    // ============ 发放逻辑 ============

    private void GrantStarterCharacters()
    {
        var prog = CharacterProgressionManager.Instance;
        if (prog == null) return;

        foreach (var card in starterCharacters)
        {
            if (card == null) continue;
            prog.AddCharacter(card.name); // 用 SO asset name 作为 unitId
            if (verbose)
                Debug.Log($"[ProgressionInit] + Character: {card.displayName}");
        }
    }

    private void GrantStarterItems()
    {
        var inv = PlayerInventoryManager.Instance;
        if (inv == null) return;

        foreach (var entry in starterItems)
        {
            if (entry.itemDef == null || entry.count <= 0) continue;
            inv.AddItem(entry.itemDef.itemId, entry.count);
            if (verbose)
                Debug.Log($"[ProgressionInit] + Item: {entry.itemDef.displayName} x{entry.count}");
        }
    }

    /// <summary>
    /// 发放初始装备，返回所有新增装备的 instanceId 列表
    /// </summary>
    private List<string> GrantStarterEquipment()
    {
        var result = new List<string>();
        var inv = PlayerInventoryManager.Instance;
        if (inv == null) return result;

        foreach (var entry in starterEquipment)
        {
            if (entry.equipDef == null) continue;
            for (int i = 0; i < entry.count; i++)
            {
                string instanceId = inv.AddEquipment(entry.equipDef.equipId);
                result.Add(instanceId);
            }
            if (verbose)
                Debug.Log($"[ProgressionInit] + Equipment: {entry.equipDef.displayName} x{entry.count}");
        }
        return result;
    }

    /// <summary>
    /// 自动给第一个角色穿上初始装备（每个槽位只穿第一件）
    /// </summary>
    private void AutoEquipFirstCharacter(List<string> equipInstanceIds)
    {
        var prog = CharacterProgressionManager.Instance;
        var inv = PlayerInventoryManager.Instance;
        if (prog == null || inv == null) return;

        var owned = prog.GetOwnedUnits();
        if (owned.Count == 0) return;
        string firstUnitId = owned[0];

        bool weaponEquipped = false;
        bool armorEquipped = false;

        foreach (var instanceId in equipInstanceIds)
        {
            var equip = inv.GetEquipment(instanceId);
            if (equip == null) continue;

            var def = inv.GetEquipDef(equip.equipDefId);
            if (def == null) continue;

            // 每个槽位只自动穿一件
            if (def.slot == EquipSlot.Weapon && !weaponEquipped)
            {
                prog.EquipItem(firstUnitId, instanceId);
                weaponEquipped = true;
                if (verbose) Debug.Log($"[ProgressionInit] Auto-equipped weapon: {def.displayName} → {firstUnitId}");
            }
            else if (def.slot == EquipSlot.Armor && !armorEquipped)
            {
                prog.EquipItem(firstUnitId, instanceId);
                armorEquipped = true;
                if (verbose) Debug.Log($"[ProgressionInit] Auto-equipped armor: {def.displayName} → {firstUnitId}");
            }

            if (weaponEquipped && armorEquipped) break;
        }
    }

    private void GrantStarterMoney()
    {
        if (starterMoney <= 0f) return;
        if (BaseManager.Instance == null) return;

        var baseSave = BaseManager.Instance.GetActiveBaseSaveData();
        if (baseSave == null)
        {
            // 没有活跃基地，尝试给第一个基地
            var allBases = BaseManager.Instance.AllBaseSaveData;
            if (allBases.Count > 0)
                baseSave = allBases[0];
        }

        if (baseSave != null)
        {
            baseSave.money += starterMoney;
            BaseManager.Instance.UpdateBaseSaveData(baseSave);
            if (verbose)
                Debug.Log($"[ProgressionInit] + Money: ${starterMoney:N0} → base '{baseSave.baseId}'");
        }
    }
}

// ============ 配置类 ============

[System.Serializable]
public class StarterItem
{
    [Tooltip("道具定义")]
    public ItemDefinition itemDef;

    [Tooltip("发放数量")]
    [Min(1)] public int count = 1;
}

[System.Serializable]
public class StarterEquip
{
    [Tooltip("装备定义")]
    public EquipmentDefinition equipDef;

    [Tooltip("发放数量")]
    [Min(1)] public int count = 1;
}
