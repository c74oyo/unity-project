using UnityEngine;

/// <summary>
/// 装备槽位
/// </summary>
public enum EquipSlot
{
    Weapon,  // 武器
    Armor    // 护甲
}

/// <summary>
/// 装备定义（ScriptableObject）
/// 在 Project 窗口右键 → Create → Game → Progression → Equipment Definition 创建
/// 每件装备可升级，升级后按百分比提升基础加成
/// </summary>
[CreateAssetMenu(menuName = "Game/Progression/Equipment Definition", fileName = "Equip_")]
public class EquipmentDefinition : ScriptableObject
{
    [Header("基本信息")]
    [Tooltip("装备唯一ID（默认使用资产名）")]
    public string equipId;

    [Tooltip("显示名称")]
    public string displayName = "New Equipment";

    [Tooltip("装备描述")]
    [TextArea(1, 3)]
    public string description;

    [Tooltip("装备图标")]
    public Sprite icon;

    [Header("槽位")]
    [Tooltip("装备槽位类型")]
    public EquipSlot slot = EquipSlot.Weapon;

    [Header("基础加成（等级0时的加成）")]
    public int bonusAttack;
    public int bonusDefense;
    public int bonusHP;
    public int bonusMoveRange;
    public int bonusAttackRange;

    [Header("升级参数")]
    [Tooltip("每级额外加成百分比（0.1=每级+10%基础加成）")]
    [Range(0.05f, 0.5f)]
    public float bonusPercentPerLevel = 0.1f;

    [Tooltip("最大强化等级")]
    public int maxLevel = 10;

    [Tooltip("每级消耗的强化石效果值总量")]
    public int strengthenCostPerLevel = 5;

    [Header("经济")]
    [Tooltip("商店售价（0=不可购买，仅掉落获取）")]
    public float shopPrice;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(equipId))
            equipId = name;
    }

    /// <summary>
    /// 计算指定强化等级下的实际属性加成
    /// 公式：基础值 × (1 + bonusPercentPerLevel × level)
    /// moveRange 和 attackRange 不随等级变化
    /// </summary>
    public CombatStatBonus GetBonusAtLevel(int level)
    {
        float mult = 1f + bonusPercentPerLevel * level;
        return new CombatStatBonus
        {
            attack = Mathf.RoundToInt(bonusAttack * mult),
            defense = Mathf.RoundToInt(bonusDefense * mult),
            hp = Mathf.RoundToInt(bonusHP * mult),
            moveRange = bonusMoveRange,
            attackRange = bonusAttackRange
        };
    }
}
