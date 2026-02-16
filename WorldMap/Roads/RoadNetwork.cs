using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 道路网络管理器 - 管理整个大地图的道路系统
/// </summary>
public class RoadNetwork : MonoBehaviour
{
    public static RoadNetwork Instance { get; private set; }

    [Header("Road Types")]
    [Tooltip("所有可用的道路类型")]
    public List<RoadType> roadTypes = new();

    [Header("References")]
    public WorldMapManager worldMapManager;

    [Header("Visualization")]
    public bool showRoads = true;
    public float roadHeight = 0.1f;

    // 数据存储
    private Dictionary<Vector2Int, RoadSegment> _roadMap = new();
    private Dictionary<string, RoadType> _roadTypeCache = new();

    // 事件
    public event Action<Vector2Int, RoadSegment> OnRoadBuilt;
    public event Action<Vector2Int> OnRoadRemoved;
    public event Action<Vector2Int, RoadSegment> OnRoadUpgraded;
    public event Action<Vector2Int, RoadSegment> OnRoadDamaged;   // 道路损坏等级变化
    public event Action<Vector2Int, RoadSegment> OnRoadRepaired;  // 道路修复
    public event Action<Vector2Int> OnRoadBroken;                 // 道路完全损坏（不可通行）

    /// <summary>
    /// 外部通知道路升级（用于UI等外部升级操作）
    /// </summary>
    public void NotifyRoadUpgraded(Vector2Int cell, RoadSegment segment)
    {
        OnRoadUpgraded?.Invoke(cell, segment);
    }

    // ============ Lifecycle ============

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        CacheRoadTypes();

