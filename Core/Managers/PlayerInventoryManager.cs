using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家背包管理器 — 管理养成道具和装备
/// 全局持久化（跨场景），不与具体基地绑定
/// </summary>
public class PlayerInventoryManager : MonoBehaviour
{
    public static PlayerInventoryManager Instance { get; private set; }

    // ============ Definitions ============

    [Header("道具定义（拖入所有 ItemDefinition 资源）")]
    public List<ItemDefinition> allItemDefs = new();

    [Header("装备定义（拖入所有 EquipmentDefinition 资源）")]
    public List<EquipmentDefinition> allEquipDefs = new();

    // ============ Runtime Data ============

    // 道具：itemId → 数量
    private Dictionary<string, int> _items = new();

    // 装备：instanceId → 装备实例数据
    private Dictionary<string, OwnedEquipmentSaveData> _equipment = new();

    // SO 查询缓存
    private Dictionary<string, ItemDefinition> _itemDefCache = new();
    private Dictionary<string, EquipmentDefinition> _equipDefCache = new();

    // ============ Events ============

    public event Action OnInventoryChanged;
    public event Action<string> OnEquipmentChanged; // instanceId

    // ============ Lifecycle ============

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // 场景重载时把新的 SO 引用传给已存在的实例
            if (allItemDefs.Count > 0) Instance.allItemDefs = allItemDefs;
            if (allEquipDefs.Count > 0) Instance.allEquipDefs = allEquipDefs;
            Instance.CacheDefinitions();
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        CacheDefinitions();
    }

    private void CacheDefinitions()
    {
        _itemDefCache.Clear();
        foreach (var def in allItemDefs)
            if (def != null) _itemDefCache[def.itemId] = def;

        _equipDefCache.Clear();
        foreach (var def in allEquipDefs)
            if (def != null) _equipDefCache[def.equipId] = def;
    }

    // ============ 道具操作 ============

    /// <summary>
    /// 获取道具持有数量
    /// </summary>
    public int GetItemCount(string itemId)
    {
        return _items.TryGetValue(itemId, out var c) ? c : 0;
    }

    /// <summary>
    /// 是否持有足够数量的道具
    /// </summary>
    public bool HasItem(string itemId, int amount)
    {
        return GetItemCount(itemId) >= amount;
    }

    /// <summary>
    /// 添加道具
    /// </summary>
    public void AddItem(string itemId, int amount)
    {
        if (amount <= 0) return;
        _items[itemId] = GetItemCount(itemId) + amount;
        OnInventoryChanged?.Invoke();
        Debug.Log($"[Inventory] Added {amount}x {itemId} (total: {_items[itemId]})");
    }

    /// <summary>
    /// 尝试消耗道具
    /// </summary>
    public bool TryConsumeItem(string itemId, int amount)
    {
        if (amount <= 0) return true;
        int current = GetItemCount(itemId);
        if (current < amount) return false;

        _items[itemId] = current - amount;
        if (_items[itemId] <= 0) _items.Remove(itemId);
        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 获取所有持有道具
    /// </summary>
    public Dictionary<string, int> GetAllItems()
    {
        return new Dictionary<string, int>(_items);
    }

    // ============ 装备操作 ============

    /// <summary>
    /// 获得一件新装备（返回实例ID）
    /// </summary>
    public string AddEquipment(string equipDefId)
    {
        string instanceId = Guid.NewGuid().ToString();
        _equipment[instanceId] = new OwnedEquipmentSaveData
        {
            instanceId = instanceId,
            equipDefId = equipDefId,
            level = 0,
            equippedToUnitId = ""
        };
        OnInventoryChanged?.Invoke();
        Debug.Log($"[Inventory] Obtained equipment: {equipDefId} (instance: {instanceId})");
        return instanceId;
    }

    /// <summary>
    /// 获取装备实例数据
    /// </summary>
    public OwnedEquipmentSaveData GetEquipment(string instanceId)
    {
        return _equipment.TryGetValue(instanceId, out var e) ? e : null;
    }

    /// <summary>
    /// 获取所有拥有的装备
    /// </summary>
    public List<OwnedEquipmentSaveData> GetAllEquipment()
    {
        return new List<OwnedEquipmentSaveData>(_equipment.Values);
    }

    /// <summary>
    /// 获取某角色已装备的所有装备
    /// </summary>
    public List<OwnedEquipmentSaveData> GetEquippedBy(string unitId)
    {
        var result = new List<OwnedEquipmentSaveData>();
        foreach (var e in _equipment.Values)
        {
            if (e.equippedToUnitId == unitId)
                result.Add(e);
        }
        return result;
    }

    /// <summary>
    /// 获取未装备的装备列表
    /// </summary>
    public List<OwnedEquipmentSaveData> GetUnequippedEquipment()
    {
        var result = new List<OwnedEquipmentSaveData>();
        foreach (var e in _equipment.Values)
        {
            if (string.IsNullOrEmpty(e.equippedToUnitId))
                result.Add(e);
        }
        return result;
    }

    /// <summary>
    /// 尝试强化装备（消耗强化石）
    /// </summary>
    public bool TryUpgradeEquipment(string instanceId, string stoneItemId)
    {
        if (!_equipment.TryGetValue(instanceId, out var equip)) return false;

        var def = GetEquipDef(equip.equipDefId);
        if (def == null || equip.level >= def.maxLevel) return false;

        var stoneDef = GetItemDef(stoneItemId);
        if (stoneDef == null || stoneDef.itemType != ItemType.StrengthenStone) return false;

        // 计算需要消耗几个强化石
        int needed = Mathf.CeilToInt((float)def.strengthenCostPerLevel / stoneDef.effectValue);
        if (needed <= 0) needed = 1;

        if (!TryConsumeItem(stoneItemId, needed)) return false;

        equip.level++;
        OnEquipmentChanged?.Invoke(instanceId);
        Debug.Log($"[Inventory] Equipment {equip.equipDefId} upgraded to Lv.{equip.level}");
        return true;
    }

    // ============ 定义查询 ============

    /// <summary>
    /// 根据 itemId 查找 ItemDefinition
    /// </summary>
    public ItemDefinition GetItemDef(string itemId)
    {
        return _itemDefCache.TryGetValue(itemId, out var d) ? d : null;
    }

    /// <summary>
    /// 根据 equipId 查找 EquipmentDefinition
    /// </summary>
    public EquipmentDefinition GetEquipDef(string equipId)
    {
        return _equipDefCache.TryGetValue(equipId, out var d) ? d : null;
    }

    // ============ 存档 ============

    /// <summary>
    /// 导出存档数据
    /// </summary>
    public PlayerInventorySaveData GetSaveData()
    {
        var data = new PlayerInventorySaveData();
        foreach (var kvp in _items)
            data.items.Add(new ItemStackSaveData(kvp.Key, kvp.Value));
        foreach (var e in _equipment.Values)
            data.equipment.Add(e);
        return data;
    }

    /// <summary>
    /// 从存档数据恢复
    /// </summary>
    public void LoadFromSaveData(PlayerInventorySaveData data)
    {
        _items.Clear();
        _equipment.Clear();
        if (data == null) return;

        foreach (var stack in data.items)
            if (!string.IsNullOrEmpty(stack.itemId))
                _items[stack.itemId] = stack.count;

        foreach (var e in data.equipment)
            if (!string.IsNullOrEmpty(e.instanceId))
                _equipment[e.instanceId] = e;

        Debug.Log($"[Inventory] Loaded {_items.Count} item types, {_equipment.Count} equipment pieces");
    }
}
