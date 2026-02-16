using System;
using UnityEngine;

/// <summary>
/// 道路段数据 - 单个格子的道路信息
/// </summary>
[Serializable]
public class RoadSegment
{
    [Header("位置")]
    public Vector2Int cell;

    [Header("道路属性")]
    [Tooltip("道路类型ID")]
    public string roadTypeId;

    [Tooltip("道路唯一ID（用于整条道路的标识）")]
    public string roadNetworkId;

    [Header("连接信息")]
    [Tooltip("连接的方向（上下左右）")]
    public RoadDirection connections;

    [Header("耐久度")]
    [Tooltip("当前耐久度（0~100）")]
    [Range(0f, 100f)]
    public float durability = 100f;

    [Tooltip("累计运输量（用于计算磨损）")]
    public int totalCargoTransported = 0;

    /// <summary>
    /// 道路损坏等级
    /// </summary>
    public enum DamageLevel
    {
        Normal,     // 100%~50% - 无影响
        Light,      // 50%~25% - 运输时间+50%
        Severe,     // 25%~1% - 运输时间+100%，货物额外损失5%
        Broken      // 0% - 不可通行
    }

    [Flags]
    public enum RoadDirection
    {
        None = 0,
        Up = 1,      // +Y
        Down = 2,    // -Y
        Left = 4,    // -X
        Right = 8    // +X
    }

    // ============ 构造函数 ============

    public RoadSegment()
    {
        cell = Vector2Int.zero;
        connections = RoadDirection.None;
    }

    public RoadSegment(Vector2Int cell, string roadTypeId)
    {
        this.cell = cell;
        this.roadTypeId = roadTypeId;
        this.connections = RoadDirection.None;
        this.durability = 100f;
        this.totalCargoTransported = 0;
    }

    public RoadSegment(Vector2Int cell, string roadTypeId, string networkId)
    {
        this.cell = cell;
        this.roadTypeId = roadTypeId;
        this.roadNetworkId = networkId;
        this.connections = RoadDirection.None;
        this.durability = 100f;
        this.totalCargoTransported = 0;
    }

    // ============ 连接操作 ============

    /// <summary>
    /// 添加连接方向
    /// </summary>
    public void AddConnection(RoadDirection direction)
    {
        connections |= direction;
    }

    /// <summary>
    /// 移除连接方向
    /// </summary>
    public void RemoveConnection(RoadDirection direction)
    {
        connections &= ~direction;
    }

    /// <summary>
    /// 检查是否有指定方向的连接
    /// </summary>
    public bool HasConnection(RoadDirection direction)
    {
        return (connections & direction) != 0;
    }

    /// <summary>
    /// 获取连接数量
    /// </summary>
    public int ConnectionCount
    {
        get
        {
            int count = 0;
            if (HasConnection(RoadDirection.Up)) count++;
            if (HasConnection(RoadDirection.Down)) count++;
            if (HasConnection(RoadDirection.Left)) count++;
            if (HasConnection(RoadDirection.Right)) count++;
            return count;
        }
    }

    /// <summary>
    /// 是否是端点（只有一个连接）
    /// </summary>
    public bool IsEndpoint => ConnectionCount == 1;

    /// <summary>
    /// 是否是交叉口（三个以上连接）
    /// </summary>
    public bool IsIntersection => ConnectionCount >= 3;

    // ============ 方向工具方法 ============

    /// <summary>
    /// 从相邻格子位置获取方向
    /// </summary>
    public static RoadDirection GetDirectionTo(Vector2Int from, Vector2Int to)
    {
        Vector2Int diff = to - from;

        if (diff == Vector2Int.up) return RoadDirection.Up;
        if (diff == Vector2Int.down) return RoadDirection.Down;
        if (diff == Vector2Int.left) return RoadDirection.Left;
        if (diff == Vector2Int.right) return RoadDirection.Right;

        return RoadDirection.None;
    }

    /// <summary>
    /// 获取方向的相反方向
    /// </summary>
    public static RoadDirection GetOppositeDirection(RoadDirection direction)
    {
        return direction switch
        {
            RoadDirection.Up => RoadDirection.Down,
            RoadDirection.Down => RoadDirection.Up,
            RoadDirection.Left => RoadDirection.Right,
            RoadDirection.Right => RoadDirection.Left,
            _ => RoadDirection.None
        };
    }