        if (worldMapManager == null)
        {
            worldMapManager = FindObjectOfType<WorldMapManager>();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void CacheRoadTypes()
    {
        _roadTypeCache.Clear();
        foreach (var roadType in roadTypes)
        {
            if (roadType != null && !string.IsNullOrEmpty(roadType.roadTypeId))
            {
                _roadTypeCache[roadType.roadTypeId] = roadType;
            }
        }
    }

    // ============ Road Type Access ============

    /// <summary>
    /// 获取道路类型
    /// </summary>
    public RoadType GetRoadType(string roadTypeId)
    {
        if (string.IsNullOrEmpty(roadTypeId)) return null;
        _roadTypeCache.TryGetValue(roadTypeId, out var roadType);
        return roadType;
    }

    /// <summary>
    /// 获取默认道路类型（最低等级）
    /// </summary>
    public RoadType GetDefaultRoadType()
    {
        RoadType lowest = null;
        foreach (var roadType in roadTypes)
        {
            if (lowest == null || roadType.level < lowest.level)
            {
                lowest = roadType;
            }
        }
        return lowest;
    }

    // ============ Road Segment Access ============

    /// <summary>
    /// 获取指定位置的道路段
    /// </summary>
    public RoadSegment GetRoadAt(Vector2Int cell)
    {
        _roadMap.TryGetValue(cell, out var segment);
        return segment;
    }

    /// <summary>
    /// 检查指定位置是否有道路
    /// </summary>
    public bool HasRoadAt(Vector2Int cell)
    {
        return _roadMap.ContainsKey(cell);
    }

    // ============ Road Building ============

    /// <summary>
    /// 检查是否可以在指定位置建造道路
    /// </summary>
    public bool CanBuildRoadAt(Vector2Int cell)
    {
        // 已有道路
        if (HasRoadAt(cell)) return false;

        // 检查WorldMapManager
        if (worldMapManager != null)
        {
            return worldMapManager.CanBuildRoadAt(cell);
        }

        return true;
    }

    /// <summary>
    /// 在指定位置建造道路
    /// </summary>
    public bool TryBuildRoad(Vector2Int cell, string roadTypeId)
    {
        if (!CanBuildRoadAt(cell)) return false;

        var roadType = GetRoadType(roadTypeId);
        if (roadType == null)
        {
            Debug.LogWarning($"[RoadNetwork] Road type not found: {roadTypeId}");
            return false;
        }

        // 创建道路段（生成唯一ID）
        string networkId = $"road_{cell.x}_{cell.y}_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
        var segment = new RoadSegment(cell, roadTypeId, networkId);

        // 先在 WorldMapManager 中标记占用（单一数据源）
        if (worldMapManager != null)
        {
            if (!worldMapManager.TryBuildRoad(cell, segment.roadNetworkId))
            {
                Debug.LogWarning($"[RoadNetwork] WorldMapManager rejected road at {cell}");
                return false;
            }
        }

        // WorldMapManager 通过后，再加入本地追踪
        UpdateConnections(cell, segment);
        _roadMap[cell] = segment;

        OnRoadBuilt?.Invoke(cell, segment);
        return true;
    }

    /// <summary>
    /// 建造一条道路路径
    /// </summary>
    public bool TryBuildRoadPath(List<Vector2Int> path, string roadTypeId)
    {
        if (path == null || path.Count == 0) return false;

        // 预检查所有格子是否可建造
        foreach (var cell in path)
        {
            if (!CanBuildRoadAt(cell))
            {
                Debug.LogWarning($"[RoadNetwork] Cannot build road at {cell}");
                return false;
            }
        }

        // 逐格建造，带回滚支持
        string networkId = Guid.NewGuid().ToString();
        var builtCells = new List<Vector2Int>();

        foreach (var cell in path)
        {
            var segment = new RoadSegment(cell, roadTypeId, networkId);

            // 先在 WorldMapManager 中标记占用
            if (worldMapManager != null)
            {
                if (!worldMapManager.TryBuildRoad(cell, networkId))
                {
                    Debug.LogWarning($"[RoadNetwork] WorldMapManager rejected road at {cell}, rolling back {builtCells.Count} cells");
                    // 回滚已建好的格子
                    for (int i = builtCells.Count - 1; i >= 0; i--)
                        TryRemoveRoad(builtCells[i]);
                    return false;
                }
            }

            // WorldMapManager 通过后，加入本地追踪
            UpdateConnections(cell, segment);
            _roadMap[cell] = segment;
            builtCells.Add(cell);

            OnRoadBuilt?.Invoke(cell, segment);
        }

        return true;
    }

    /// <summary>
    /// 更新道路连接
    /// </summary>
    private void UpdateConnections(Vector2Int cell, RoadSegment segment)
    {
        var adjacent = RoadSegment.GetAdjacentCells(cell);

        foreach (var adjCell in adjacent)
        {
            if (_roadMap.TryGetValue(adjCell, out var adjSegment))
            {
                // 计算方向
                var dirToAdj = RoadSegment.GetDirectionTo(cell, adjCell);
                var dirFromAdj = RoadSegment.GetOppositeDirection(dirToAdj);

                // 添加双向连接
                segment.AddConnection(dirToAdj);
                adjSegment.AddConnection(dirFromAdj);
            }
        }
    }

    // ============ Road Removal ============

    /// <summary>
    /// 移除指定位置的道路
    /// </summary>
    public bool TryRemoveRoad(Vector2Int cell)
    {
        if (!_roadMap.TryGetValue(cell, out var segment))
            return false;

        // 移除与相邻道路的连接
        var adjacent = RoadSegment.GetAdjacentCells(cell);
        foreach (var adjCell in adjacent)
        {
            if (_roadMap.TryGetValue(adjCell, out var adjSegment))
            {
                var dirFromAdj = RoadSegment.GetDirectionTo(adjCell, cell);
                adjSegment.RemoveConnection(dirFromAdj);
            }
        }

        // 从地图移除
        _roadMap.Remove(cell);

        // 同步清理 WorldMapManager 中的占用状态
        if (worldMapManager != null)
        {
            var cellData = worldMapManager.GetCellData(cell);
            if (cellData != null && cellData.occupation == WorldMapCellData.OccupationType.Road)
            {
                cellData.ClearOccupation();
            }
        }

        OnRoadRemoved?.Invoke(cell);
        return true;
    }

    // ============ Road Upgrade ============

    /// <summary>
    /// 检查是否可以升级道路
    /// </summary>
    public bool CanUpgradeRoad(Vector2Int cell)
    {
        if (!_roadMap.TryGetValue(cell, out var segment))
            return false;

        var roadType = GetRoadType(segment.roadTypeId);
        return roadType != null && roadType.CanUpgrade;
    }

    /// <summary>
    /// 升级道路
    /// </summary>
    public bool TryUpgradeRoad(Vector2Int cell)
    {
        if (!_roadMap.TryGetValue(cell, out var segment))
            return false;

        var roadType = GetRoadType(segment.roadTypeId);
        if (roadType == null || !roadType.CanUpgrade)
            return false;

        // 更新道路类型
        segment.roadTypeId = roadType.upgradeTo.roadTypeId;

        OnRoadUpgraded?.Invoke(cell, segment);
        return true;
    }

    /// <summary>
    /// 升级整条路径
    /// </summary>
    public int UpgradeRoadPath(List<Vector2Int> path)
    {
        int upgraded = 0;
        foreach (var cell in path)
        {
            if (TryUpgradeRoad(cell))
                upgraded++;
        }
        return upgraded;
    }

    // ============ Path Finding ============

    /// <summary>
    /// 查找两点之间的道路路径（A*算法）
    /// 注意：此方法直接检查相邻格子是否有道路，不依赖预先建立的连接
    /// </summary>
    public List<Vector2Int> FindPath(Vector2Int start, Vector2Int end)
    {
        if (!HasRoadAt(start) || !HasRoadAt(end))
            return null;

        var openSet = new List<Vector2Int> { start };
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var gScore = new Dictionary<Vector2Int, float> { [start] = 0 };
        var fScore = new Dictionary<Vector2Int, float> { [start] = Heuristic(start, end) };

        while (openSet.Count > 0)
        {
            // 找到fScore最小的节点
            Vector2Int current = openSet[0];
            float currentFScore = fScore.GetValueOrDefault(current, float.MaxValue);
            for (int i = 1; i < openSet.Count; i++)
            {
                float f = fScore.GetValueOrDefault(openSet[i], float.MaxValue);
                if (f < currentFScore)
                {
                    current = openSet[i];
                    currentFScore = f;
                }
            }

            if (current == end)
            {
                return ReconstructPath(cameFrom, current);
            }

            openSet.Remove(current);

            // 遍历所有相邻格子（直接检查是否有道路，不依赖预先建立的连接）
            var adjacentCells = RoadSegment.GetAdjacentCells(current);
            foreach (var neighbor in adjacentCells)
            {
                // 直接检查相邻格子是否有道路
                if (!HasRoadAt(neighbor)) continue;

                // 计算移动成本（考虑道路类型）
                var neighborSegment = GetRoadAt(neighbor);
                var neighborRoadType = GetRoadType(neighborSegment.roadTypeId);
                float moveCost = neighborRoadType != null ? 1f / neighborRoadType.speedMultiplier : 1f;

                float tentativeGScore = gScore.GetValueOrDefault(current, float.MaxValue) + moveCost;

                if (tentativeGScore < gScore.GetValueOrDefault(neighbor, float.MaxValue))
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;
                    fScore[neighbor] = tentativeGScore + Heuristic(neighbor, end);

                    if (!openSet.Contains(neighbor))
                    {
                        openSet.Add(neighbor);
                    }
                }
            }
        }

        return null; // 未找到路径
    }

