using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 大地图阶段1测试器 - 用于测试区域系统、道路系统等基础功能
/// </summary>
public class WorldMapTesterPhase1 : MonoBehaviour
{
    [Header("References")]
    public WorldMapManager worldMapManager;
    public RoadNetwork roadNetwork;
    public RoadBuilder roadBuilder;

    [Header("Test Settings")]
    [Tooltip("是否在启动时生成测试数据（建议关闭，需要时手动通过右键菜单生成）")]
    public bool generateTestDataOnStart = false;

    [Header("Resource Zone Test")]
    public Vector2Int mineralZoneAnchor = new Vector2Int(5, 5);
    public Vector2Int mineralZoneSize = new Vector2Int(6, 6);

    public Vector2Int forestZoneAnchor = new Vector2Int(15, 5);
    public Vector2Int forestZoneSize = new Vector2Int(8, 5);

    public Vector2Int fertileZoneAnchor = new Vector2Int(5, 15);
    public Vector2Int fertileZoneSize = new Vector2Int(5, 8);

    public Vector2Int waterZoneAnchor = new Vector2Int(15, 15);
    public Vector2Int waterZoneSize = new Vector2Int(4, 4);

    [Header("Threat Zone Test")]
    public Vector2Int threatZoneAnchor = new Vector2Int(25, 10);
    public Vector2Int threatZoneSize = new Vector2Int(5, 5);
    public int threatLevel = 3;

    [Header("Unbuildable Zone Test")]
    public Vector2Int unbuildableAnchor = new Vector2Int(30, 5);
    public Vector2Int unbuildableSize = new Vector2Int(4, 8);

    [Header("Test Road")]
    public List<Vector2Int> testRoadPath = new List<Vector2Int>
    {
        new Vector2Int(10, 10),
        new Vector2Int(11, 10),
        new Vector2Int(12, 10),
        new Vector2Int(13, 10),
        new Vector2Int(14, 10),
        new Vector2Int(15, 10)
    };

    // ============ Lifecycle ============

    private void Awake()
    {
        if (worldMapManager == null)
            worldMapManager = FindObjectOfType<WorldMapManager>();

        if (roadNetwork == null)
            roadNetwork = FindObjectOfType<RoadNetwork>();

        if (roadBuilder == null)
            roadBuilder = FindObjectOfType<RoadBuilder>();
    }

    private void Start()
    {
        // 优先从存档恢复大地图数据（道路、NPC据点、格子状态）
        bool restoredFromSave = TryRestoreFromSave();

        if (!restoredFromSave && generateTestDataOnStart)
        {
            GenerateTestData();
        }
    }

    /// <summary>
    /// 尝试从存档恢复大地图数据，返回是否成功恢复
    /// </summary>
    private bool TryRestoreFromSave()
    {
        if (BaseManager.Instance == null || !BaseManager.Instance.HasPendingWorldMapData)
            return false;

        Debug.Log("[WorldMapTesterPhase1] Restoring world map data from save...");
        BaseManager.Instance.FlushPendingWorldMapData();

        // 恢复后刷新可视化
        var visualizer = FindObjectOfType<NPCOutpostVisualizer>();
        if (visualizer != null)
            visualizer.RebuildAllOutposts();

        // 通知 ResourceZoneVisualizer 也刷新
        var rzVisualizer = FindObjectOfType<ResourceZoneVisualizer>();
        if (rzVisualizer != null)
            rzVisualizer.RebuildAllZones();

        // 通知 RoadVisualizer 刷新（LoadFromSaveData 不触发事件，需要手动刷新）
        var roadVisualizer = FindObjectOfType<RoadVisualizer>();
        if (roadVisualizer != null)
            roadVisualizer.RebuildAllRoads();

        return true;
    }

    // ============ Test Data Generation ============

