using UnityEngine;

/// <summary>
/// NPC势力定义 - ScriptableObject
/// 定义一个NPC势力的基本属性
/// </summary>
[CreateAssetMenu(fileName = "NewNPCFaction", menuName = "Game/World Map/NPC Faction")]
public class NPCFaction : ScriptableObject
{
    [Header("基本信息")]
    [Tooltip("势力唯一ID")]
    public string factionId;

    [Tooltip("势力名称")]
    public string displayName;

    [Tooltip("势力描述")]
    [TextArea(3, 5)]
    public string description;

    [Tooltip("势力图标")]
    public Sprite icon;

    [Tooltip("势力代表颜色")]
    public Color factionColor = Color.blue;

    [Header("势力类型")]
    [Tooltip("势力类型")]
    public FactionType factionType = FactionType.Neutral;

    public enum FactionType
    {
        Friendly,   // 友好 - 默认好感度高，容易交易
        Neutral,    // 中立 - 默认好感度中等
        Hostile,    // 敌对 - 默认好感度低，需要努力改善关系
        Merchant    // 商人 - 专注于贸易，提供特殊商品
    }

    [Header("初始关系")]
    [Tooltip("初始好感度 (-100 到 100)")]
    [Range(-100, 100)]
    public int initialReputation = 0;

    [Tooltip("每回合自然衰减/恢复的好感度（向0靠拢）")]
    [Range(0, 5)]
    public float reputationDecayRate = 0.5f;

    [Header("贸易设置")]
    [Tooltip("基础税率（0-1，影响交易价格）")]
    [Range(0f, 0.5f)]
    public float baseTaxRate = 0.1f;

    [Tooltip("是否允许贸易")]
    public bool allowTrade = true;

    [Tooltip("贸易所需最低好感度")]
    [Range(-100, 100)]
    public int minReputationForTrade = -50;

    [Header("势力范围")]
    [Tooltip("默认领土半径（菱形）")]
    [Range(1, 10)]
    public int defaultTerritoryRadius = 3;

    [Tooltip("在领土内建造的成本倍率")]
    [Range(1f, 3f)]
    public float territoryBuildCostMultiplier = 1.5f;

    [Header("特殊商品")]
    [Tooltip("该势力专门出售的资源")]
    public ResourceDefinition[] specialSellResources;

    [Tooltip("该势力高价收购的资源")]
    public ResourceDefinition[] specialBuyResources;

    [Tooltip("特殊商品价格倍率")]
    [Range(0.5f, 2f)]
    public float specialPriceMultiplier = 1.2f;

    [Header("声望市场配置")]
    [Tooltip("该势力的声望-市场分层配置")]
    public ReputationMarketConfig marketConfig;

    [Header("任务设置")]
    [Tooltip("同时可发布的最大任务数")]
    [Range(1, 10)]
    public int maxActiveQuests = 3;

    [Tooltip("任务刷新间隔（秒）")]
    public float questRefreshInterval = 300f;

    // ============ 好感度相关计算 ============

    /// <summary>
    /// 根据好感度计算实际税率
    /// </summary>
    public float GetEffectiveTaxRate(int currentReputation)
    {
        // 好感度越高，税率越低
        // reputation 100 -> 税率减半
        // reputation -100 -> 税率翻倍
        float reputationFactor = 1f - (currentReputation / 200f); // 0.5 到 1.5
        return Mathf.Clamp(baseTaxRate * reputationFactor, 0f, 0.8f);
    }

    /// <summary>
    /// 检查是否可以与该势力交易
    /// </summary>
    public bool CanTradeWith(int currentReputation)
    {
        return allowTrade && currentReputation >= minReputationForTrade;
    }

    /// <summary>
    /// 获取好感度等级描述
    /// </summary>
    public static string GetReputationLevel(int reputation)
    {
        if (reputation >= 80) return "Exalted";
        if (reputation >= 50) return "Friendly";
        if (reputation >= 20) return "Favorable";
        if (reputation >= -20) return "Neutral";
        if (reputation >= -50) return "Unfriendly";
        if (reputation >= -80) return "Hostile";
        return "Hated";
    }

    /// <summary>
    /// 获取好感度对应的颜色
    /// </summary>
    public static Color GetReputationColor(int reputation)
    {
        if (reputation >= 50) return Color.green;
        if (reputation >= 0) return Color.yellow;
        if (reputation >= -50) return new Color(1f, 0.5f, 0f); // 橙色
        return Color.red;
    }
}
