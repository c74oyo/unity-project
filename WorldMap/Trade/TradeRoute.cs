using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 贸易路线数据 - 连接玩家基地和NPC据点
/// </summary>
[Serializable]
public class TradeRoute
{
    [Header("基本信息")]
    [Tooltip("路线唯一ID")]
    public string routeId;

    [Tooltip("路线名称")]
    public string displayName;

    [Header("端点")]
    [Tooltip("起点基地ID")]
    public string sourceBaseId;

    [Tooltip("起点格子坐标")]
    public Vector2Int sourceCell;

    [Tooltip("目标据点ID")]
    public string targetOutpostId;

    [Tooltip("目标格子坐标")]
    public Vector2Int targetCell;

    [Header("路径")]
    [Tooltip("道路路径（格子坐标列表）")]
    public List<Vector2Int> roadPath = new();

    [Header("状态")]
    [Tooltip("路线是否激活")]
    public bool isActive = true;

    [Tooltip("路线是否有效（路径完整）")]
    public bool isValid = false;

    [Header("运输设置")]
    [Tooltip("运输货物列表")]
    public List<TradeCargoItem> cargoItems = new();

    [Tooltip("运输频率（每X回合运输一次）- 已弃用，使用 transportIntervalSeconds")]
    [Range(1, 20)]
    public int transportInterval = 5;

    [Tooltip("上次运输的回合 - 已弃用，使用 lastDispatchTime")]
    public int lastTransportTurn = 0;

    [Header("Real-time Transport")]
    [Tooltip("自动派遣间隔（秒）")]
    public float transportIntervalSeconds = 60f;

    [Tooltip("上次派遣的 Time.time")]
    public float lastDispatchTime = -999f;

    [Tooltip("是否自动循环派遣")]
    public bool autoLoop = true;

    [Header("载具需求")]
    [Tooltip("每次运输需要的载具数量")]
    [Range(1, 10)]
    public int requiredVehicles = 1;

    [Tooltip("载具资源ID")]
    public string vehicleResourceId = "vehicle_truck";

    [Tooltip("每辆载具的容量（单位）")]
    [Range(10, 500)]
    public int vehicleCapacityPerUnit = 100;

    [Header("统计")]
    [Tooltip("总运输次数")]
    public int totalTrips = 0;

    [Tooltip("总运输货物量")]
    public int totalCargoTransported = 0;

    [Tooltip("总货物损失量")]
    public int totalCargoLost = 0;

    // ============ 构造函数 ============

    public TradeRoute()
    {
        routeId = Guid.NewGuid().ToString();
    }

    public TradeRoute(string sourceBaseId, Vector2Int sourceCell, string targetOutpostId, Vector2Int targetCell)
    {
        this.routeId = Guid.NewGuid().ToString();
        this.sourceBaseId = sourceBaseId;
        this.sourceCell = sourceCell;
        this.targetOutpostId = targetOutpostId;
        this.targetCell = targetCell;
        this.displayName = $"路线_{sourceCell}_{targetCell}";
    }

    // ============ 路径计算 ============

    /// <summary>
    /// 计算路径长度
    /// </summary>
    public int PathLength => roadPath?.Count ?? 0;

    /// <summary>
    /// 估算运输时间（基于路径长度和道路等级）
    /// </summary>
    public float EstimateTransportTime(RoadNetwork roadNetwork)
    {
        if (roadNetwork == null || roadPath == null || roadPath.Count == 0)
            return float.MaxValue;

        return roadNetwork.CalculatePathTravelTime(roadPath);
    }

    // ============ 货物管理 ============

    /// <summary>
    /// 添加货物
    /// </summary>
    public void AddCargoItem(string resourceId, int amount, TradeDirection direction)
    {
        var existing = cargoItems.Find(c => c.resourceId == resourceId && c.direction == direction);
        if (existing != null)
        {
            existing.amount += amount;
        }
        else
        {
            cargoItems.Add(new TradeCargoItem(resourceId, amount, direction));
        }
    }