    [ContextMenu("Generate Test Data")]
    public void GenerateTestData()
    {
        Debug.Log("[WorldMapTesterPhase1] Generating test data...");

        GenerateResourceZones();
        GenerateThreatZones();
        GenerateUnbuildableZones();
        GenerateTestRoad();
        InitializeNPCOutposts();

        Debug.Log("[WorldMapTesterPhase1] Test data generation complete!");
    }

    private void GenerateResourceZones()
    {
        if (worldMapManager == null) return;
        if (worldMapManager.resourceZoneTypes == null || worldMapManager.resourceZoneTypes.Count < 4)
        {
            Debug.LogWarning("[WorldMapTesterPhase1] Need at least 4 resource zone types defined!");
            return;
        }

        // 矿产区
        if (worldMapManager.resourceZoneTypes.Count > 0 && worldMapManager.resourceZoneTypes[0] != null)
        {
            worldMapManager.SetResourceZoneArea(mineralZoneAnchor, mineralZoneSize,
                worldMapManager.resourceZoneTypes[0].zoneId);
            Debug.Log($"[Test] Created mineral zone at {mineralZoneAnchor}");
        }

        // 森林区
        if (worldMapManager.resourceZoneTypes.Count > 1 && worldMapManager.resourceZoneTypes[1] != null)
        {
            worldMapManager.SetResourceZoneArea(forestZoneAnchor, forestZoneSize,
                worldMapManager.resourceZoneTypes[1].zoneId);
            Debug.Log($"[Test] Created forest zone at {forestZoneAnchor}");
        }

        // 肥沃区
        if (worldMapManager.resourceZoneTypes.Count > 2 && worldMapManager.resourceZoneTypes[2] != null)
        {
            worldMapManager.SetResourceZoneArea(fertileZoneAnchor, fertileZoneSize,
                worldMapManager.resourceZoneTypes[2].zoneId);
            Debug.Log($"[Test] Created fertile zone at {fertileZoneAnchor}");
        }

        // 水源区
        if (worldMapManager.resourceZoneTypes.Count > 3 && worldMapManager.resourceZoneTypes[3] != null)
        {
            worldMapManager.SetResourceZoneArea(waterZoneAnchor, waterZoneSize,
                worldMapManager.resourceZoneTypes[3].zoneId);
            Debug.Log($"[Test] Created water zone at {waterZoneAnchor}");
        }
    }

    private void GenerateThreatZones()
    {
        if (worldMapManager == null) return;

        worldMapManager.SetThreatZoneArea(threatZoneAnchor, threatZoneSize, threatLevel);
        Debug.Log($"[Test] Created threat zone at {threatZoneAnchor} with level {threatLevel}");
    }

    private void GenerateUnbuildableZones()
    {
        if (worldMapManager == null) return;

        worldMapManager.SetUnbuildableArea(unbuildableAnchor, unbuildableSize);
        Debug.Log($"[Test] Created unbuildable zone at {unbuildableAnchor}");
    }

    private void GenerateTestRoad()
    {
        if (roadNetwork == null) return;

        var defaultType = roadNetwork.GetDefaultRoadType();
        if (defaultType == null)
        {
            Debug.LogWarning("[WorldMapTesterPhase1] No road types defined!");
            return;
        }

        bool success = roadNetwork.TryBuildRoadPath(testRoadPath, defaultType.roadTypeId);
        if (success)
        {
            Debug.Log($"[Test] Created test road with {testRoadPath.Count} segments");
        }
        else
        {
            Debug.LogWarning("[Test] Failed to create test road");
        }
    }

    private void InitializeNPCOutposts()
    {
        var npcManager = NPCManager.Instance;
        if (npcManager == null)
            npcManager = FindObjectOfType<NPCManager>();

        if (npcManager == null)
        {
            Debug.LogWarning("[WorldMapTesterPhase1] NPCManager not found, skipping NPC initialization.");
            return;
        }

        npcManager.InitializeDefaultOutposts();
    }

    // ============ Test Commands ============