    private float Heuristic(Vector2Int a, Vector2Int b)
    {
        // 曼哈顿距离
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
    {
        var path = new List<Vector2Int> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Insert(0, current);
        }
        return path;
    }

    /// <summary>
    /// 检查两点之间是否有连通的道路
    /// </summary>
    public bool IsConnected(Vector2Int a, Vector2Int b)
    {
        return FindPath(a, b) != null;
    }

    /// <summary>
    /// 查找从起点到目标区域周围的道路路径
    /// 用于连接到NPC据点（据点本身不能建造道路，所以检查周围一圈）
    /// </summary>
    /// <param name="start">起点坐标</param>
    /// <param name="targetCenter">目标中心坐标</param>
    /// <param name="targetSize">目标占地大小（如2x2）</param>
    /// <returns>路径，如果找到则返回路径列表，否则返回null</returns>
    public List<Vector2Int> FindPathToArea(Vector2Int start, Vector2Int targetCenter, Vector2Int targetSize)
    {
        return FindPathBetweenAreas(start, Vector2Int.one, targetCenter, targetSize);
    }

    /// <summary>
    /// 查找两个区域之间的道路路径（起点和终点都检查周围一圈）
    /// 用于连接玩家基地和NPC据点（两者所在格子都不能建造道路）
    /// </summary>
    /// <param name="startCenter">起点中心坐标</param>
    /// <param name="startSize">起点占地大小</param>
    /// <param name="targetCenter">目标中心坐标</param>
    /// <param name="targetSize">目标占地大小</param>
    /// <returns>路径，如果找到则返回路径列表，否则返回null</returns>
    public List<Vector2Int> FindPathBetweenAreas(Vector2Int startCenter, Vector2Int startSize,
                                                   Vector2Int targetCenter, Vector2Int targetSize)
    {
        // 获取起点区域周围一圈有道路的格子
        // 注意：始终检查周围一圈，因为基地/据点的中心区域不能建造道路
        List<Vector2Int> validStartPoints = new List<Vector2Int>();

        // 检查起点周围一圈
        var startSurrounding = GetSurroundingCells(startCenter, startSize);
        foreach (var cell in startSurrounding)
        {
            if (HasRoadAt(cell))
            {
                validStartPoints.Add(cell);
            }
        }

        if (validStartPoints.Count == 0)
        {
            return null; // 起点周围没有道路
        }

        // 获取目标区域周围一圈有道路的格子
        List<Vector2Int> validEndpoints = new List<Vector2Int>();

        // 检查目标周围一圈
        var targetSurrounding = GetSurroundingCells(targetCenter, targetSize);
        foreach (var cell in targetSurrounding)
        {
            if (HasRoadAt(cell))
            {
                validEndpoints.Add(cell);
            }
        }

        if (validEndpoints.Count == 0)
        {
            return null; // 目标周围没有道路
        }

        // 尝试找到任意起点到任意终点的最短路径
        List<Vector2Int> bestPath = null;
        float bestCost = float.MaxValue;

        foreach (var startPoint in validStartPoints)
        {
            foreach (var endpoint in validEndpoints)
            {
                var path = FindPath(startPoint, endpoint);
                if (path != null && path.Count < bestCost)
                {
                    bestPath = path;
                    bestCost = path.Count;
                }
            }
        }

        return bestPath;
    }