    /// <summary>
    /// 移除货物
    /// </summary>
    public bool RemoveCargoItem(string resourceId, TradeDirection direction)
    {
        var item = cargoItems.Find(c => c.resourceId == resourceId && c.direction == direction);
        if (item != null)
        {
            cargoItems.Remove(item);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 获取出口货物（从基地到据点）
    /// </summary>
    public List<TradeCargoItem> GetExportCargo()
    {
        return cargoItems.FindAll(c => c.direction == TradeDirection.Export);
    }

    /// <summary>
    /// 获取进口货物（从据点到基地）
    /// </summary>
    public List<TradeCargoItem> GetImportCargo()
    {
        return cargoItems.FindAll(c => c.direction == TradeDirection.Import);
    }

    // ============ 道路状态检查 ============

    /// <summary>
    /// 检查路径是否可通行（无断裂道路）
    /// </summary>
    public bool IsPathPassable(RoadNetwork roadNetwork)
    {
        if (roadNetwork == null || roadPath == null || roadPath.Count == 0)
            return false;

        return roadNetwork.IsPathPassable(roadPath);
    }

    /// <summary>
    /// 检查路径上是否有损坏的道路
    /// </summary>
    public bool HasDamagedRoads(RoadNetwork roadNetwork)
    {
        if (roadNetwork == null || roadPath == null || roadPath.Count == 0)
            return false;

        foreach (var cell in roadPath)
        {
            var segment = roadNetwork.GetRoadAt(cell);
            if (segment != null && segment.GetDamageLevel() != RoadSegment.DamageLevel.Normal)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 检查路径上是否有严重损坏的道路
    /// </summary>
    public bool HasSevereDamage(RoadNetwork roadNetwork)
    {
        if (roadNetwork == null || roadPath == null || roadPath.Count == 0)
            return false;

        foreach (var cell in roadPath)
        {
            var segment = roadNetwork.GetRoadAt(cell);
            if (segment != null)
            {
                var level = segment.GetDamageLevel();
                if (level == RoadSegment.DamageLevel.Severe || level == RoadSegment.DamageLevel.Broken)
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// 获取路径上损坏道路的数量
    /// </summary>
    public int GetDamagedRoadCount(RoadNetwork roadNetwork)
    {
        if (roadNetwork == null || roadPath == null || roadPath.Count == 0)
            return 0;

        int count = 0;
        foreach (var cell in roadPath)
        {
            var segment = roadNetwork.GetRoadAt(cell);
            if (segment != null && segment.GetDamageLevel() != RoadSegment.DamageLevel.Normal)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// 获取路径的平均耐久度
    /// </summary>
    public float GetAveragePathDurability(RoadNetwork roadNetwork)
    {
        if (roadNetwork == null || roadPath == null || roadPath.Count == 0)
            return 0f;

        float total = 0f;
        int count = 0;
        foreach (var cell in roadPath)
        {
            var segment = roadNetwork.GetRoadAt(cell);
            if (segment != null)
            {
                total += segment.durability;
                count++;
            }
        }
        return count > 0 ? total / count : 0f;
    }

    /// <summary>
    /// 计算考虑损坏后的实际运输时间
    /// </summary>
    /// <param name="roadNetwork">道路网络</param>
    /// <param name="baseTimePerCell">每格基础时间（默认1秒）</param>
    public float EstimateTransportTimeWithDamage(RoadNetwork roadNetwork, float baseTimePerCell = 1f)
    {
        if (roadNetwork == null || roadPath == null || roadPath.Count == 0)
            return float.MaxValue;

        return roadNetwork.CalculatePathTravelTimeWithDamage(roadPath, baseTimePerCell);
    }

    /// <summary>
    /// 获取路径状态摘要（用于UI显示）
    /// </summary>
    public string GetPathStatusSummary(RoadNetwork roadNetwork)
    {
        if (roadNetwork == null || roadPath == null || roadPath.Count == 0)
            return "无路径";

        if (!IsPathPassable(roadNetwork))
            return "<color=red>道路中断</color>";

        if (HasSevereDamage(roadNetwork))
            return "<color=orange>道路严重损坏</color>";

        if (HasDamagedRoads(roadNetwork))
            return "<color=yellow>道路需要维护</color>";

        return "<color=green>道路状况良好</color>";
    }

    // ============ 状态检查 ============

    /// <summary>
    /// 检查是否可以运输（回合制，已弃用）
    /// </summary>
    public bool CanTransport(int currentTurn)
    {
        if (!isActive || !isValid) return false;
        if (cargoItems.Count == 0) return false;
        return currentTurn - lastTransportTurn >= transportInterval;
    }

    /// <summary>
    /// 检查是否可以派遣（实时模式）
    /// </summary>
    public bool CanDispatchRealtime(float currentTime)
    {
        if (!isActive || !isValid) return false;
        if (!autoLoop) return false;
        if (cargoItems.Count == 0) return false;
        return currentTime - lastDispatchTime >= transportIntervalSeconds;
    }

    // ============ 克隆 ============

    public TradeRoute Clone()
    {
        var clone = new TradeRoute
        {
            routeId = this.routeId,
            displayName = this.displayName,
            sourceBaseId = this.sourceBaseId,
            sourceCell = this.sourceCell,
            targetOutpostId = this.targetOutpostId,
            targetCell = this.targetCell,
            isActive = this.isActive,
            isValid = this.isValid,
            transportInterval = this.transportInterval,
            lastTransportTurn = this.lastTransportTurn,
            totalTrips = this.totalTrips,
            totalCargoTransported = this.totalCargoTransported,
            totalCargoLost = this.totalCargoLost,
            transportIntervalSeconds = this.transportIntervalSeconds,
            lastDispatchTime = this.lastDispatchTime,
            autoLoop = this.autoLoop
        };

        clone.roadPath = new List<Vector2Int>(this.roadPath);

        foreach (var item in cargoItems)
            clone.cargoItems.Add(item.Clone());

        return clone;
    }
}

/// <summary>
/// 贸易货物项
/// </summary>
[Serializable]
public class TradeCargoItem
{
    public string resourceId;
    public int amount;
    public TradeDirection direction;

    public TradeCargoItem() { }

    public TradeCargoItem(string resourceId, int amount, TradeDirection direction)
    {
        this.resourceId = resourceId;
        this.amount = amount;
        this.direction = direction;
    }

    public TradeCargoItem Clone()
    {
        return new TradeCargoItem(resourceId, amount, direction);
    }
}

/// <summary>
/// 贸易方向
/// </summary>
public enum TradeDirection
{
    Export, // 出口（基地 -> 据点）
    Import  // 进口（据点 -> 基地）
}