    [ContextMenu("Test: Clear Threat Zone")]
    public void TestClearThreatZone()
    {
        if (worldMapManager == null) return;

        worldMapManager.ClearThreatArea(threatZoneAnchor, threatZoneSize);
        Debug.Log("[Test] Cleared threat zone");
    }

    [ContextMenu("Test: Build Road At (20,10)")]
    public void TestBuildSingleRoad()
    {
        if (roadNetwork == null) return;

        var defaultType = roadNetwork.GetDefaultRoadType();
        if (defaultType == null) return;

        Vector2Int cell = new Vector2Int(20, 10);
        bool success = roadNetwork.TryBuildRoad(cell, defaultType.roadTypeId);
        Debug.Log($"[Test] Build road at {cell}: {success}");
    }

    [ContextMenu("Test: Upgrade Test Road")]
    public void TestUpgradeRoad()
    {
        if (roadNetwork == null) return;

        int upgraded = 0;
        foreach (var cell in testRoadPath)
        {
            if (roadNetwork.TryUpgradeRoad(cell))
                upgraded++;
        }
        Debug.Log($"[Test] Upgraded {upgraded} road segments");
    }

    [ContextMenu("Test: Find Path (10,10) to (15,10)")]
    public void TestPathFinding()
    {
        if (roadNetwork == null) return;

        Vector2Int start = new Vector2Int(10, 10);
        Vector2Int end = new Vector2Int(15, 10);

        var path = roadNetwork.FindPath(start, end);
        if (path != null)
        {
            Debug.Log($"[Test] Found path: {string.Join(" -> ", path)}");
        }
        else
        {
            Debug.LogWarning("[Test] No path found!");
        }
    }

    [ContextMenu("Test: Check Cell (15,15) Info")]
    public void TestCellInfo()
    {
        if (worldMapManager == null) return;

        Vector2Int cell = new Vector2Int(15, 15);
        var data = worldMapManager.GetCellData(cell);

        if (data != null)
        {
            Debug.Log($"[Test] Cell {cell}:\n" +
                      $"  Resource Zone: {data.resourceZoneTypeId ?? "None"}\n" +
                      $"  Zone State: {data.zoneState}\n" +
                      $"  Occupation: {data.occupation}\n" +
                      $"  Can Build Base: {data.CanBuildBase}\n" +
                      $"  Can Build Road: {data.CanBuildRoad}");
        }
        else
        {
            Debug.Log($"[Test] Cell {cell}: No data (default)");
        }
    }

    [ContextMenu("Test: Print Statistics")]
    public void TestPrintStatistics()
    {
        if (worldMapManager != null)
        {
            var allCells = worldMapManager.GetAllCellDataForSave();
            int resourceZones = 0;
            int threats = 0;
            int roads = 0;

            foreach (var cell in allCells)
            {
                if (cell.HasResourceZone) resourceZones++;
                if (cell.zoneState == WorldMapCellData.ZoneState.Threat) threats++;
                if (cell.occupation == WorldMapCellData.OccupationType.Road) roads++;
            }

            Debug.Log($"[Test] WorldMap Statistics:\n" +
                      $"  Total Cells with Data: {allCells.Count}\n" +
                      $"  Resource Zones: {resourceZones}\n" +
                      $"  Threat Zones: {threats}\n" +
                      $"  Roads: {roads}");
        }

        if (roadNetwork != null)
        {
            var allRoads = roadNetwork.GetAllRoads();
            Debug.Log($"[Test] Road Network:\n" +
                      $"  Total Segments: {allRoads.Count}");
        }
    }

    // ============ Connection Verification ============