    /// <summary>
    /// 查找两个区域之间的道路路径（使用anchor左下角坐标）
    /// 与WorldMapGrid.TryOccupyArea的逻辑一致
    /// </summary>
    /// <param name="startAnchor">起点左下角坐标</param>
    /// <param name="startSize">起点占地大小</param>
    /// <param name="targetAnchor">目标左下角坐标</param>
    /// <param name="targetSize">目标占地大小</param>
    /// <returns>路径，如果找到则返回路径列表，否则返回null</returns>
    public List<Vector2Int> FindPathBetweenAreasFromAnchor(Vector2Int startAnchor, Vector2Int startSize,
                                                            Vector2Int targetAnchor, Vector2Int targetSize)
    {
        // 获取起点区域周围一圈有道路的格子
        List<Vector2Int> validStartPoints = GetRoadCellsAroundAreaFromAnchor(startAnchor, startSize);

        if (validStartPoints.Count == 0)
            return null;

        // 获取目标区域周围一圈有道路的格子
        List<Vector2Int> validEndpoints = GetRoadCellsAroundAreaFromAnchor(targetAnchor, targetSize);

        if (validEndpoints.Count == 0)
            return null;

        // 尝试找到任意起点到任意终点的最短路径
        List<Vector2Int> bestPath = null;
        float bestCost = float.MaxValue;

        foreach (var startPoint in validStartPoints)
        {
            foreach (var endpoint in validEndpoints)
            {
                var path = FindPath(startPoint, endpoint);
                if (path != null && path.Count < bestCost)
                {
                    bestPath = path;
                    bestCost = path.Count;
                }
            }
        }

        return bestPath;
    }

