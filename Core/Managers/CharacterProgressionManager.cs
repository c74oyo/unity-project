using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 角色养成管理器 — 管理角色等级、经验、装备绑定
/// 全局持久化（跨场景）
/// </summary>
public class CharacterProgressionManager : MonoBehaviour
{
    public static CharacterProgressionManager Instance { get; private set; }

    // ============ Config ============

    [Header("角色花名册（拖入所有 CharacterCard 资源）")]
    public List<CharacterCard> allUnitDefs = new();

    [Header("等级配置")]
    [Tooltip("升级经验表：索引=当前等级，值=升到下一级所需经验")]
    public int[] expTable = { 0, 100, 300, 600, 1000, 1500, 2200, 3000, 4000, 5500 };

    [Tooltip("最大等级")]
    public int maxLevel = 10;

    [Header("每级属性成长")]
    public int hpPerLevel = 10;
    public int attackPerLevel = 2;
    public int defensePerLevel = 1;

    // ============ Runtime ============

    private HashSet<string> _ownedUnits = new();
    private Dictionary<string, CharacterLevelData> _levels = new();
    private Dictionary<string, CharacterCard> _unitDefCache = new();

    // ============ Events ============

    public event Action<string> OnCharacterLevelUp;      // unitId
    public event Action<string> OnCharacterAdded;         // unitId
    public event Action<string, string> OnEquipmentEquipped; // unitId, equipInstanceId

