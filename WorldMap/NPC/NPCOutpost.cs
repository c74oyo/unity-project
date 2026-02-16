using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPC据点数据 - 大地图上的NPC交易点
/// </summary>
[Serializable]
public class NPCOutpost
{
    [Header("基本信息")]
    [Tooltip("据点唯一ID")]
    public string outpostId;

    [Tooltip("据点名称")]
    public string displayName;

    [Tooltip("所属势力ID")]
    public string factionId;

    [Header("位置")]
    [Tooltip("大地图格子坐标（据点中心）")]
    public Vector2Int cell;

    [Tooltip("据点占地大小（默认1x1）")]
    public Vector2Int size = Vector2Int.one;

    [Header("状态")]
    [Tooltip("是否已发现")]
    public bool isDiscovered = false;

    [Tooltip("是否可以交易")]
    public bool canTrade = true;

    [Header("库存")]
    [Tooltip("当前出售的资源库存")]
    public List<ResourceStock> sellStock = new();

    [Tooltip("当前收购的资源需求")]
    public List<ResourceStock> buyDemand = new();

    [Header("刷新")]
    [Tooltip("上次刷新库存的时间（游戏内回合）")]
    public int lastRefreshTurn = 0;

    [Tooltip("库存刷新间隔（回合）")]
    public int refreshInterval = 10;

    [Tooltip("上次库存刷新的实时时间")]
    public float lastStockRefreshTime;

    [Tooltip("库存刷新间隔（秒）")]
    public float stockRefreshIntervalSeconds = 120f;

    [Header("任务公告板")]
    [Tooltip("当前该据点发布的可接受任务ID列表")]
    public List<string> questBoardIds = new();

    [Tooltip("上次任务刷新的实时时间")]
    public float lastQuestRefreshTime;

    // ============ 构造函数 ============

    public NPCOutpost()
    {
        outpostId = Guid.NewGuid().ToString();
    }

    public NPCOutpost(string factionId, Vector2Int cell, string name = null)
    {
        this.outpostId = Guid.NewGuid().ToString();
        this.factionId = factionId;
        this.cell = cell;
        this.displayName = name ?? $"据点_{cell.x}_{cell.y}";
    }

    // ============ 库存操作 ============

    /// <summary>
    /// 获取某资源的出售库存
    /// </summary>
    public ResourceStock GetSellStock(string resourceId)
    {
        return sellStock.Find(s => s.resourceId == resourceId);
    }

    /// <summary>
    /// 获取某资源的收购需求
    /// </summary>
    public ResourceStock GetBuyDemand(string resourceId)
    {
        return buyDemand.Find(s => s.resourceId == resourceId);
    }

    /// <summary>
    /// 添加出售库存
    /// </summary>
    public void AddSellStock(string resourceId, int amount, float pricePerUnit)
    {
        var existing = GetSellStock(resourceId);
        if (existing != null)
        {
            existing.amount += amount;
        }
        else
        {
            sellStock.Add(new ResourceStock(resourceId, amount, pricePerUnit));
        }
    }

    /// <summary>
    /// 添加收购需求
    /// </summary>
    public void AddBuyDemand(string resourceId, int amount, float pricePerUnit)
    {
        var existing = GetBuyDemand(resourceId);
        if (existing != null)
        {
            existing.amount += amount;
        }
        else
        {
            buyDemand.Add(new ResourceStock(resourceId, amount, pricePerUnit));
        }
    }

    /// <summary>
    /// 从出售库存中扣除
    /// </summary>
    public bool TryDeductSellStock(string resourceId, int amount)
    {
        var stock = GetSellStock(resourceId);
        if (stock == null || stock.amount < amount)
            return false;

        stock.amount -= amount;
        if (stock.amount <= 0)
        {
            sellStock.Remove(stock);
        }
        return true;
    }

    /// <summary>
    /// 从收购需求中扣除
    /// </summary>
    public bool TryDeductBuyDemand(string resourceId, int amount)
    {
        var demand = GetBuyDemand(resourceId);
        if (demand == null || demand.amount < amount)
            return false;

        demand.amount -= amount;
        if (demand.amount <= 0)
        {
            buyDemand.Remove(demand);
        }
        return true;
    }

    // ============ 克隆 ============

    public NPCOutpost Clone()
    {
        var clone = new NPCOutpost
        {
            outpostId = this.outpostId,
            displayName = this.displayName,
            factionId = this.factionId,
            cell = this.cell,
            size = this.size,
            isDiscovered = this.isDiscovered,
            canTrade = this.canTrade,
            lastRefreshTurn = this.lastRefreshTurn,
            refreshInterval = this.refreshInterval,
            lastStockRefreshTime = this.lastStockRefreshTime,
            stockRefreshIntervalSeconds = this.stockRefreshIntervalSeconds,
            lastQuestRefreshTime = this.lastQuestRefreshTime
        };

        if (questBoardIds != null)
            clone.questBoardIds = new List<string>(this.questBoardIds);

        foreach (var stock in sellStock)
            clone.sellStock.Add(stock.Clone());

        foreach (var demand in buyDemand)
            clone.buyDemand.Add(demand.Clone());

        return clone;
    }
}

/// <summary>
/// 资源库存数据
/// </summary>
[Serializable]
public class ResourceStock
{
    public string resourceId;
    public int amount;
    public float pricePerUnit;

    public ResourceStock() { }

    public ResourceStock(string resourceId, int amount, float pricePerUnit)
    {
        this.resourceId = resourceId;
        this.amount = amount;
        this.pricePerUnit = pricePerUnit;
    }

    public float TotalValue => amount * pricePerUnit;

    public ResourceStock Clone()
    {
        return new ResourceStock(resourceId, amount, pricePerUnit);
    }
}