    /// <summary>
    /// 检查从起点是否能连接到目标区域周围
    /// </summary>
    public bool IsConnectedToArea(Vector2Int start, Vector2Int targetCenter, Vector2Int targetSize)
    {
        return FindPathToArea(start, targetCenter, targetSize) != null;
    }

    /// <summary>
    /// 检查两个区域之间是否有道路连接（以中心为基准）
    /// </summary>
    public bool IsConnectedBetweenAreas(Vector2Int startCenter, Vector2Int startSize,
                                         Vector2Int targetCenter, Vector2Int targetSize)
    {
        return FindPathBetweenAreas(startCenter, startSize, targetCenter, targetSize) != null;
    }

    /// <summary>
    /// 检查两个区域之间是否有道路连接（以anchor左下角为基准）
    /// </summary>
    public bool IsConnectedBetweenAreasFromAnchor(Vector2Int startAnchor, Vector2Int startSize,
                                                    Vector2Int targetAnchor, Vector2Int targetSize)
    {
        return FindPathBetweenAreasFromAnchor(startAnchor, startSize, targetAnchor, targetSize) != null;
    }

    /// <summary>
    /// 获取目标区域周围一圈的格子坐标（以中心为基准）
    /// </summary>
    /// <param name="center">区域中心</param>
    /// <param name="size">区域大小（如1x1, 2x2）</param>
    /// <returns>周围一圈的格子列表</returns>
    public static List<Vector2Int> GetSurroundingCells(Vector2Int center, Vector2Int size)
    {
        // 计算区域的边界
        int halfW = (size.x - 1) / 2;
        int halfH = (size.y - 1) / 2;
        int areaMinX = center.x - halfW;
        int areaMinY = center.y - halfH;
        int areaMaxX = center.x + halfW + (size.x % 2 == 0 ? 1 : 0);
        int areaMaxY = center.y + halfH + (size.y % 2 == 0 ? 1 : 0);

        return GetSurroundingCellsFromBounds(areaMinX, areaMinY, areaMaxX, areaMaxY);
    }

    /// <summary>
    /// 获取目标区域周围一圈的格子坐标（以左下角anchor为基准）
    /// 与WorldMapGrid.TryOccupyArea的逻辑一致：anchor是左下角，占据 anchor 到 anchor+size-1 的区域
    /// </summary>
    /// <param name="anchor">区域左下角坐标</param>
    /// <param name="size">区域大小（如3x3）</param>
    /// <returns>周围一圈的格子列表</returns>
    public static List<Vector2Int> GetSurroundingCellsFromAnchor(Vector2Int anchor, Vector2Int size)
    {
        // anchor模式：区域占据 anchor.x ~ anchor.x+size.x-1, anchor.y ~ anchor.y+size.y-1
        int areaMinX = anchor.x;
        int areaMinY = anchor.y;
        int areaMaxX = anchor.x + size.x - 1;
        int areaMaxY = anchor.y + size.y - 1;

        return GetSurroundingCellsFromBounds(areaMinX, areaMinY, areaMaxX, areaMaxY);
    }

    /// <summary>
    /// 根据区域边界计算周围一圈格子（内部方法）
    /// </summary>
    private static List<Vector2Int> GetSurroundingCellsFromBounds(int areaMinX, int areaMinY, int areaMaxX, int areaMaxY)
    {
        var result = new List<Vector2Int>();

        // 周围一圈 = 区域边界向外扩展1格
        int minX = areaMinX - 1;
        int maxX = areaMaxX + 1;
        int minY = areaMinY - 1;
        int maxY = areaMaxY + 1;

        // 上边和下边
        for (int x = minX; x <= maxX; x++)
        {
            result.Add(new Vector2Int(x, minY)); // 下边
            result.Add(new Vector2Int(x, maxY)); // 上边
        }

        // 左边和右边（不含角落，已在上下边添加）
        for (int y = minY + 1; y < maxY; y++)
        {
            result.Add(new Vector2Int(minX, y)); // 左边
            result.Add(new Vector2Int(maxX, y)); // 右边
        }

        return result;
    }

