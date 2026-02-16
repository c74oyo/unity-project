using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 统一角色卡 — 合并 UnitDefinition（战斗属性）和 WorkerCard（建筑加成）
/// 一个角色既能上战场，也能派去建筑工作
///
/// 在 Project 窗口右键 → Create → Game → Character Card 创建
/// </summary>
[CreateAssetMenu(menuName = "Game/Character Card", fileName = "Char_")]
public class CharacterCard : ScriptableObject
{
    // ============ 基本信息 ============

    [Header("基本信息")]
    [Tooltip("角色唯一ID（默认使用资产名）")]
    public string characterId;

    [Tooltip("显示名称")]
    public string displayName = "New Character";

    [Tooltip("角色描述")]
    [TextArea(2, 4)]
    public string description = "";

    [Tooltip("稀有度")]
    public CharacterRarity rarity = CharacterRarity.Common;

    // ============ 外观 ============

    [Header("外观")]
    [Tooltip("角色头像（UI用）")]
    public Sprite portrait;

    [Tooltip("自定义3D模型预制体（为空则使用默认 Capsule）")]
    public GameObject modelPrefab;

    [Tooltip("备用颜色（无自定义模型时 Capsule 的颜色）")]
    public Color fallbackColor = Color.blue;

    // ============ 战斗属性（对应原 UnitDefinition） ============

    [Header("战斗属性")]
    [Tooltip("最大生命值")]
    public int maxHP = 50;

    [Tooltip("攻击力")]
    public int attack = 10;

    [Tooltip("防御力")]
    public int defense = 5;

    [Tooltip("每回合移动格数")]
    public int moveRange = 10;

    [Tooltip("攻击范围（1=近战，2+=远程）")]
    public int attackRange = 1;

    // ============ 建筑加成（对应原 WorkerCard） ============

    [Header("建筑加成（派驻到 Worksite 时生效）")]
    [Tooltip("卡牌加成列表")]
    public List<CardModifier> modifiers = new();

    // ============ 经济 ============

    [Header("经济")]
    [Tooltip("商店售价（0=不可购买，仅掉落/初始拥有）")]
    public float shopPrice;

    // ============ Validation ============

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(characterId))
            characterId = name;
    }

    // ============ 兼容接口 ============

    /// <summary>
    /// 兼容原 UnitDefinition.unitName 的引用
    /// </summary>
    public string unitName => displayName;

    /// <summary>
    /// 兼容原 WorkerCard.modifiers 的查询
    /// 计算指定类型的 modifier 总和
    /// </summary>
    public float GetModifierSum(CardModifier.ModifierType type)
    {
        float sum = 0f;
        if (modifiers == null) return sum;
        foreach (var m in modifiers)
        {
            if (m != null && m.type == type)
                sum += m.value;
        }
        return sum;
    }
}

/// <summary>
/// 角色稀有度（统一 WorkerCard.CardRarity 和 ItemRarity 的概念）
/// </summary>
public enum CharacterRarity
{
    Common,     // 白
    Rare,       // 蓝
    Epic,       // 紫
    Legendary   // 橙
}