    /// <summary>
    /// 获取方向对应的偏移量
    /// </summary>
    public static Vector2Int GetDirectionOffset(RoadDirection direction)
    {
        return direction switch
        {
            RoadDirection.Up => Vector2Int.up,
            RoadDirection.Down => Vector2Int.down,
            RoadDirection.Left => Vector2Int.left,
            RoadDirection.Right => Vector2Int.right,
            _ => Vector2Int.zero
        };
    }

    /// <summary>
    /// 获取所有相邻格子
    /// </summary>
    public static Vector2Int[] GetAdjacentCells(Vector2Int cell)
    {
        return new Vector2Int[]
        {
            cell + Vector2Int.up,
            cell + Vector2Int.down,
            cell + Vector2Int.left,
            cell + Vector2Int.right
        };
    }

    // ============ 耐久度方法 ============

    /// <summary>
    /// 获取当前损坏等级
    /// </summary>
    public DamageLevel GetDamageLevel()
    {
        if (durability <= 0f) return DamageLevel.Broken;
        if (durability <= 25f) return DamageLevel.Severe;
        if (durability <= 50f) return DamageLevel.Light;
        return DamageLevel.Normal;
    }

    /// <summary>
    /// 道路是否可通行
    /// </summary>
    public bool IsPassable => durability > 0f;

    /// <summary>
    /// 获取损坏等级对应的运输时间倍率
    /// </summary>
    public float GetDamageTimeMultiplier()
    {
        return GetDamageLevel() switch
        {
            DamageLevel.Light => 1.5f,   // +50%
            DamageLevel.Severe => 2.0f,  // +100%
            DamageLevel.Broken => float.MaxValue, // 不可通行
            _ => 1.0f
        };
    }

    /// <summary>
    /// 获取损坏等级对应的额外货物损失率
    /// </summary>
    public float GetDamageCargoLossRate()
    {
        return GetDamageLevel() switch
        {
            DamageLevel.Severe => 0.05f, // 5%额外损失
            _ => 0f
        };
    }

    /// <summary>
    /// 应用运输磨损
    /// </summary>
    /// <param name="cargoAmount">本次运输的货物量</param>
    /// <param name="roadType">道路类型定义</param>
    /// <returns>道路是否仍然可通行</returns>
    public bool ApplyTransportWear(int cargoAmount, RoadType roadType)
    {
        if (cargoAmount <= 0 || roadType == null) return IsPassable;

        totalCargoTransported += cargoAmount;

        // 超过免维护阈值才会损耗
        int excessCargo = totalCargoTransported - roadType.durabilityFreeThreshold;
        if (excessCargo <= 0) return IsPassable;

        // 计算应该损耗的耐久度百分比
        // 总损耗 = 超出量 / 每单位损耗量
        float totalDecay = (float)excessCargo / roadType.durabilityDecayPerUnit;

        // 新耐久度 = 100 - 总损耗
        float newDurability = 100f - totalDecay;
        durability = Mathf.Clamp(newDurability, 0f, 100f);

        return IsPassable;
    }

    /// <summary>
    /// 修复道路
    /// </summary>
    /// <param name="repairPercent">修复的耐久度百分比</param>
    public void Repair(float repairPercent)
    {
        if (repairPercent <= 0f) return;

        durability = Mathf.Clamp(durability + repairPercent, 0f, 100f);

        // 如果完全修复，重置运输计数
        if (durability >= 100f)
        {
            totalCargoTransported = 0;
        }
    }

    /// <summary>
    /// 完全修复道路
    /// </summary>
    public void FullRepair()
    {
        durability = 100f;
        totalCargoTransported = 0;
    }

    /// <summary>
    /// 计算修复到满耐久度需要的金钱成本
    /// </summary>
    public float CalculateRepairCost(RoadType roadType)
    {
        if (roadType == null) return 0f;
        float repairNeeded = 100f - durability;
        return repairNeeded * roadType.repairCostPerPercent;
    }

    // ============ 序列化 ============

    public RoadSegment Clone()
    {
        return new RoadSegment
        {
            cell = this.cell,
            roadTypeId = this.roadTypeId,
            roadNetworkId = this.roadNetworkId,
            connections = this.connections,
            durability = this.durability,
            totalCargoTransported = this.totalCargoTransported
        };
    }
}