    /// <summary>
    /// 获取目标区域周围一圈中有道路的格子（以中心为基准）
    /// </summary>
    public List<Vector2Int> GetRoadCellsAroundArea(Vector2Int center, Vector2Int size)
    {
        var surrounding = GetSurroundingCells(center, size);
        return FilterRoadCells(surrounding);
    }

    /// <summary>
    /// 获取目标区域周围一圈中有道路的格子（以anchor左下角为基准）
    /// </summary>
    public List<Vector2Int> GetRoadCellsAroundAreaFromAnchor(Vector2Int anchor, Vector2Int size)
    {
        var surrounding = GetSurroundingCellsFromAnchor(anchor, size);
        return FilterRoadCells(surrounding);
    }

    private List<Vector2Int> FilterRoadCells(List<Vector2Int> cells)
    {
        var result = new List<Vector2Int>();
        foreach (var cell in cells)
        {
            if (HasRoadAt(cell))
            {
                result.Add(cell);
            }
        }
        return result;
    }

    // ============ Query Methods ============

    /// <summary>
    /// 获取所有道路段
    /// </summary>
    public List<RoadSegment> GetAllRoads()
    {
        return new List<RoadSegment>(_roadMap.Values);
    }

    /// <summary>
    /// 获取指定网络ID的所有道路段
    /// </summary>
    public List<RoadSegment> GetRoadsByNetwork(string networkId)
    {
        var result = new List<RoadSegment>();
        foreach (var segment in _roadMap.Values)
        {
            if (segment.roadNetworkId == networkId)
            {
                result.Add(segment);
            }
        }
        return result;
    }

    /// <summary>
    /// 获取连接到指定基地的道路
    /// </summary>
    public List<RoadSegment> GetRoadsConnectedToBase(Vector2Int baseCell)
    {
        var result = new List<RoadSegment>();
        var adjacent = RoadSegment.GetAdjacentCells(baseCell);

        foreach (var adjCell in adjacent)
        {
            if (_roadMap.TryGetValue(adjCell, out var segment))
            {
                result.Add(segment);
            }
        }
        return result;
    }

    // ============ Cost Calculation ============

    /// <summary>
    /// 计算路径的建造总成本
    /// </summary>
    public float CalculatePathBuildCost(List<Vector2Int> path, string roadTypeId)
    {
        var roadType = GetRoadType(roadTypeId);
        if (roadType == null) return float.MaxValue;

        float totalCost = 0f;
        foreach (var cell in path)
        {
            float terrainMultiplier = 1f;
            if (worldMapManager != null)
            {
                var cellData = worldMapManager.GetOrCreateCellData(cell);
                terrainMultiplier = cellData.GetRoadBuildCostMultiplier();
            }
            totalCost += roadType.CalculateTotalMoneyCost(terrainMultiplier);
        }
        return totalCost;
    }

    /// <summary>
    /// 计算路径的运输时间（考虑道路等级）
    /// </summary>
    public float CalculatePathTravelTime(List<Vector2Int> path, float baseTimePerCell = 1f)
    {
        float totalTime = 0f;
        foreach (var cell in path)
        {
            var segment = GetRoadAt(cell);
            if (segment != null)
            {
                var roadType = GetRoadType(segment.roadTypeId);
                float speedMult = roadType != null ? roadType.speedMultiplier : 1f;
                totalTime += baseTimePerCell / speedMult;
            }
            else
            {
                totalTime += baseTimePerCell;
            }
        }
        return totalTime;
    }

    // ============ Durability Management ============

