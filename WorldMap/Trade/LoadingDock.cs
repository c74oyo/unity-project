using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 装卸码头 - 基地中用于连接贸易路线的建筑
/// 每个基地需要至少一个装卸码头才能进行贸易
/// </summary>
[Serializable]
public class LoadingDock
{
    [Header("基本信息")]
    [Tooltip("码头唯一ID")]
    public string dockId;

    [Tooltip("所属基地ID")]
    public string baseId;

    [Tooltip("码头名称")]
    public string displayName;

    [Header("位置")]
    [Tooltip("在基地内的格子坐标")]
    public Vector2Int localCell;

    [Header("容量")]
    [Tooltip("最大同时处理的贸易路线数")]
    [Range(1, 10)]
    public int maxRoutes = 3;

    [Tooltip("单次运输的最大货物量")]
    public int maxCargoPerTrip = 100;

    [Header("效率")]
    [Tooltip("运输效率倍率（影响运输速度）")]
    [Range(0.5f, 2f)]
    public float efficiencyMultiplier = 1f;

    [Tooltip("货物损失减少率（0-1，减少运输过程中的损失）")]
    [Range(0f, 0.5f)]
    public float lossReduction = 0f;

    [Header("升级")]
    [Tooltip("当前等级")]
    [Range(1, 5)]
    public int level = 1;

    [Header("状态")]
    [Tooltip("是否启用")]
    public bool isActive = true;

    [Tooltip("当前连接的贸易路线ID列表")]
    public List<string> connectedRouteIds = new();

    // ============ 构造函数 ============

    public LoadingDock()
    {
        dockId = Guid.NewGuid().ToString();
    }

    public LoadingDock(string baseId, Vector2Int localCell, string name = null)
    {
        this.dockId = Guid.NewGuid().ToString();
        this.baseId = baseId;
        this.localCell = localCell;
        this.displayName = name ?? "装卸码头";
    }

    // ============ 容量检查 ============

    /// <summary>
    /// 当前连接的路线数
    /// </summary>
    public int CurrentRouteCount => connectedRouteIds.Count;

    /// <summary>
    /// 是否还能连接新路线
    /// </summary>
    public bool CanConnectNewRoute => isActive && CurrentRouteCount < maxRoutes;

    /// <summary>
    /// 剩余可连接的路线数
    /// </summary>
    public int RemainingCapacity => Mathf.Max(0, maxRoutes - CurrentRouteCount);

    // ============ 路线管理 ============

    /// <summary>
    /// 连接贸易路线
    /// </summary>
    public bool ConnectRoute(string routeId)
    {
        if (!CanConnectNewRoute) return false;
        if (connectedRouteIds.Contains(routeId)) return false;

        connectedRouteIds.Add(routeId);
        return true;
    }

    /// <summary>
    /// 断开贸易路线
    /// </summary>
    public bool DisconnectRoute(string routeId)
    {
        return connectedRouteIds.Remove(routeId);
    }

    /// <summary>
    /// 检查是否连接了指定路线
    /// </summary>
    public bool IsRouteConnected(string routeId)
    {
        return connectedRouteIds.Contains(routeId);
    }

    // ============ 升级 ============

    /// <summary>
    /// 获取升级后的属性
    /// </summary>
    public static LoadingDockStats GetStatsForLevel(int level)
    {
        return new LoadingDockStats
        {
            maxRoutes = 2 + level,                    // 3, 4, 5, 6, 7
            maxCargoPerTrip = 50 + level * 50,        // 100, 150, 200, 250, 300
            efficiencyMultiplier = 0.8f + level * 0.2f, // 1.0, 1.2, 1.4, 1.6, 1.8
            lossReduction = level * 0.05f              // 0.05, 0.10, 0.15, 0.20, 0.25
        };
    }

    /// <summary>
    /// 应用升级
    /// </summary>
    public void ApplyLevel(int newLevel)
    {
        level = Mathf.Clamp(newLevel, 1, 5);
        var stats = GetStatsForLevel(level);
        maxRoutes = stats.maxRoutes;
        maxCargoPerTrip = stats.maxCargoPerTrip;
        efficiencyMultiplier = stats.efficiencyMultiplier;
        lossReduction = stats.lossReduction;
    }

    // ============ 克隆 ============

    public LoadingDock Clone()
    {
        var clone = new LoadingDock
        {
            dockId = this.dockId,
            baseId = this.baseId,
            displayName = this.displayName,
            localCell = this.localCell,
            maxRoutes = this.maxRoutes,
            maxCargoPerTrip = this.maxCargoPerTrip,
            efficiencyMultiplier = this.efficiencyMultiplier,
            lossReduction = this.lossReduction,
            level = this.level,
            isActive = this.isActive,
            connectedRouteIds = new List<string>(this.connectedRouteIds)
        };
        return clone;
    }
}

/// <summary>
/// 装卸码头属性
/// </summary>
public struct LoadingDockStats
{
    public int maxRoutes;
    public int maxCargoPerTrip;
    public float efficiencyMultiplier;
    public float lossReduction;
}

/// <summary>
/// 装卸码头建筑定义 - 可以放入BuildableDefinition系统
/// </summary>
[CreateAssetMenu(fileName = "LoadingDockDefinition", menuName = "Game/Buildings/Loading Dock Definition")]
public class LoadingDockDefinition : ScriptableObject
{
    [Header("基本信息")]
    public string buildingId = "loading_dock";
    public string displayName = "装卸码头";
    [TextArea(2, 4)]
    public string description = "用于连接贸易路线，进行货物运输";
    public Sprite icon;

    [Header("建造")]
    public Vector2Int size = new Vector2Int(2, 2);
    public ResourceAmount[] buildCost;
    public float buildTime = 5f;

    [Header("初始属性")]
    public int initialMaxRoutes = 3;
    public int initialMaxCargo = 100;

    [Header("升级成本")]
    public ResourceAmount[] upgradeCostPerLevel;
    public float upgradeTimePerLevel = 3f;

    /// <summary>
    /// 创建码头实例
    /// </summary>
    public LoadingDock CreateInstance(string baseId, Vector2Int localCell)
    {
        var dock = new LoadingDock(baseId, localCell, displayName);
        dock.maxRoutes = initialMaxRoutes;
        dock.maxCargoPerTrip = initialMaxCargo;
        return dock;
    }
}
