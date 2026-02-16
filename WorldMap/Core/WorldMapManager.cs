using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 大地图管理器 - 管理所有大地图格子数据
/// 整合资源区、威胁区、道路等系统
/// </summary>
public class WorldMapManager : MonoBehaviour
{
    public static WorldMapManager Instance { get; private set; }

    [Header("Grid Settings")]
    [Tooltip("引用现有的WorldMapGrid，如果设置则从它读取网格设置")]
    public WorldMapGrid existingGrid;

    [Tooltip("是否从现有WorldMapGrid同步设置（仅在existingGrid不为空时有效）")]
    public bool syncFromExistingGrid = true;

    public Vector3 origin = Vector3.zero;
    [Min(0.1f)] public float cellSize = 10f;
    [Min(1)] public int width = 100;
    [Min(1)] public int height = 100;

    [Header("Resource Zone Types")]
    [Tooltip("所有可用的资源区类型")]
    public List<ResourceZoneType> resourceZoneTypes = new();

    [Header("Visualization")]
    public bool showGrid = true;
    public bool showResourceZones = true;
    public bool showThreatZones = true;
    public Color gridColor = new Color(0.5f, 0.5f, 0.5f, 0.2f);
    public Color threatColor = new Color(1f, 0f, 0f, 0.4f);
    public Color clearedThreatColor = new Color(0.5f, 0.5f, 0f, 0.3f);
    public Color unbuildableColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
    public Color npcTerritoryColor = new Color(0.8f, 0.5f, 0f, 0.3f);

    // 数据存储
    private Dictionary<Vector2Int, WorldMapCellData> _cellDataMap = new();
    private Dictionary<string, ResourceZoneType> _zoneTypeCache = new();

    // 事件
    public event Action<Vector2Int, WorldMapCellData> OnCellDataChanged;
    public event Action<Vector2Int> OnThreatCleared;
    public event Action<Vector2Int, string> OnBaseBuilt;
    public event Action<Vector2Int, string> OnRoadBuilt;

    // ============ Lifecycle ============

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 尝试从现有的WorldMapGrid同步设置
        SyncFromExistingGrid();

        // 缓存资源区类型
        CacheResourceZoneTypes();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// 从现有的WorldMapGrid同步网格设置
    /// </summary>
    private void SyncFromExistingGrid()
    {
        if (!syncFromExistingGrid) return;

        // 如果没有手动设置，尝试自动查找
        if (existingGrid == null)
        {
            existingGrid = FindObjectOfType<WorldMapGrid>();
        }

        if (existingGrid != null)
        {
            origin = existingGrid.origin;
            cellSize = existingGrid.cellSize;
            width = existingGrid.width;
            height = existingGrid.height;
            Debug.Log($"[WorldMapManager] Synced settings from WorldMapGrid: " +
                      $"origin={origin}, cellSize={cellSize}, size={width}x{height}");
        }
    }

    private void CacheResourceZoneTypes()
    {
        _zoneTypeCache.Clear();
        foreach (var zoneType in resourceZoneTypes)
        {
            if (zoneType != null && !string.IsNullOrEmpty(zoneType.zoneId))
            {
                _zoneTypeCache[zoneType.zoneId] = zoneType;
            }
        }
    }

    // ============ Grid Operations ============