    /// <summary>
    /// 运输完成后更新路径上所有道路的磨损
    /// </summary>
    /// <param name="path">运输路径</param>
    /// <param name="totalCargo">运输的货物总量</param>
    /// <returns>是否所有道路仍然可通行</returns>
    public bool ApplyTransportWearToPath(List<Vector2Int> path, int totalCargo)
    {
        if (path == null || path.Count == 0 || totalCargo <= 0) return true;

        bool allPassable = true;

        foreach (var cell in path)
        {
            if (!_roadMap.TryGetValue(cell, out var segment)) continue;

            var roadType = GetRoadType(segment.roadTypeId);
            if (roadType == null) continue;

            var oldDamageLevel = segment.GetDamageLevel();
            bool stillPassable = segment.ApplyTransportWear(totalCargo, roadType);
            var newDamageLevel = segment.GetDamageLevel();

            // 检查是否损坏等级变化
            if (newDamageLevel != oldDamageLevel)
            {
                OnRoadDamaged?.Invoke(cell, segment);

                if (newDamageLevel == RoadSegment.DamageLevel.Broken)
                {
                    OnRoadBroken?.Invoke(cell);
                    allPassable = false;
                }
            }

            if (!stillPassable) allPassable = false;
        }

        return allPassable;
    }

    /// <summary>
    /// 修复单个格子的道路
    /// </summary>
    /// <param name="cell">道路格子</param>
    /// <param name="repairPercent">修复的耐久度百分比（如果为0则完全修复）</param>
    /// <returns>是否修复成功</returns>
    public bool TryRepairRoad(Vector2Int cell, float repairPercent = 0f)
    {
        if (!_roadMap.TryGetValue(cell, out var segment)) return false;

        if (repairPercent <= 0f)
        {
            segment.FullRepair();
        }
        else
        {
            segment.Repair(repairPercent);
        }

        OnRoadRepaired?.Invoke(cell, segment);
        return true;
    }

    /// <summary>
    /// 批量修复道路
    /// </summary>
    /// <param name="cells">要修复的格子列表</param>
    /// <param name="repairPercent">修复的耐久度百分比（如果为0则完全修复）</param>
    /// <returns>成功修复的数量</returns>
    public int RepairRoadPath(List<Vector2Int> cells, float repairPercent = 0f)
    {
        if (cells == null) return 0;

        int repaired = 0;
        foreach (var cell in cells)
        {
            if (TryRepairRoad(cell, repairPercent))
                repaired++;
        }
        return repaired;
    }

    /// <summary>
    /// 获取路径的最低耐久度
    /// </summary>
    public float GetPathMinDurability(List<Vector2Int> path)
    {
        if (path == null || path.Count == 0) return 0f;

        float minDurability = 100f;
        foreach (var cell in path)
        {
            if (_roadMap.TryGetValue(cell, out var segment))
            {
                if (segment.durability < minDurability)
                    minDurability = segment.durability;
            }
        }
        return minDurability;
    }

    /// <summary>
    /// 获取路径的平均耐久度
    /// </summary>
    public float GetPathAverageDurability(List<Vector2Int> path)
    {
        if (path == null || path.Count == 0) return 0f;

        float total = 0f;
        int count = 0;
        foreach (var cell in path)
        {
            if (_roadMap.TryGetValue(cell, out var segment))
            {
                total += segment.durability;
                count++;
            }
        }
        return count > 0 ? total / count : 0f;
    }

    /// <summary>
    /// 检查路径是否全部可通行
    /// </summary>
    public bool IsPathPassable(List<Vector2Int> path)
    {
        if (path == null || path.Count == 0) return false;

        foreach (var cell in path)
        {
            if (!_roadMap.TryGetValue(cell, out var segment))
                return false; // 没有道路

            if (!segment.IsPassable)
                return false; // 道路损坏
        }
        return true;
    }

    /// <summary>
    /// 获取路径中损坏的道路数量
    /// </summary>
    public int GetPathDamagedCount(List<Vector2Int> path)
    {
        if (path == null) return 0;

        int count = 0;
        foreach (var cell in path)
        {
            if (_roadMap.TryGetValue(cell, out var segment))
            {
                if (segment.GetDamageLevel() != RoadSegment.DamageLevel.Normal)
                    count++;
            }
        }
        return count;
    }

    /// <summary>
    /// 获取路径中完全损坏的道路数量
    /// </summary>
    public int GetPathBrokenCount(List<Vector2Int> path)
    {
        if (path == null) return 0;

        int count = 0;
        foreach (var cell in path)
        {
            if (_roadMap.TryGetValue(cell, out var segment))
            {
                if (segment.GetDamageLevel() == RoadSegment.DamageLevel.Broken)
                    count++;
            }
        }
        return count;
    }