    [ContextMenu("Verify: Check All Outpost Connections")]
    public void VerifyAllOutpostConnections()
    {
        if (roadNetwork == null || worldMapManager == null)
        {
            Debug.LogError("[Verify] RoadNetwork or WorldMapManager is null!");
            return;
        }

        var npcManager = NPCManager.Instance ?? FindObjectOfType<NPCManager>();
        if (npcManager == null)
        {
            Debug.LogError("[Verify] NPCManager not found!");
            return;
        }

        // 找到玩家基地
        Vector2Int? baseCell = FindPlayerBaseCell();
        if (!baseCell.HasValue)
        {
            Debug.LogError("[Verify] No player base found on world map!");
            return;
        }

        // 获取基地在大地图上的占地大小（不是建筑场景内部的网格大小）
        Vector2Int baseSize = Vector2Int.one * 3; // 默认3x3
        if (BaseManager.Instance != null)
        {
            baseSize = BaseManager.Instance.baseGridSize;
        }

        Debug.Log($"[Verify] ===== Outpost Connection Report =====");
        Debug.Log($"[Verify] Player base at cell {baseCell.Value}, size {baseSize}");

        var outposts = npcManager.GetAllOutposts();
        if (outposts.Count == 0)
        {
            Debug.LogWarning("[Verify] No outposts exist!");
            return;
        }

        int connected = 0;
        int total = outposts.Count;

        foreach (var outpost in outposts)
        {
            var faction = npcManager.GetFaction(outpost.factionId);
            string factionName = faction != null ? faction.displayName : outpost.factionId;

            // 检查据点周围是否有道路
            bool outpostHasRoad = HasRoadAdjacentTo(outpost.cell, outpost.size);

            // 检查基地周围是否有道路
            bool baseHasRoad = HasRoadAdjacentTo(baseCell.Value, baseSize);

            // 检查道路连通性
            var path = roadNetwork.FindPathBetweenAreasFromAnchor(
                baseCell.Value, baseSize, outpost.cell, outpost.size);

            bool isConnected = path != null;

            string status = isConnected ? "<color=green>CONNECTED</color>" : "<color=red>DISCONNECTED</color>";
            Debug.Log($"[Verify] {outpost.displayName} ({factionName}) at {outpost.cell}: {status}" +
                $"\n    Base has adjacent road: {baseHasRoad}" +
                $"\n    Outpost has adjacent road: {outpostHasRoad}" +
                (isConnected ? $"\n    Path length: {path.Count} cells" : ""),
                npcManager);
            // 使用richText需要在Console窗口看

            if (isConnected) connected++;
        }

        Debug.Log($"[Verify] ===== Result: {connected}/{total} outposts connected =====");
    }

    [ContextMenu("Verify: Show Road Statistics")]
    public void VerifyRoadStatistics()
    {
        if (roadNetwork == null)
        {
            Debug.LogError("[Verify] RoadNetwork is null!");
            return;
        }

        var allRoads = roadNetwork.GetAllRoads();
        Debug.Log($"[Verify] ===== Road Statistics =====");
        Debug.Log($"[Verify] Total road segments: {allRoads.Count}");

        if (allRoads.Count > 0)
        {
            // 统计连接信息
            int isolated = 0;
            int endpoints = 0;
            int connected = 0;

            foreach (var segment in allRoads)
            {
                int connCount = CountBits((int)segment.connections);
                if (connCount == 0) isolated++;
                else if (connCount == 1) endpoints++;
                else connected++;
            }

            Debug.Log($"[Verify] Isolated (0 connections): {isolated}");
            Debug.Log($"[Verify] Endpoints (1 connection): {endpoints}");
            Debug.Log($"[Verify] Connected (2+ connections): {connected}");
        }

        // 显示基地位置
        Vector2Int? baseCell = FindPlayerBaseCell();
        if (baseCell.HasValue)
        {
            Vector2Int bSize = BaseManager.Instance != null ? BaseManager.Instance.baseGridSize : Vector2Int.one * 3;
            bool baseAdjacentRoad = HasRoadAdjacentTo(baseCell.Value, bSize);
            Debug.Log($"[Verify] Player base at {baseCell.Value}, adjacent road: {baseAdjacentRoad}");
        }
        else
        {
            Debug.LogWarning("[Verify] No player base found!");
        }
    }

