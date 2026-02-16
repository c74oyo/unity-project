using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 声望市场配置 - 定义某个势力在不同声望层级下的商品开放规则
/// </summary>
[CreateAssetMenu(menuName = "Game/World Map/Reputation Market Config", fileName = "MarketConfig_")]
public class ReputationMarketConfig : ScriptableObject
{
    [Tooltip("配置ID")]
    public string configId;

    [Tooltip("声望层级列表（按 minReputation 从低到高排列）")]
    public List<ReputationTier> tiers = new();

    /// <summary>
    /// 根据当前声望获取对应的层级
    /// </summary>
    public ReputationTier GetTierForReputation(int reputation)
    {
        ReputationTier best = null;
        foreach (var tier in tiers)
        {
            if (reputation >= tier.minReputation)
                best = tier;
        }
        return best;
    }

    /// <summary>
    /// 获取层级索引
    /// </summary>
    public int GetTierIndex(int reputation)
    {
        int index = -1;
        for (int i = 0; i < tiers.Count; i++)
        {
            if (reputation >= tiers[i].minReputation)
                index = i;
        }
        return index;
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(configId))
            configId = name;
    }
}

/// <summary>
/// 声望层级 - 定义某个声望阈值下可交易的商品和限制
/// </summary>
[Serializable]
public class ReputationTier
{
    [Tooltip("该层级的最低声望要求")]
    public int minReputation;

    [Tooltip("层级名称（如：冷淡贸易、中立贸易）")]
    public string tierName;

    [Tooltip("该层级允许交易的资源ID列表")]
    public string[] availableResourceIds;

    [Tooltip("每种资源的最大交易量")]
    public int maxQuantityPerResource = 100;

    [Tooltip("价格倍率（1.0=正常）")]
    [Range(0.5f, 3f)]
    public float priceMultiplier = 1f;

    [Tooltip("是否解锁势力专属特殊商品")]
    public bool unlockSpecialGoods;

    [Tooltip("是否允许交易")]
    public bool canTrade = true;
}