    /// <summary>
    /// 计算修复路径上所有道路的总成本
    /// </summary>
    public float CalculatePathRepairCost(List<Vector2Int> path)
    {
        if (path == null) return 0f;

        float totalCost = 0f;
        foreach (var cell in path)
        {
            if (_roadMap.TryGetValue(cell, out var segment))
            {
                var roadType = GetRoadType(segment.roadTypeId);
                if (roadType != null)
                {
                    totalCost += segment.CalculateRepairCost(roadType);
                }
            }
        }
        return totalCost;
    }

    /// <summary>
    /// 计算考虑损坏的运输时间
    /// </summary>
    public float CalculatePathTravelTimeWithDamage(List<Vector2Int> path, float baseTimePerCell = 1f)
    {
        if (path == null || path.Count == 0) return 0f;

        float totalTime = 0f;
        foreach (var cell in path)
        {
            var segment = GetRoadAt(cell);
            if (segment != null)
            {
                var roadType = GetRoadType(segment.roadTypeId);
                float speedMult = roadType != null ? roadType.speedMultiplier : 1f;
                float damageMult = segment.GetDamageTimeMultiplier();

                // 如果道路完全损坏，返回无穷大
                if (damageMult >= float.MaxValue) return float.MaxValue;

                totalTime += (baseTimePerCell / speedMult) * damageMult;
            }
            else
            {
                totalTime += baseTimePerCell;
            }
        }
        return totalTime;
    }

    // ============ Save/Load ============

    public List<RoadSegment> GetAllRoadsForSave()
    {
        var result = new List<RoadSegment>();
        foreach (var segment in _roadMap.Values)
        {
            result.Add(segment.Clone());
        }
        return result;
    }

    public void LoadFromSaveData(List<RoadSegment> savedData)
    {
        _roadMap.Clear();
        if (savedData == null) return;

        foreach (var segment in savedData)
        {
            _roadMap[segment.cell] = segment;
        }
    }

    // ============ Visualization ============
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showRoads) return;
        if (worldMapManager == null) return;

        foreach (var kvp in _roadMap)
        {
            var segment = kvp.Value;
            var roadType = GetRoadType(segment.roadTypeId);
            Vector3 center = worldMapManager.CellToWorldCenter(segment.cell);
            center.y = roadHeight;

            // 绘制道路格子
            Gizmos.color = roadType != null ? roadType.roadColor : Color.gray;
            float size = worldMapManager.cellSize * 0.8f;
            Gizmos.DrawCube(center, new Vector3(size, 0.05f, size));

            // 绘制连接线
            Gizmos.color = Color.white;
            foreach (RoadSegment.RoadDirection dir in Enum.GetValues(typeof(RoadSegment.RoadDirection)))
            {
                if (dir == RoadSegment.RoadDirection.None) continue;
                if (!segment.HasConnection(dir)) continue;

                Vector2Int offset = RoadSegment.GetDirectionOffset(dir);
                Vector3 neighborCenter = worldMapManager.CellToWorldCenter(segment.cell + offset);
                neighborCenter.y = roadHeight;

                Gizmos.DrawLine(center, Vector3.Lerp(center, neighborCenter, 0.5f));
            }
        }
    }
#endif

    // ============ Debug ============
#if UNITY_EDITOR
    [ContextMenu("Debug: Print Road Statistics")]
    private void DebugPrintStatistics()
    {
        int total = _roadMap.Count;
        var typeCounts = new Dictionary<string, int>();

        foreach (var segment in _roadMap.Values)
        {
            if (!typeCounts.ContainsKey(segment.roadTypeId))
                typeCounts[segment.roadTypeId] = 0;
            typeCounts[segment.roadTypeId]++;
        }

        string stats = $"[RoadNetwork] Total roads: {total}\n";
        foreach (var kvp in typeCounts)
        {
            var roadType = GetRoadType(kvp.Key);
            string name = roadType != null ? roadType.displayName : kvp.Key;
            stats += $"  {name}: {kvp.Value}\n";
        }
        Debug.Log(stats);
    }
#endif
}