    /// <summary>
    /// 查找玩家基地在大地图上的格子坐标
    /// </summary>
    private Vector2Int? FindPlayerBaseCell()
    {
        if (BaseManager.Instance != null)
        {
            var baseSave = BaseManager.Instance.GetActiveBaseSaveData();
            if (baseSave != null && worldMapManager != null)
            {
                return worldMapManager.WorldToCell(baseSave.worldPosition);
            }

            // 尝试遍历所有基地
            var allBases = BaseManager.Instance.AllBaseSaveData;
            if (allBases != null && allBases.Count > 0 && worldMapManager != null)
            {
                return worldMapManager.WorldToCell(allBases[0].worldPosition);
            }
        }

        // 从 WorldMapCellData 查找基地占用的格子
        if (worldMapManager != null)
        {
            var baseCells = worldMapManager.GetCellsByOccupation(WorldMapCellData.OccupationType.Base);
            if (baseCells != null && baseCells.Count > 0)
            {
                return baseCells[0].cell;
            }
        }

        return null;
    }

    /// <summary>
    /// 检查某个区域周围是否有道路（紧邻边缘）
    /// </summary>
    private bool HasRoadAdjacentTo(Vector2Int anchor, Vector2Int size)
    {
        if (roadNetwork == null) return false;

        // 检查四条边外侧一圈
        for (int x = anchor.x - 1; x <= anchor.x + size.x; x++)
        {
            if (roadNetwork.HasRoadAt(new Vector2Int(x, anchor.y - 1))) return true;
            if (roadNetwork.HasRoadAt(new Vector2Int(x, anchor.y + size.y))) return true;
        }
        for (int y = anchor.y - 1; y <= anchor.y + size.y; y++)
        {
            if (roadNetwork.HasRoadAt(new Vector2Int(anchor.x - 1, y))) return true;
            if (roadNetwork.HasRoadAt(new Vector2Int(anchor.x + size.x, y))) return true;
        }

        return false;
    }

    /// <summary>
    /// 计算整数中置位的位数
    /// </summary>
    private static int CountBits(int value)
    {
        int count = 0;
        while (value != 0)
        {
            count += value & 1;
            value >>= 1;
        }
        return count;
    }

    // ============ Trade System Test (Phase 3) ============