    // ============ Lifecycle ============

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (allUnitDefs.Count > 0) Instance.allUnitDefs = allUnitDefs;
            Instance.CacheUnitDefs();
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        CacheUnitDefs();
    }

    private void CacheUnitDefs()
    {
        _unitDefCache.Clear();
        foreach (var def in allUnitDefs)
            if (def != null) _unitDefCache[def.name] = def;
    }

    // ============ 花名册 ============

    /// <summary>
    /// 添加角色到花名册（初始等级1）
    /// </summary>
    public void AddCharacter(string unitId)
    {
        if (_ownedUnits.Contains(unitId)) return;
        _ownedUnits.Add(unitId);
        _levels[unitId] = new CharacterLevelData(unitId, 1, 0);
        OnCharacterAdded?.Invoke(unitId);
        Debug.Log($"[Progression] Character added: {unitId}");
    }

    /// <summary>
    /// 是否拥有该角色
    /// </summary>
    public bool OwnsCharacter(string unitId) => _ownedUnits.Contains(unitId);

    /// <summary>
    /// 获取所有拥有的角色ID列表
    /// </summary>
    public List<string> GetOwnedUnits() => new List<string>(_ownedUnits);

    // ============ 等级系统 ============

    /// <summary>
    /// 获取角色当前等级
    /// </summary>
    public int GetLevel(string unitId)
    {
        return _levels.TryGetValue(unitId, out var d) ? d.level : 1;
    }

    /// <summary>
    /// 获取角色累计经验
    /// </summary>
    public int GetTotalExp(string unitId)
    {
        return _levels.TryGetValue(unitId, out var d) ? d.totalExp : 0;
    }

    /// <summary>
    /// 获取升到下一级所需的经验值
    /// 已满级返回 0
    /// </summary>
    public int GetExpForNextLevel(string unitId)
    {
        int level = GetLevel(unitId);
        if (level >= maxLevel) return 0;

        // 经验表内直接查
        if (level < expTable.Length)
            return expTable[level];

        // 超出表范围：线性外推
        return expTable[expTable.Length - 1] + (level - expTable.Length + 1) * 1000;
    }

    /// <summary>
    /// 使用经验道具给角色升级
    /// 返回是否成功消耗道具
    /// </summary>
    public bool UseExpItem(string unitId, string itemId)
    {
        if (!_ownedUnits.Contains(unitId)) return false;
        if (GetLevel(unitId) >= maxLevel) return false;

        var inv = PlayerInventoryManager.Instance;
        if (inv == null) return false;

        var itemDef = inv.GetItemDef(itemId);
        if (itemDef == null || itemDef.itemType != ItemType.ExpBook) return false;
        if (!inv.TryConsumeItem(itemId, 1)) return false;

        var data = _levels[unitId];
        data.totalExp += itemDef.effectValue;

        // 自动升级循环
        bool leveledUp = false;
        while (data.level < maxLevel)
        {
            int needed = GetExpForNextLevel(unitId);
            if (needed <= 0 || data.totalExp < needed) break;

            data.totalExp -= needed;
            data.level++;
            leveledUp = true;
            Debug.Log($"[Progression] {unitId} leveled up to Lv.{data.level}!");
        }

        if (leveledUp)
            OnCharacterLevelUp?.Invoke(unitId);

        return true;
    }

    // ============ 装备绑定 ============

    /// <summary>
    /// 给角色装备一件装备
    /// 同槽位自动替换，从原持有者自动卸下
    /// </summary>
    public bool EquipItem(string unitId, string equipInstanceId)
    {
        if (!_ownedUnits.Contains(unitId)) return false;

        var inv = PlayerInventoryManager.Instance;
        if (inv == null) return false;

        var equip = inv.GetEquipment(equipInstanceId);
        if (equip == null) return false;

        var equipDef = inv.GetEquipDef(equip.equipDefId);
        if (equipDef == null) return false;

        // 卸下该角色同槽位的已有装备
        var currentEquipped = inv.GetEquippedBy(unitId);
        foreach (var e in currentEquipped)
        {
            var eDef = inv.GetEquipDef(e.equipDefId);
            if (eDef != null && eDef.slot == equipDef.slot)
                e.equippedToUnitId = "";
        }

        // 从原持有者卸下
        if (!string.IsNullOrEmpty(equip.equippedToUnitId))
            equip.equippedToUnitId = "";

        // 装备
        equip.equippedToUnitId = unitId;
        OnEquipmentEquipped?.Invoke(unitId, equipInstanceId);
        Debug.Log($"[Progression] {unitId} equipped {equip.equipDefId} (Lv.{equip.level})");
        return true;
    }

    /// <summary>
    /// 卸下装备
    /// </summary>
    public bool UnequipItem(string equipInstanceId)
    {
        var inv = PlayerInventoryManager.Instance;
        if (inv == null) return false;

        var equip = inv.GetEquipment(equipInstanceId);
        if (equip == null) return false;

        equip.equippedToUnitId = "";
        return true;
    }

    // ============ ★ 战斗属性计算 ============

    /// <summary>
    /// 获取角色的总属性加成 = 等级成长 + 装备加成
    /// 在 TacticalUnit.SpawnUnit 中调用，叠加到 CharacterCard 基础属性上
    /// </summary>
    public CombatStatBonus GetTotalBonus(string unitId)
    {
        // 等级加成
        int level = GetLevel(unitId);
        CombatStatBonus levelBonus = new CombatStatBonus
        {
            hp = hpPerLevel * (level - 1),
            attack = attackPerLevel * (level - 1),
            defense = defensePerLevel * (level - 1),
            moveRange = 0,
            attackRange = 0
        };

        // 装备加成
        CombatStatBonus equipBonus = CombatStatBonus.Zero;
        var inv = PlayerInventoryManager.Instance;
        if (inv != null)
        {
            var equipped = inv.GetEquippedBy(unitId);
            foreach (var e in equipped)
            {
                var def = inv.GetEquipDef(e.equipDefId);
                if (def != null)
                    equipBonus = equipBonus + def.GetBonusAtLevel(e.level);
            }
        }

        return levelBonus + equipBonus;
    }

    // ============ 存档 ============

    /// <summary>
    /// 导出存档数据
    /// </summary>
    public CharacterProgressionSaveData GetSaveData()
    {
        var data = new CharacterProgressionSaveData();
        data.ownedUnitIds = new List<string>(_ownedUnits);
        foreach (var kvp in _levels)
            data.characterLevels.Add(kvp.Value);
        return data;
    }

    /// <summary>
    /// 从存档数据恢复
    /// </summary>
    public void LoadFromSaveData(CharacterProgressionSaveData data)
    {
        _ownedUnits.Clear();
        _levels.Clear();
        if (data == null) return;

        foreach (var id in data.ownedUnitIds)
            _ownedUnits.Add(id);
        foreach (var ld in data.characterLevels)
            _levels[ld.unitId] = ld;

        Debug.Log($"[Progression] Loaded {_ownedUnits.Count} characters, " +
                  $"{_levels.Count} level records");
    }

    // ============ 查询 ============

    /// <summary>
    /// 根据 unitId 查找 CharacterCard
    /// </summary>
    public CharacterCard GetUnitDef(string unitId)
    {
        return _unitDefCache.TryGetValue(unitId, out var d) ? d : null;
    }
}
