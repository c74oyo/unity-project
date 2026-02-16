using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 声望门控市场系统 - 根据玩家与各势力的声望等级，控制可交易商品种类和数量
/// </summary>
public class ReputationMarketSystem : MonoBehaviour
{
    public static ReputationMarketSystem Instance { get; private set; }

    [Header("References")]
    public NPCManager npcManager;

    [Header("Settings")]
    [Tooltip("库存刷新间隔（秒）")]
    public float stockRefreshInterval = 120f;

    [Tooltip("基础价格表（resourceId -> 基础价格）")]
    public List<ResourceBasePrice> basePrices = new();

    // 缓存
    private Dictionary<string, float> _basePriceCache = new();

    // ============ Lifecycle ============

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        CacheBasePrices();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        if (npcManager == null)
            npcManager = FindObjectOfType<NPCManager>();

        // 监听声望变化事件
        if (npcManager != null)
            npcManager.OnReputationChanged += OnReputationChanged;
    }

    private void CacheBasePrices()
    {
        _basePriceCache.Clear();
        foreach (var entry in basePrices)
        {
            if (!string.IsNullOrEmpty(entry.resourceId))
                _basePriceCache[entry.resourceId] = entry.basePrice;
        }
    }

    // ============ Core Queries ============

    /// <summary>
    /// 获取指定势力在当前声望下的可交易商品列表
    /// </summary>
    public List<MarketListing> GetAvailableGoods(string factionId, string outpostId)
    {
        var result = new List<MarketListing>();
        if (npcManager == null) return result;

        var faction = npcManager.GetFaction(factionId);
        if (faction == null || faction.marketConfig == null) return result;

        int reputation = npcManager.GetReputation(factionId);
        var tier = faction.marketConfig.GetTierForReputation(reputation);
        if (tier == null || !tier.canTrade) return result;

        // 常规资源
        if (tier.availableResourceIds != null)
        {
            foreach (var resId in tier.availableResourceIds)
            {
                float price = GetBasePrice(resId) * tier.priceMultiplier;
                result.Add(new MarketListing
                {
                    resourceId = resId,
                    availableAmount = tier.maxQuantityPerResource,
                    pricePerUnit = price,
                    isSpecialGood = false,
                    direction = TradeDirection.Export
                });
            }
        }

        // 特殊商品（高声望解锁）
        if (tier.unlockSpecialGoods && faction.specialSellResources != null)
        {
            foreach (var res in faction.specialSellResources)
            {
                if (res == null) continue;
                float price = GetBasePrice(res.id) * tier.priceMultiplier * faction.specialPriceMultiplier;
                result.Add(new MarketListing
                {
                    resourceId = res.id,
                    availableAmount = tier.maxQuantityPerResource / 4,
                    pricePerUnit = price,
                    isSpecialGood = true,
                    direction = TradeDirection.Import
                });
            }
        }

        return result;
    }

    /// <summary>
    /// 获取指定势力某资源的最大交易量
    /// </summary>
    public int GetMaxTradeQuantity(string factionId, string resourceId)
    {
        if (npcManager == null) return 0;

        var faction = npcManager.GetFaction(factionId);
        if (faction == null || faction.marketConfig == null) return 0;

        int reputation = npcManager.GetReputation(factionId);
        var tier = faction.marketConfig.GetTierForReputation(reputation);
        if (tier == null || !tier.canTrade) return 0;

        // 检查资源是否在可交易列表中
        if (tier.availableResourceIds != null)
        {
            foreach (var resId in tier.availableResourceIds)
            {
                if (resId == resourceId)
                    return tier.maxQuantityPerResource;
            }
        }

        // 检查特殊商品
        if (tier.unlockSpecialGoods && faction.specialSellResources != null)
        {
            foreach (var res in faction.specialSellResources)
            {
                if (res != null && res.id == resourceId)
                    return tier.maxQuantityPerResource / 4;
            }
        }

        return 0;
    }

    /// <summary>
    /// 检查是否可以交易指定资源
    /// </summary>
    public bool CanTradeResource(string factionId, string resourceId)
    {
        return GetMaxTradeQuantity(factionId, resourceId) > 0;
    }

    /// <summary>
    /// 获取当前声望层级的价格倍率
    /// </summary>
    public float GetPriceMultiplier(string factionId)
    {
        if (npcManager == null) return 1f;

        var faction = npcManager.GetFaction(factionId);
        if (faction == null || faction.marketConfig == null) return 1f;

        int reputation = npcManager.GetReputation(factionId);
        var tier = faction.marketConfig.GetTierForReputation(reputation);
        return tier != null ? tier.priceMultiplier : 1f;
    }

    /// <summary>
    /// 获取当前声望层级
    /// </summary>
    public ReputationTier GetCurrentTier(string factionId)
    {
        if (npcManager == null) return null;

        var faction = npcManager.GetFaction(factionId);
        if (faction == null || faction.marketConfig == null) return null;

        int reputation = npcManager.GetReputation(factionId);
        return faction.marketConfig.GetTierForReputation(reputation);
    }

    // ============ Stock Refresh ============

    /// <summary>
    /// 刷新据点库存（基于声望层级）
    /// </summary>
    public void RefreshOutpostStock(NPCOutpost outpost)
    {
        if (outpost == null || npcManager == null) return;

        var faction = npcManager.GetFaction(outpost.factionId);
        if (faction == null || faction.marketConfig == null) return;

        int reputation = npcManager.GetReputation(outpost.factionId);
        var tier = faction.marketConfig.GetTierForReputation(reputation);

        if (tier == null || !tier.canTrade)
        {
            outpost.sellStock.Clear();
            outpost.buyDemand.Clear();
            outpost.lastStockRefreshTime = Time.time;
            return;
        }

        outpost.sellStock.Clear();
        outpost.buyDemand.Clear();

        // 生成卖出库存（NPC卖给玩家）
        if (tier.availableResourceIds != null)
        {
            foreach (var resId in tier.availableResourceIds)
            {
                int qty = UnityEngine.Random.Range(
                    Mathf.Max(1, tier.maxQuantityPerResource / 2),
                    tier.maxQuantityPerResource + 1);
                float price = GetBasePrice(resId) * tier.priceMultiplier;
                outpost.AddSellStock(resId, qty, price);
            }
        }

        // 生成买入需求（NPC从玩家购买）
        if (tier.availableResourceIds != null)
        {
            foreach (var resId in tier.availableResourceIds)
            {
                int qty = UnityEngine.Random.Range(
                    Mathf.Max(1, tier.maxQuantityPerResource / 2),
                    tier.maxQuantityPerResource + 1);
                float price = GetBasePrice(resId) * tier.priceMultiplier * 0.8f; // 收购价略低于售价
                outpost.AddBuyDemand(resId, qty, price);
            }
        }

        // 特殊商品
        if (tier.unlockSpecialGoods && faction.specialSellResources != null)
        {
            foreach (var res in faction.specialSellResources)
            {
                if (res == null) continue;
                int qty = tier.maxQuantityPerResource / 4;
                float price = GetBasePrice(res.id) * tier.priceMultiplier * faction.specialPriceMultiplier;
                outpost.AddSellStock(res.id, qty, price);
            }
        }

        if (tier.unlockSpecialGoods && faction.specialBuyResources != null)
        {
            foreach (var res in faction.specialBuyResources)
            {
                if (res == null) continue;
                int qty = tier.maxQuantityPerResource / 4;
                float price = GetBasePrice(res.id) * tier.priceMultiplier * faction.specialPriceMultiplier * 0.8f;
                outpost.AddBuyDemand(res.id, qty, price);
            }
        }

        outpost.lastStockRefreshTime = Time.time;
        Debug.Log($"[ReputationMarketSystem] Refreshed stock for '{outpost.displayName}' " +
                  $"(tier: {tier.tierName}, sell: {outpost.sellStock.Count}, buy: {outpost.buyDemand.Count})");
    }

    /// <summary>
    /// 刷新所有据点库存
    /// </summary>
    public void RefreshAllOutpostStocks()
    {
        if (npcManager == null) return;

        var outposts = npcManager.GetAllOutposts();
        foreach (var outpost in outposts)
        {
            RefreshOutpostStock(outpost);
        }
    }

    // ============ Event Handlers ============

    private void OnReputationChanged(string factionId, int oldValue, int newValue)
    {
        if (npcManager == null) return;

        var faction = npcManager.GetFaction(factionId);
        if (faction == null || faction.marketConfig == null) return;

        // 检查是否跨越了层级阈值
        int oldTier = faction.marketConfig.GetTierIndex(oldValue);
        int newTier = faction.marketConfig.GetTierIndex(newValue);

        if (oldTier != newTier)
        {
            Debug.Log($"[ReputationMarketSystem] Faction '{factionId}' tier changed: {oldTier} -> {newTier}, refreshing stocks");
            // 刷新该势力所有据点的库存
            var outposts = npcManager.GetOutpostsByFaction(factionId);
            foreach (var outpost in outposts)
            {
                RefreshOutpostStock(outpost);
            }
        }
    }

    // ============ Utility ============

    /// <summary>
    /// 获取资源的基础价格
    /// </summary>
    public float GetBasePrice(string resourceId)
    {
        if (_basePriceCache.TryGetValue(resourceId, out float price))
            return price;
        return 10f; // 默认价格
    }
}

/// <summary>
/// 资源基础价格条目
/// </summary>
[Serializable]
public class ResourceBasePrice
{
    public string resourceId;
    public float basePrice = 10f;
}

/// <summary>
/// 市场商品列表项
/// </summary>
[Serializable]
public class MarketListing
{
    public string resourceId;
    public int availableAmount;
    public float pricePerUnit;
    public bool isSpecialGood;
    public TradeDirection direction;
}
