using System;
using UnityEngine;

/// <summary>
/// 大地图格子的完整数据
/// 支持多层属性：资源区(底层) + 区域状态(中层) + 占用物(顶层)
/// </summary>
[Serializable]
public class WorldMapCellData
{
    /// <summary>
    /// 区域状态（中层）
    /// </summary>
    public enum ZoneState
    {
        Buildable,      // 可建造
        Unbuildable,    // 不可建造（地形限制）
        Threat,         // 威胁区域（需战斗清除）
        NPCTerritory    // NPC势力范围
    }

    /// <summary>
    /// 占用类型（顶层）
    /// </summary>
    public enum OccupationType
    {
        None,           // 无占用
        Base,           // 玩家基地
        Road,           // 道路
        NPCOutpost      // NPC据点
    }

    [Header("位置")]
    public Vector2Int cell;

    [Header("底层 - 资源区")]
    [Tooltip("资源区类型ID，空表示无资源区")]
    public string resourceZoneTypeId;

    [Header("中层 - 区域状态")]
    public ZoneState zoneState = ZoneState.Buildable;

    [Tooltip("如果是NPC势力范围，对应的NPC势力ID")]
    public string npcFactionId;

    [Header("顶层 - 占用")]
    public OccupationType occupation = OccupationType.None;

    [Tooltip("占用物的ID（基地ID、道路ID等）")]
    public string occupationId;

    [Header("威胁数据")]
    [Tooltip("威胁等级（用于战斗难度和货物损失计算）")]
    [Range(0, 10)]
    public int threatLevel = 0;

    [Tooltip("威胁是否已被清除")]
    public bool threatCleared = false;

    // ============ 构造函数 ============

    public WorldMapCellData()
    {
        cell = Vector2Int.zero;
        zoneState = ZoneState.Buildable;
        occupation = OccupationType.None;
    }

    public WorldMapCellData(Vector2Int cell)
    {
        this.cell = cell;
        zoneState = ZoneState.Buildable;
        occupation = OccupationType.None;
    }

    public WorldMapCellData(Vector2Int cell, ZoneState state)
    {
        this.cell = cell;
        this.zoneState = state;
        occupation = OccupationType.None;
    }

    // ============ 查询方法 ============

    /// <summary>
    /// 是否有资源区
    /// </summary>
    public bool HasResourceZone => !string.IsNullOrEmpty(resourceZoneTypeId);

    /// <summary>
    /// 是否可以建造基地
    /// </summary>
    public bool CanBuildBase
    {
        get
        {
            // 不可建造区域
            if (zoneState == ZoneState.Unbuildable)
                return false;

            // 威胁区域需要先清除
            if (zoneState == ZoneState.Threat && !threatCleared)
                return false;

            // 已有占用
            if (occupation != OccupationType.None)
                return false;

            return true;
        }
    }

    /// <summary>
    /// 是否可以建造道路
    /// </summary>
    public bool CanBuildRoad
    {
        get
        {
            // 不可建造区域不能建路
            if (zoneState == ZoneState.Unbuildable)
                return false;

            // 已有非道路占用
            if (occupation != OccupationType.None && occupation != OccupationType.Road)
                return false;

            // 威胁区域可以建路（但运输会有损失）
            return true;
        }
    }

    /// <summary>
    /// 是否是危险区域（运输会有损失）
    /// </summary>
    public bool IsDangerous
    {
        get
        {
            // 未清除的威胁区域
            if (zoneState == ZoneState.Threat && !threatCleared)
                return true;

            return false;
        }
    }

    /// <summary>
    /// 获取道路建造成本倍率
    /// </summary>
    public float GetRoadBuildCostMultiplier()
    {
        float multiplier = 1f;

        // NPC势力范围内建造成本增加
        if (zoneState == ZoneState.NPCTerritory)
        {
            multiplier *= 1.5f; // 默认增加50%，后续可根据好感度调整
        }

        // 威胁区域建造成本增加
        if (zoneState == ZoneState.Threat && !threatCleared)
        {
            multiplier *= 1f + (threatLevel * 0.1f); // 每级威胁增加10%
        }

        return multiplier;
    }

    /// <summary>
    /// 获取货物损失率
    /// </summary>
    public float GetCargoLossRate()
    {
        if (!IsDangerous)
            return 0f;

        // 威胁区域基础损失50%，根据威胁等级调整
        return 0.5f * (1f + threatLevel * 0.05f);
    }

    // ============ 修改方法 ============

    /// <summary>
    /// 设置资源区
    /// </summary>
    public void SetResourceZone(string zoneTypeId)
    {
        resourceZoneTypeId = zoneTypeId;
    }

    /// <summary>
    /// 设置为威胁区域
    /// </summary>
    public void SetAsThreat(int level)
    {
        zoneState = ZoneState.Threat;
        threatLevel = Mathf.Clamp(level, 1, 10);
        threatCleared = false;
    }

    /// <summary>
    /// 清除威胁
    /// </summary>
    public void ClearThreat()
    {
        if (zoneState == ZoneState.Threat)
        {
            threatCleared = true;
            // 清除后变为可建造区域，但保留威胁等级记录
        }
    }

    /// <summary>
    /// 设置NPC势力范围
    /// </summary>
    public void SetNPCTerritory(string factionId)
    {
        zoneState = ZoneState.NPCTerritory;
        npcFactionId = factionId;
    }

    /// <summary>
    /// 占用格子（建造基地）
    /// </summary>
    public bool OccupyWithBase(string baseId)
    {
        if (!CanBuildBase) return false;

        occupation = OccupationType.Base;
        occupationId = baseId;
        return true;
    }

    /// <summary>
    /// 占用格子（建造道路）
    /// </summary>
    public bool OccupyWithRoad(string roadId)
    {
        if (!CanBuildRoad) return false;

        occupation = OccupationType.Road;
        occupationId = roadId;
        return true;
    }

    /// <summary>
    /// 清除占用
    /// </summary>
    public void ClearOccupation()
    {
        occupation = OccupationType.None;
        occupationId = null;
    }

    // ============ 序列化 ============

    /// <summary>
    /// 创建副本
    /// </summary>
    public WorldMapCellData Clone()
    {
        return new WorldMapCellData
        {
            cell = this.cell,
            resourceZoneTypeId = this.resourceZoneTypeId,
            zoneState = this.zoneState,
            npcFactionId = this.npcFactionId,
            occupation = this.occupation,
            occupationId = this.occupationId,
            threatLevel = this.threatLevel,
            threatCleared = this.threatCleared
        };
    }
}
