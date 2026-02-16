using UnityEngine;

/// <summary>
/// 道具类型
/// </summary>
public enum ItemType
{
    ExpBook,          // 经验书
    StrengthenStone,  // 强化石
    Consumable        // 消耗品（预留）
}

/// <summary>
/// 道具稀有度
/// </summary>
public enum ItemRarity
{
    Common,     // 白
    Uncommon,   // 绿
    Rare,       // 蓝
    Epic,       // 紫
    Legendary   // 橙
}

/// <summary>
/// 养成道具定义（ScriptableObject）
/// 在 Project 窗口右键 → Create → Game → Progression → Item Definition 创建
/// 与 ResourceDefinition 分离，养成材料不参与基地经济循环
/// </summary>
[CreateAssetMenu(menuName = "Game/Progression/Item Definition", fileName = "Item_")]
public class ItemDefinition : ScriptableObject
{
    [Header("基本信息")]
    [Tooltip("道具唯一ID（默认使用资产名）")]
    public string itemId;

    [Tooltip("显示名称")]
    public string displayName = "New Item";

    [Tooltip("道具描述")]
    [TextArea(1, 3)]
    public string description;

    [Tooltip("道具图标")]
    public Sprite icon;

    [Header("分类")]
    [Tooltip("道具类型")]
    public ItemType itemType = ItemType.ExpBook;

    [Tooltip("稀有度")]
    public ItemRarity rarity = ItemRarity.Common;

    [Header("效果")]
    [Tooltip("效果值：ExpBook=提供的EXP量, StrengthenStone=提供的强化经验")]
    public int effectValue = 100;

    [Header("经济")]
    [Tooltip("NPC商店售价（0=不可购买，仅掉落获取）")]
    public float shopPrice;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(itemId))
            itemId = name;
    }

    /// <summary>
    /// 获取稀有度对应的颜色
    /// </summary>
    public Color GetRarityColor()
    {
        return rarity switch
        {
            ItemRarity.Common   => Color.white,
            ItemRarity.Uncommon => new Color(0.3f, 0.9f, 0.3f),  // 绿
            ItemRarity.Rare     => new Color(0.3f, 0.5f, 1f),    // 蓝
            ItemRarity.Epic     => new Color(0.7f, 0.3f, 0.9f),  // 紫
            ItemRarity.Legendary => new Color(1f, 0.6f, 0.1f),   // 橙
            _ => Color.white
        };
    }
}