    public bool IsInBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height;
    }

    public Vector2Int WorldToCell(Vector3 worldPos)
    {
        Vector3 local = worldPos - origin;
        int x = Mathf.FloorToInt(local.x / cellSize);
        int z = Mathf.FloorToInt(local.z / cellSize);
        return new Vector2Int(x, z);
    }

    public Vector3 CellToWorldCenter(Vector2Int cell)
    {
        return origin + new Vector3(
            (cell.x + 0.5f) * cellSize,
            0f,
            (cell.y + 0.5f) * cellSize
        );
    }

    // ============ Cell Data Management ============

    /// <summary>
    /// 获取格子数据，如果不存在则创建默认数据
    /// </summary>
    public WorldMapCellData GetOrCreateCellData(Vector2Int cell)
    {
        if (!IsInBounds(cell))
            return null;

        if (!_cellDataMap.TryGetValue(cell, out var data))
        {
            data = new WorldMapCellData(cell);
            _cellDataMap[cell] = data;
        }
        return data;
    }

    /// <summary>
    /// 获取格子数据，不存在返回null
    /// </summary>
    public WorldMapCellData GetCellData(Vector2Int cell)
    {
        _cellDataMap.TryGetValue(cell, out var data);
        return data;
    }

    /// <summary>
    /// 设置格子数据
    /// </summary>
    public void SetCellData(Vector2Int cell, WorldMapCellData data)
    {
        if (!IsInBounds(cell)) return;

        data.cell = cell;
        _cellDataMap[cell] = data;
        OnCellDataChanged?.Invoke(cell, data);
    }

    // ============ Resource Zone Operations ============

    /// <summary>
    /// 获取资源区类型
    /// </summary>
    public ResourceZoneType GetResourceZoneType(string zoneTypeId)
    {
        if (string.IsNullOrEmpty(zoneTypeId)) return null;
        _zoneTypeCache.TryGetValue(zoneTypeId, out var zoneType);
        return zoneType;
    }

    /// <summary>
    /// 设置格子的资源区
    /// </summary>
    public void SetResourceZone(Vector2Int cell, string zoneTypeId)
    {
        var data = GetOrCreateCellData(cell);
        if (data == null) return;

        data.SetResourceZone(zoneTypeId);
        OnCellDataChanged?.Invoke(cell, data);
    }

    /// <summary>
    /// 批量设置资源区（矩形区域）
    /// </summary>
    public void SetResourceZoneArea(Vector2Int anchor, Vector2Int size, string zoneTypeId)
    {
        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                Vector2Int cell = new Vector2Int(anchor.x + x, anchor.y + y);
                SetResourceZone(cell, zoneTypeId);
            }
        }
    }

    /// <summary>
    /// 获取格子的资源区类型
    /// </summary>
    public ResourceZoneType GetCellResourceZone(Vector2Int cell)
    {
        var data = GetCellData(cell);
        if (data == null || !data.HasResourceZone) return null;
        return GetResourceZoneType(data.resourceZoneTypeId);
    }

    // ============ Threat Zone Operations ============

    /// <summary>
    /// 设置威胁区域
    /// </summary>
    public void SetThreatZone(Vector2Int cell, int threatLevel)
    {
        var data = GetOrCreateCellData(cell);
        if (data == null) return;

        data.SetAsThreat(threatLevel);
        OnCellDataChanged?.Invoke(cell, data);
    }

    /// <summary>
    /// 批量设置威胁区域
    /// </summary>
    public void SetThreatZoneArea(Vector2Int anchor, Vector2Int size, int threatLevel)
    {
        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                Vector2Int cell = new Vector2Int(anchor.x + x, anchor.y + y);
                SetThreatZone(cell, threatLevel);
            }
        }
    }

    /// <summary>
    /// 清除威胁（战斗胜利后调用）
    /// </summary>
    public void ClearThreat(Vector2Int cell)
    {
        var data = GetCellData(cell);
        if (data == null) return;

        data.ClearThreat();
        OnCellDataChanged?.Invoke(cell, data);
        OnThreatCleared?.Invoke(cell);
    }

    /// <summary>
    /// 批量清除威胁
    /// </summary>
    public void ClearThreatArea(Vector2Int anchor, Vector2Int size)
    {
        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                Vector2Int cell = new Vector2Int(anchor.x + x, anchor.y + y);
                ClearThreat(cell);
            }
        }
    }

    // ============ Unbuildable Zone Operations ============

    /// <summary>
    /// 设置不可建造区域
    /// </summary>
    public void SetUnbuildable(Vector2Int cell)
    {
        var data = GetOrCreateCellData(cell);
        if (data == null) return;

        data.zoneState = WorldMapCellData.ZoneState.Unbuildable;
        OnCellDataChanged?.Invoke(cell, data);
    }

    /// <summary>
    /// 批量设置不可建造区域
    /// </summary>
    public void SetUnbuildableArea(Vector2Int anchor, Vector2Int size)
    {
        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                Vector2Int cell = new Vector2Int(anchor.x + x, anchor.y + y);
                SetUnbuildable(cell);
            }
        }
    }

    // ============ NPC Territory Operations ============

    /// <summary>
    /// 设置NPC势力范围
    /// </summary>
    public void SetNPCTerritory(Vector2Int cell, string factionId)
    {
        var data = GetOrCreateCellData(cell);
        if (data == null) return;

        data.SetNPCTerritory(factionId);
        OnCellDataChanged?.Invoke(cell, data);
    }

    /// <summary>
    /// 设置NPC势力范围（菱形区域）
    /// </summary>
    public void SetNPCTerritoryDiamond(Vector2Int center, int radius, string factionId)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                // 菱形判定：|dx| + |dy| <= radius
                if (Mathf.Abs(dx) + Mathf.Abs(dy) <= radius)
                {
                    Vector2Int cell = new Vector2Int(center.x + dx, center.y + dy);
                    if (IsInBounds(cell))
                    {
                        SetNPCTerritory(cell, factionId);
                    }
                }
            }
        }
    }

    // ============ Building Operations ============

    /// <summary>
    /// 检查是否可以建造基地
    /// </summary>
    public bool CanBuildBaseAt(Vector2Int cell)
    {
        if (!IsInBounds(cell)) return false;

        var data = GetOrCreateCellData(cell);
        return data.CanBuildBase;
    }

    /// <summary>
    /// 在指定位置建造基地
    /// </summary>
    public bool TryBuildBase(Vector2Int cell, string baseId)
    {
        if (!CanBuildBaseAt(cell)) return false;

        var data = GetOrCreateCellData(cell);
        if (data.OccupyWithBase(baseId))
        {
            OnCellDataChanged?.Invoke(cell, data);
            OnBaseBuilt?.Invoke(cell, baseId);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 检查是否可以建造道路
    /// </summary>
    public bool CanBuildRoadAt(Vector2Int cell)
    {
        if (!IsInBounds(cell)) return false;

        // 交叉检查 WorldMapGrid（基地占据在此处标记为 CellType.Base）
        if (existingGrid != null && existingGrid.IsCellOccupied(cell))
            return false;

        var data = GetOrCreateCellData(cell);
        return data.CanBuildRoad;
    }

    /// <summary>
    /// 在指定位置建造道路
    /// </summary>
    public bool TryBuildRoad(Vector2Int cell, string roadId)
    {
        if (!CanBuildRoadAt(cell)) return false;

        var data = GetOrCreateCellData(cell);
        if (data.OccupyWithRoad(roadId))
        {
            OnCellDataChanged?.Invoke(cell, data);
            OnRoadBuilt?.Invoke(cell, roadId);
            return true;
        }
        return false;
    }

    // ============ Query Methods ============

    /// <summary>
    /// 获取指定区域状态的所有格子
    /// </summary>
    public List<WorldMapCellData> GetCellsByZoneState(WorldMapCellData.ZoneState state)
    {
        var result = new List<WorldMapCellData>();
        foreach (var kvp in _cellDataMap)
        {
            if (kvp.Value.zoneState == state)
            {
                result.Add(kvp.Value);
            }
        }
        return result;
    }

    /// <summary>
    /// 获取指定占用类型的所有格子
    /// </summary>
    public List<WorldMapCellData> GetCellsByOccupation(WorldMapCellData.OccupationType occupation)
    {
        var result = new List<WorldMapCellData>();
        foreach (var kvp in _cellDataMap)
        {
            if (kvp.Value.occupation == occupation)
            {
                result.Add(kvp.Value);
            }
        }
        return result;
    }

    /// <summary>
    /// 获取指定资源区类型的所有格子
    /// </summary>
    public List<WorldMapCellData> GetCellsByResourceZone(string zoneTypeId)
    {
        var result = new List<WorldMapCellData>();
        foreach (var kvp in _cellDataMap)
        {
            if (kvp.Value.resourceZoneTypeId == zoneTypeId)
            {
                result.Add(kvp.Value);
            }
        }
        return result;
    }

    /// <summary>
    /// 获取指定NPC势力的所有格子
    /// </summary>
    public List<WorldMapCellData> GetCellsByNPCFaction(string factionId)
    {
        var result = new List<WorldMapCellData>();
        foreach (var kvp in _cellDataMap)
        {
            if (kvp.Value.npcFactionId == factionId)
            {
                result.Add(kvp.Value);
            }
        }
        return result;
    }

    /// <summary>
    /// 获取所有未清除的威胁区域
    /// </summary>
    public List<WorldMapCellData> GetActiveThreatZones()
    {
        var result = new List<WorldMapCellData>();
        foreach (var kvp in _cellDataMap)
        {
            if (kvp.Value.zoneState == WorldMapCellData.ZoneState.Threat && !kvp.Value.threatCleared)
            {
                result.Add(kvp.Value);
            }
        }
        return result;
    }

    // ============ Utility Methods ============

    /// <summary>
    /// 计算路径上的总货物损失率
    /// </summary>
    public float CalculatePathCargoLoss(List<Vector2Int> path)
    {
        float totalLoss = 0f;
        foreach (var cell in path)
        {
            var data = GetCellData(cell);
            if (data != null)
            {
                totalLoss += data.GetCargoLossRate();
            }
        }
        // 损失率不能超过100%
        return Mathf.Min(totalLoss, 1f);
    }

    /// <summary>
    /// 计算路径上的道路建造成本倍率
    /// </summary>
    public float CalculatePathBuildCostMultiplier(List<Vector2Int> path)
    {
        float totalMultiplier = 0f;
        foreach (var cell in path)
        {
            var data = GetOrCreateCellData(cell);
            if (data != null)
            {
                totalMultiplier += data.GetRoadBuildCostMultiplier();
            }
        }
        return totalMultiplier;
    }

    // ============ Save/Load ============

    /// <summary>
    /// 获取所有格子数据用于保存
    /// </summary>
    public List<WorldMapCellData> GetAllCellDataForSave()
    {
        var result = new List<WorldMapCellData>();
        foreach (var kvp in _cellDataMap)
        {
            result.Add(kvp.Value.Clone());
        }
        return result;
    }

    /// <summary>
    /// 从保存数据加载
    /// </summary>
    public void LoadFromSaveData(List<WorldMapCellData> savedData)
    {
        _cellDataMap.Clear();
        if (savedData == null) return;

        foreach (var data in savedData)
        {
            _cellDataMap[data.cell] = data;
        }
    }

    // ============ Visualization ============
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showGrid) return;

        // Draw grid lines
        Gizmos.color = gridColor;
        for (int x = 0; x <= width; x++)
        {
            Vector3 start = origin + new Vector3(x * cellSize, 0, 0);
            Vector3 end = origin + new Vector3(x * cellSize, 0, height * cellSize);
            Gizmos.DrawLine(start, end);
        }
        for (int z = 0; z <= height; z++)
        {
            Vector3 start = origin + new Vector3(0, 0, z * cellSize);
            Vector3 end = origin + new Vector3(width * cellSize, 0, z * cellSize);
            Gizmos.DrawLine(start, end);
        }

        // Draw cell data
        foreach (var kvp in _cellDataMap)
        {
            var data = kvp.Value;
            Vector3 center = CellToWorldCenter(data.cell);
            Vector3 size = new Vector3(cellSize * 0.9f, 0.1f, cellSize * 0.9f);

            // 底层：资源区
            if (showResourceZones && data.HasResourceZone)
            {
                var zoneType = GetResourceZoneType(data.resourceZoneTypeId);
                if (zoneType != null)
                {
                    Gizmos.color = zoneType.mapColor;
                    Gizmos.DrawCube(center + Vector3.down * 0.05f, size);
                }
            }

            // 中层：区域状态
            if (showThreatZones)
            {
                Color stateColor = data.zoneState switch
                {
                    WorldMapCellData.ZoneState.Threat => data.threatCleared ? clearedThreatColor : threatColor,
                    WorldMapCellData.ZoneState.Unbuildable => unbuildableColor,
                    WorldMapCellData.ZoneState.NPCTerritory => npcTerritoryColor,
                    _ => Color.clear
                };

                if (stateColor != Color.clear)
                {
                    Gizmos.color = stateColor;
                    Gizmos.DrawCube(center, size);
                }
            }

            // 顶层：占用（由其他系统渲染）
        }
    }