    [ContextMenu("Test Trade: Setup & Dispatch")]
    public void TestTradeSetupAndDispatch()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[TradeTest] Must be in Play mode!");
            return;
        }

        Debug.Log("[TradeTest] ===== Phase 3 Trade Test Begin =====");

        // 1) 找 TradeManager
        var tradeManager = TradeManager.Instance;
        if (tradeManager == null)
            tradeManager = FindObjectOfType<TradeManager>();
        if (tradeManager == null)
        {
            Debug.LogError("[TradeTest] TradeManager not found in scene! Add it to your WorldMapSystem GameObject.");
            return;
        }

        // 2) 找玩家基地
        if (BaseManager.Instance == null)
        {
            Debug.LogError("[TradeTest] BaseManager not found!");
            return;
        }

        var baseSave = BaseManager.Instance.GetActiveBaseSaveData();
        if (baseSave == null)
        {
            var allBases = BaseManager.Instance.AllBaseSaveData;
            if (allBases.Count > 0) baseSave = allBases[0];
        }
        if (baseSave == null)
        {
            Debug.LogError("[TradeTest] No base data found! Place a base first.");
            return;
        }

        Vector2Int baseCell = worldMapManager.WorldToCell(baseSave.worldPosition);
        Vector2Int baseSize = BaseManager.Instance.baseGridSize;
        Debug.Log($"[TradeTest] Base '{baseSave.baseId}' at cell {baseCell}, size {baseSize}, money={baseSave.money:F0}");

        // 3) 确保基地有 DockYard 标记
        if (!baseSave.hasDockYard)
        {
            baseSave.hasDockYard = true;
            BaseManager.Instance.UpdateBaseSaveData(baseSave);
            Debug.Log("[TradeTest] Enabled hasDockYard on base (test override)");
        }

        // 4) 给基地添加测试资源（用于出口）
        string testResourceId = FindAnyResourceId();
        if (string.IsNullOrEmpty(testResourceId))
        {
            testResourceId = "iron"; // fallback
        }
        float existingAmount = baseSave.GetResourceAmount(testResourceId);
        if (existingAmount < 100)
        {
            baseSave.AddResource(testResourceId, 100);
            BaseManager.Instance.UpdateBaseSaveData(baseSave);
            Debug.Log($"[TradeTest] Added 100x '{testResourceId}' to base (had {existingAmount:F0})");
        }

        // 5) 找已连接的 NPC 据点
        var npcManager = NPCManager.Instance ?? FindObjectOfType<NPCManager>();
        if (npcManager == null)
        {
            Debug.LogError("[TradeTest] NPCManager not found!");
            return;
        }

        var outposts = npcManager.GetAllOutposts();
        NPCOutpost targetOutpost = null;

        foreach (var outpost in outposts)
        {
            if (roadNetwork == null) break;
            var path = roadNetwork.FindPathBetweenAreasFromAnchor(baseCell, baseSize, outpost.cell, outpost.size);
            if (path != null && path.Count > 0)
            {
                targetOutpost = outpost;
                Debug.Log($"[TradeTest] Found connected outpost: '{outpost.displayName}' at {outpost.cell}, path={path.Count} cells");
                break;
            }
        }

        if (targetOutpost == null)
        {
            Debug.LogError("[TradeTest] No connected NPC outpost found! Build roads between base and an outpost first.");
            return;
        }

        // 6) 创建贸易路线
        var route = tradeManager.CreateTradeRoute(
            baseSave.baseId, baseCell,
            targetOutpost.outpostId, targetOutpost.cell,
            baseSize
        );

        if (route == null || !route.isValid)
        {
            Debug.LogError($"[TradeTest] Failed to create valid trade route! route={route}, valid={route?.isValid}");
            return;
        }

        // 7) 添加出口货物
        route.AddCargoItem(testResourceId, 50, TradeDirection.Export);
        route.autoLoop = false; // 手动触发，不自动循环
        Debug.Log($"[TradeTest] Trade route created: '{route.displayName}', exporting 50x {testResourceId}");

        // 8) 手动派遣运输
        bool dispatched = tradeManager.TryDispatchTransport(route);
        if (dispatched)
        {
            Debug.Log("[TradeTest] <color=green>Transport dispatched successfully!</color>");
            Debug.Log("[TradeTest] Watch for '[TradeManager] Transport completed: delivered=X, lost=Y' in console");
            Debug.Log($"[TradeTest] Base cargo loss rate = {tradeManager.baseCargoLossRate:P1}");

            var orders = tradeManager.GetActiveOrders();
            foreach (var order in orders)
            {
                Debug.Log($"[TradeTest] Active order: {order.orderId.Substring(0, 8)} | " +
                          $"ETA={order.FormattedRemainingTime} | Duration={order.FormattedTravelDuration} | " +
                          $"Progress={order.Progress:P0}");
            }
        }
        else
        {
            Debug.LogError("[TradeTest] Failed to dispatch transport! Check DockYard and resource availability.");
        }

        Debug.Log("[TradeTest] ===== Phase 3 Trade Test End =====");
    }

    [ContextMenu("Test Trade: Show Active Orders")]
    public void TestShowActiveOrders()
    {
        var tradeManager = TradeManager.Instance ?? FindObjectOfType<TradeManager>();
        if (tradeManager == null)
        {
            Debug.LogError("[TradeTest] TradeManager not found!");
            return;
        }

        var orders = tradeManager.GetActiveOrders();
        Debug.Log($"[TradeTest] Active orders: {orders.Count}");
        foreach (var order in orders)
        {
            Debug.Log($"[TradeTest] Order {order.orderId.Substring(0, 8)}: " +
                      $"State={order.state} | Progress={order.Progress:P0} | " +
                      $"Time={order.FormattedRemainingTime}/{order.FormattedTravelDuration} | " +
                      $"Delivered={order.totalDelivered} Lost={order.totalLost}");
        }

        var routes = tradeManager.GetAllTradeRoutes();
        Debug.Log($"[TradeTest] Trade routes: {routes.Count}");
        foreach (var route in routes)
        {
            Debug.Log($"[TradeTest] Route '{route.displayName}': " +
                      $"Valid={route.isValid} | Trips={route.totalTrips} | TotalLost={route.totalCargoLost}");
        }
    }

    /// <summary>
    /// 查找任意可用的资源 ID
    /// </summary>
    private string FindAnyResourceId()
    {
        // 从基地现有资源中找（ResourceSaveData 用 resourceName 字段）
        if (BaseManager.Instance != null)
        {
            var baseSaveData = BaseManager.Instance.GetActiveBaseSaveData();
            if (baseSaveData != null && baseSaveData.resources != null)
            {
                foreach (var res in baseSaveData.resources)
                {
                    if (!string.IsNullOrEmpty(res.resourceName))
                        return res.resourceName;
                }
            }
        }

        // 从 ResourceDefinition ScriptableObjects 中查找
        var allDefs = Resources.FindObjectsOfTypeAll<ResourceDefinition>();
        if (allDefs.Length > 0)
            return allDefs[0].id;

        return null;
    }

    // ============ Gizmos ============
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (worldMapManager == null) return;

        float cellSize = worldMapManager.cellSize;
        Vector3 origin = worldMapManager.origin;

        // 绘制资源区预览
        DrawZonePreview(mineralZoneAnchor, mineralZoneSize, new Color(0.5f, 0.5f, 0.5f, 0.3f), "Mineral", origin, cellSize);
        DrawZonePreview(forestZoneAnchor, forestZoneSize, new Color(0f, 0.6f, 0f, 0.3f), "Forest", origin, cellSize);
        DrawZonePreview(fertileZoneAnchor, fertileZoneSize, new Color(0.6f, 0.4f, 0f, 0.3f), "Fertile", origin, cellSize);
        DrawZonePreview(waterZoneAnchor, waterZoneSize, new Color(0f, 0.4f, 0.8f, 0.3f), "Water", origin, cellSize);

        // 绘制威胁区预览
        DrawZonePreview(threatZoneAnchor, threatZoneSize, new Color(1f, 0f, 0f, 0.4f), "Threat", origin, cellSize);

        // 绘制不可建造区预览
        DrawZonePreview(unbuildableAnchor, unbuildableSize, new Color(0.3f, 0.3f, 0.3f, 0.5f), "Unbuildable", origin, cellSize);

        // 绘制测试道路预览
        Gizmos.color = Color.yellow;
        foreach (var cell in testRoadPath)
        {
            Vector3 center = origin + new Vector3((cell.x + 0.5f) * cellSize, 0.2f, (cell.y + 0.5f) * cellSize);
            Gizmos.DrawWireCube(center, new Vector3(cellSize * 0.5f, 0.1f, cellSize * 0.5f));
        }
    }

    private void DrawZonePreview(Vector2Int anchor, Vector2Int size, Color color, string label, Vector3 origin, float cellSize)
    {
        Gizmos.color = color;

        Vector3 center = origin + new Vector3(
            (anchor.x + size.x * 0.5f) * cellSize,
            0.1f,
            (anchor.y + size.y * 0.5f) * cellSize
        );

        Vector3 boxSize = new Vector3(size.x * cellSize, 0.1f, size.y * cellSize);
        Gizmos.DrawCube(center, boxSize);

        // 绘制标签
        UnityEditor.Handles.Label(center + Vector3.up * 2f, label);
    }
#endif
}