#endif

    // ============ Debug ============
#if UNITY_EDITOR
    [ContextMenu("Debug: Print Statistics")]
    private void DebugPrintStatistics()
    {
        int totalCells = _cellDataMap.Count;
        int resourceZones = 0;
        int threats = 0;
        int unbuildable = 0;
        int bases = 0;
        int roads = 0;

        foreach (var kvp in _cellDataMap)
        {
            var data = kvp.Value;
            if (data.HasResourceZone) resourceZones++;
            if (data.zoneState == WorldMapCellData.ZoneState.Threat) threats++;
            if (data.zoneState == WorldMapCellData.ZoneState.Unbuildable) unbuildable++;
            if (data.occupation == WorldMapCellData.OccupationType.Base) bases++;
            if (data.occupation == WorldMapCellData.OccupationType.Road) roads++;
        }

        Debug.Log($"[WorldMapManager] Statistics:\n" +
                  $"  Total cells with data: {totalCells}\n" +
                  $"  Resource zones: {resourceZones}\n" +
                  $"  Threat zones: {threats}\n" +
                  $"  Unbuildable: {unbuildable}\n" +
                  $"  Bases: {bases}\n" +
                  $"  Roads: {roads}");
    }

    [ContextMenu("Debug: Generate Test Data")]
    private void DebugGenerateTestData()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Only works in play mode");
            return;
        }

        // 生成一些测试资源区
        if (resourceZoneTypes.Count > 0)
        {
            SetResourceZoneArea(new Vector2Int(10, 10), new Vector2Int(5, 5), resourceZoneTypes[0].zoneId);
        }

        // 生成一些威胁区域
        SetThreatZoneArea(new Vector2Int(20, 20), new Vector2Int(3, 3), 3);

        // 生成不可建造区域
        SetUnbuildableArea(new Vector2Int(30, 30), new Vector2Int(4, 4));

        Debug.Log("[WorldMapManager] Test data generated");
    }
#endif
}
