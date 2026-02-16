using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// WorldMap统一调试工具 - 整合资源区、道路、NPC、贸易等系统的诊断和测试
/// 合并自 ResourceZoneDebugger 和 WorldMapTesterPhase1
/// 优先保留 ResourceZoneDebugger 的诊断风格和方法
/// </summary>
public class WorldMapDebugger : MonoBehaviour
{
    [Header("引用")]
    public WorldMapManager worldMapManager;
    public RoadNetwork roadNetwork;
    public RoadBuilder roadBuilder;
    public RoadSelector roadSelector;

    [Header("测试数据生成设置")]
    [Tooltip("是否在启动时自动生成测试数据（建议关闭，需要时手动通过右键菜单生成）")]
    public bool generateTestDataOnStart = false;

    [Tooltip("资源区生成：true=使用预配置坐标，false=自动动态生成")]
    public bool useConfiguredPositions = false;

    [Header("资源区测试配置（仅当useConfiguredPositions=true时使用）")]
    public Vector2Int mineralZoneAnchor = new Vector2Int(5, 5);
    public Vector2Int mineralZoneSize = new Vector2Int(6, 6);
    public Vector2Int forestZoneAnchor = new Vector2Int(15, 5);
    public Vector2Int forestZoneSize = new Vector2Int(8, 5);
    public Vector2Int fertileZoneAnchor = new Vector2Int(5, 15);
    public Vector2Int fertileZoneSize = new Vector2Int(5, 8);
    public Vector2Int waterZoneAnchor = new Vector2Int(15, 15);
    public Vector2Int waterZoneSize = new Vector2Int(4, 4);

    [Header("其他测试配置")]
    public Vector2Int threatZoneAnchor = new Vector2Int(25, 10);
    public Vector2Int threatZoneSize = new Vector2Int(5, 5);
    public int threatLevel = 3;
    public Vector2Int unbuildableAnchor = new Vector2Int(30, 5);
    public Vector2Int unbuildableSize = new Vector2Int(4, 8);

    [Header("测试道路路径")]
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

        if (roadSelector == null)
            roadSelector = FindObjectOfType<RoadSelector>();
    }

    private void Start()
    {
        // 优先从存档恢复大地图数据
        bool restoredFromSave = TryRestoreFromSave();

        if (!restoredFromSave && generateTestDataOnStart)
        {
            GenerateAllTestData();
        }
    }

    /// <summary>
    /// 尝试从存档恢复大地图数据
    /// </summary>
    private bool TryRestoreFromSave()
    {
        if (BaseManager.Instance == null || !BaseManager.Instance.HasPendingWorldMapData)
            return false;

        Debug.Log("[WorldMap调试] 正在从存档恢复大地图数据...");
        BaseManager.Instance.FlushPendingWorldMapData();

        // 恢复后刷新可视化
        var npcVisualizer = FindObjectOfType<NPCOutpostVisualizer>();
        if (npcVisualizer != null)
            npcVisualizer.RebuildAllOutposts();

        var rzVisualizer = FindObjectOfType<ResourceZoneVisualizer>();
        if (rzVisualizer != null)
            rzVisualizer.RebuildAllZones();

        var roadVisualizer = FindObjectOfType<RoadVisualizer>();
        if (roadVisualizer != null)
            roadVisualizer.RebuildAllRoads();

        Debug.Log("[WorldMap调试] 存档数据恢复完成");
        return true;
    }

    // ═══════════════════════════════════════════════
    //          📊 诊断与状态检查
    // ═══════════════════════════════════════════════

    [ContextMenu("1. 完整诊断报告")]
    private void FullDiagnosticReport()
    {
        Debug.Log("========== WorldMap完整诊断报告 ==========");
        CheckWorldMapManager();
        Debug.Log("---");
        CheckResourceZoneVisualizer();
        Debug.Log("---");
        ShowRoadStatistics();
        Debug.Log("========================================");
    }

    [ContextMenu("2. 检查WorldMapManager状态")]
    private void CheckWorldMapManager()
    {
        var wmm = WorldMapManager.Instance;
        if (wmm == null)
        {
            Debug.LogError("[WorldMap调试] WorldMapManager.Instance 为 null！请确保场景中有WorldMapManager组件");
            return;
        }

        Debug.Log($"[WorldMap调试] WorldMapManager 存在");
        Debug.Log($"[WorldMap调试] 网格设置: origin={wmm.origin}, cellSize={wmm.cellSize}, size={wmm.width}x{wmm.height}");

        // 检查资源区类型
        if (wmm.resourceZoneTypes == null || wmm.resourceZoneTypes.Count == 0)
        {
            Debug.LogWarning("[WorldMap调试] WorldMapManager.resourceZoneTypes 为空！需要在Inspector中添加ResourceZoneType资产");
        }
        else
        {
            Debug.Log($"[WorldMap调试] 已配置 {wmm.resourceZoneTypes.Count} 个资源区类型:");
            foreach (var zt in wmm.resourceZoneTypes)
            {
                if (zt != null)
                    Debug.Log($"  - {zt.displayName} (ID: {zt.zoneId})");
            }
        }

        // 检查格子数据
        var allCells = wmm.GetAllCellDataForSave();
        Debug.Log($"[WorldMap调试] 当前有 {allCells.Count} 个格子有数据");

        // 统计各类型格子
        int resourceZoneCellCount = 0;
        int threatCellCount = 0;
        int roadCellCount = 0;
        int baseCellCount = 0;

        foreach (var cell in allCells)
        {
            if (cell.HasResourceZone) resourceZoneCellCount++;
            if (cell.zoneState == WorldMapCellData.ZoneState.Threat) threatCellCount++;
            if (cell.occupation == WorldMapCellData.OccupationType.Road) roadCellCount++;
            if (cell.occupation == WorldMapCellData.OccupationType.Base) baseCellCount++;
        }

        Debug.Log($"[WorldMap调试] 其中：资源区格子={resourceZoneCellCount}, 威胁区={threatCellCount}, 道路={roadCellCount}, 基地={baseCellCount}");

        if (resourceZoneCellCount == 0)
        {
            Debug.LogWarning("[WorldMap调试] 没有任何资源区格子！可以使用'生成资源区数据'或'生成完整测试数据'来创建");
        }
    }

    [ContextMenu("3. 检查ResourceZoneVisualizer状态")]
    private void CheckResourceZoneVisualizer()
    {
        var viz = FindObjectOfType<ResourceZoneVisualizer>();
        if (viz == null)
        {
            Debug.LogError("[WorldMap调试] 场景中没有 ResourceZoneVisualizer 组件！");
            Debug.LogError("[WorldMap调试] 解决方案：添加一个空GameObject，挂上ResourceZoneVisualizer组件");
            return;
        }

        Debug.Log($"[WorldMap调试] ResourceZoneVisualizer 存在于 {viz.gameObject.name}");
        Debug.Log($"[WorldMap调试] worldMapManager引用: {(viz.worldMapManager != null ? "已连接" : "未连接")}");
        Debug.Log($"[WorldMap调试] showResourceZones: {viz.showResourceZones}");
        Debug.Log($"[WorldMap调试] showLabels: {viz.showLabels}");
        Debug.Log($"[WorldMap调试] showBorders: {viz.showBorders}");
        Debug.Log($"[WorldMap调试] dynamicUpdate: {viz.dynamicUpdate}");

        // 检查预制体映射
        Debug.Log($"[WorldMap调试] 已配置 {viz.resourceZonePrefabs.Count} 个资源区预制体映射");
        foreach (var mapping in viz.resourceZonePrefabs)
        {
            string prefabStatus = mapping.prefab != null ? "有预制体" : "无预制体(使用颜色)";
            Debug.Log($"  - {mapping.displayName} (ID: {mapping.zoneTypeId}): {prefabStatus}");
        }
    }

    [ContextMenu("4. 道路系统统计")]
    private void ShowRoadStatistics()
    {
        if (roadNetwork == null)
        {
            Debug.LogError("[WorldMap调试] RoadNetwork 为 null！");
            return;
        }

        var allRoads = roadNetwork.GetAllRoads();
        Debug.Log($"[WorldMap调试] ===== 道路统计 =====");
        Debug.Log($"[WorldMap调试] 总道路段数: {allRoads.Count}");

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

            Debug.Log($"[WorldMap调试] 孤立道路（0连接）: {isolated}");
            Debug.Log($"[WorldMap调试] 端点（1连接）: {endpoints}");
            Debug.Log($"[WorldMap调试] 连接点（2+连接）: {connected}");
        }

        // 显示基地位置
        Vector2Int? baseCell = FindPlayerBaseCell();
        if (baseCell.HasValue)
        {
            Vector2Int bSize = BaseManager.Instance != null ? BaseManager.Instance.baseGridSize : Vector2Int.one * 3;
            bool baseAdjacentRoad = HasRoadAdjacentTo(baseCell.Value, bSize);
            Debug.Log($"[WorldMap调试] 玩家基地位于 {baseCell.Value}，周围有道路: {baseAdjacentRoad}");
        }
        else
        {
            Debug.LogWarning("[WorldMap调试] 未找到玩家基地");
        }
    }

    [ContextMenu("5. 验证NPC据点连接")]
    private void VerifyOutpostConnections()
    {
        if (roadNetwork == null || worldMapManager == null)
        {
            Debug.LogError("[WorldMap调试] RoadNetwork 或 WorldMapManager 为 null！");
            return;
        }

        var npcManager = NPCManager.Instance ?? FindObjectOfType<NPCManager>();
        if (npcManager == null)
        {
            Debug.LogError("[WorldMap调试] 未找到 NPCManager！");
            return;
        }

        // 找到玩家基地
        Vector2Int? baseCell = FindPlayerBaseCell();
        if (!baseCell.HasValue)
        {
            Debug.LogError("[WorldMap调试] 未找到玩家基地！");
            return;
        }

        Vector2Int baseSize = BaseManager.Instance != null ? BaseManager.Instance.baseGridSize : Vector2Int.one * 3;

        Debug.Log($"[WorldMap调试] ===== NPC据点连接报告 =====");
        Debug.Log($"[WorldMap调试] 玩家基地位置: {baseCell.Value}, 大小: {baseSize}");

        var outposts = npcManager.GetAllOutposts();
        if (outposts.Count == 0)
        {
            Debug.LogWarning("[WorldMap调试] 没有NPC据点！");
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

            string status = isConnected ? "<color=green>已连接</color>" : "<color=red>未连接</color>";
            Debug.Log($"[WorldMap调试] {outpost.displayName} ({factionName}) 位于 {outpost.cell}: {status}" +
                $"\n    基地周围有道路: {baseHasRoad}" +
                $"\n    据点周围有道路: {outpostHasRoad}" +
                (isConnected ? $"\n    路径长度: {path.Count} 格" : ""),
                npcManager);

            if (isConnected) connected++;
        }

        Debug.Log($"[WorldMap调试] ===== 结果: {connected}/{total} 个据点已连接 =====");
    }

    // ═══════════════════════════════════════════════
    //          🛠️ 数据生成与修复
    // ═══════════════════════════════════════════════

    [ContextMenu("生成完整测试数据")]
    public void GenerateAllTestData()
    {
        Debug.Log("[WorldMap调试] 开始生成完整测试数据...");

        GenerateResourceZones();
        GenerateThreatZones();
        GenerateUnbuildableZones();
        GenerateTestRoad();
        InitializeNPCOutposts();

        Debug.Log("[WorldMap调试] 测试数据生成完成！");
    }

    [ContextMenu("生成资源区数据")]
    private void GenerateResourceZones()
    {
        if (worldMapManager == null)
        {
            Debug.LogError("[WorldMap调试] WorldMapManager 为 null！");
            return;
        }

        if (worldMapManager.resourceZoneTypes == null || worldMapManager.resourceZoneTypes.Count == 0)
        {
            Debug.LogError("[WorldMap调试] WorldMapManager.resourceZoneTypes 为空！请先在Inspector中添加ResourceZoneType资产");
            return;
        }

        Debug.Log("[WorldMap调试] 开始生成资源区数据...");

        if (useConfiguredPositions)
        {
            // 使用预配置的坐标（WorldMapTesterPhase1 方式）
            Debug.Log("[WorldMap调试] 使用预配置坐标生成资源区");

            if (worldMapManager.resourceZoneTypes.Count > 0 && worldMapManager.resourceZoneTypes[0] != null)
            {
                worldMapManager.SetResourceZoneArea(mineralZoneAnchor, mineralZoneSize,
                    worldMapManager.resourceZoneTypes[0].zoneId);
                Debug.Log($"[WorldMap调试] 创建了矿产区在 {mineralZoneAnchor}");
            }

            if (worldMapManager.resourceZoneTypes.Count > 1 && worldMapManager.resourceZoneTypes[1] != null)
            {
                worldMapManager.SetResourceZoneArea(forestZoneAnchor, forestZoneSize,
                    worldMapManager.resourceZoneTypes[1].zoneId);
                Debug.Log($"[WorldMap调试] 创建了森林区在 {forestZoneAnchor}");
            }

            if (worldMapManager.resourceZoneTypes.Count > 2 && worldMapManager.resourceZoneTypes[2] != null)
            {
                worldMapManager.SetResourceZoneArea(fertileZoneAnchor, fertileZoneSize,
                    worldMapManager.resourceZoneTypes[2].zoneId);
                Debug.Log($"[WorldMap调试] 创建了肥沃区在 {fertileZoneAnchor}");
            }

            if (worldMapManager.resourceZoneTypes.Count > 3 && worldMapManager.resourceZoneTypes[3] != null)
            {
                worldMapManager.SetResourceZoneArea(waterZoneAnchor, waterZoneSize,
                    worldMapManager.resourceZoneTypes[3].zoneId);
                Debug.Log($"[WorldMap调试] 创建了水源区在 {waterZoneAnchor}");
            }
        }
        else
        {
            // 自动动态生成（ResourceZoneDebugger 方式 - 优先）
            Debug.Log("[WorldMap调试] 使用自动动态生成资源区");

            for (int i = 0; i < worldMapManager.resourceZoneTypes.Count; i++)
            {
                var zt = worldMapManager.resourceZoneTypes[i];
                if (zt == null) continue;

                // 动态计算位置，避免重叠
                Vector2Int anchor = new Vector2Int(5 + i * 10, 5);
                Vector2Int size = new Vector2Int(6, 6);

                worldMapManager.SetResourceZoneArea(anchor, size, zt.zoneId);
                Debug.Log($"[WorldMap调试] 创建了 '{zt.displayName}' 资源区在 {anchor}，大小 {size}");
            }
        }

        Debug.Log($"[WorldMap调试] 资源区生成完成！共生成 {worldMapManager.resourceZoneTypes.Count} 个区域");
    }

    private void GenerateThreatZones()
    {
        if (worldMapManager == null) return;

        worldMapManager.SetThreatZoneArea(threatZoneAnchor, threatZoneSize, threatLevel);
        Debug.Log($"[WorldMap调试] 创建了威胁区在 {threatZoneAnchor}，等级 {threatLevel}");
    }

    private void GenerateUnbuildableZones()
    {
        if (worldMapManager == null) return;

        worldMapManager.SetUnbuildableArea(unbuildableAnchor, unbuildableSize);
        Debug.Log($"[WorldMap调试] 创建了不可建造区在 {unbuildableAnchor}");
    }

    private void GenerateTestRoad()
    {
        if (roadNetwork == null) return;

        var defaultType = roadNetwork.GetDefaultRoadType();
        if (defaultType == null)
        {
            Debug.LogWarning("[WorldMap调试] 没有定义道路类型！");
            return;
        }

        bool success = roadNetwork.TryBuildRoadPath(testRoadPath, defaultType.roadTypeId);
        if (success)
        {
            Debug.Log($"[WorldMap调试] 创建了测试道路，共 {testRoadPath.Count} 段");
        }
        else
        {
            Debug.LogWarning("[WorldMap调试] 测试道路创建失败");
        }
    }

    private void InitializeNPCOutposts()
    {
        var npcManager = NPCManager.Instance ?? FindObjectOfType<NPCManager>();
        if (npcManager == null)
        {
            Debug.LogWarning("[WorldMap调试] 未找到 NPCManager，跳过NPC初始化");
            return;
        }

        npcManager.InitializeDefaultOutposts();
        Debug.Log("[WorldMap调试] NPC据点初始化完成");
    }

    [ContextMenu("一键修复资源区")]
    private void QuickFixResourceZones()
    {
        Debug.Log("[WorldMap调试] 开始一键修复资源区...");
        GenerateResourceZones();
        RebuildResourceZones();
        Debug.Log("[WorldMap调试] 一键修复完成！请查看Game视图");
    }

    // ═══════════════════════════════════════════════
    //          🎨 可视化控制
    // ═══════════════════════════════════════════════

    [ContextMenu("强制重建所有可视化")]
    private void RebuildAllVisuals()
    {
        Debug.Log("[WorldMap调试] 开始重建所有可视化...");

        RebuildResourceZones();

        var roadVisualizer = FindObjectOfType<RoadVisualizer>();
        if (roadVisualizer != null)
        {
            roadVisualizer.RebuildAllRoads();
            Debug.Log("[WorldMap调试] 道路可视化已重建");
        }

        var npcVisualizer = FindObjectOfType<NPCOutpostVisualizer>();
        if (npcVisualizer != null)
        {
            npcVisualizer.RebuildAllOutposts();
            Debug.Log("[WorldMap调试] NPC据点可视化已重建");
        }

        Debug.Log("[WorldMap调试] 所有可视化重建完成！");
    }

    [ContextMenu("重建资源区可视化")]
    private void RebuildResourceZones()
    {
        var viz = FindObjectOfType<ResourceZoneVisualizer>();
        if (viz == null)
        {
            Debug.LogError("[WorldMap调试] 场景中没有 ResourceZoneVisualizer！");
            return;
        }

        Debug.Log("[WorldMap调试] 开始重建资源区可视化...");
        viz.ForceRebuild();
        Debug.Log("[WorldMap调试] 资源区可视化重建完成！");
    }

    // ═══════════════════════════════════════════════
    //          🧪 快速测试
    // ═══════════════════════════════════════════════

    [ContextMenu("测试: 路径查找")]
    private void TestPathFinding()
    {
        if (roadNetwork == null)
        {
            Debug.LogError("[WorldMap调试] RoadNetwork 为 null！");
            return;
        }

        Vector2Int start = new Vector2Int(10, 10);
        Vector2Int end = new Vector2Int(15, 10);

        var path = roadNetwork.FindPath(start, end);
        if (path != null)
        {
            Debug.Log($"[WorldMap调试] 找到路径: {string.Join(" -> ", path)}");
        }
        else
        {
            Debug.LogWarning("[WorldMap调试] 未找到路径！");
        }
    }

    [ContextMenu("测试: 格子信息")]
    private void TestCellInfo()
    {
        if (worldMapManager == null)
        {
            Debug.LogError("[WorldMap调试] WorldMapManager 为 null！");
            return;
        }

        Vector2Int cell = new Vector2Int(15, 15);
        var data = worldMapManager.GetCellData(cell);

        if (data != null)
        {
            Debug.Log($"[WorldMap调试] 格子 {cell}:\n" +
                      $"  资源区: {data.resourceZoneTypeId ?? "无"}\n" +
                      $"  区域状态: {data.zoneState}\n" +
                      $"  占用类型: {data.occupation}\n" +
                      $"  可建造基地: {data.CanBuildBase}\n" +
                      $"  可建造道路: {data.CanBuildRoad}");
        }
        else
        {
            Debug.Log($"[WorldMap调试] 格子 {cell}: 无数据（默认状态）");
        }
    }

    [ContextMenu("工具: 预加载资源缓存")]
    private void PreloadResourceCaches()
    {
        Debug.Log("[WorldMap调试] 开始预加载资源缓存...");
        BaseInfoData.PreloadAllCaches();
        Debug.Log("[WorldMap调试] 资源缓存预加载完成！");
    }

    [ContextMenu("工具: 清空资源缓存")]
    private void ClearResourceCaches()
    {
        BaseInfoData.ClearAllCaches();
        Debug.Log("[WorldMap调试] 资源缓存已清空");
    }

    [ContextMenu("工具: 检查道路升级配置")]
    private void CheckRoadUpgradeConfig()
    {
        if (roadNetwork == null)
        {
            Debug.LogError("[WorldMap调试] RoadNetwork 为 null！");
            return;
        }

        Debug.Log($"[WorldMap调试] ========================================");
        Debug.Log($"[WorldMap调试] 道路升级系统配置检查");
        Debug.Log($"[WorldMap调试] ========================================");

        // 检查道路类型配置
        Debug.Log($"[WorldMap调试] RoadNetwork.roadTypes 数量: {roadNetwork.roadTypes.Count}");

        if (roadNetwork.roadTypes.Count == 0)
        {
            Debug.LogError("[WorldMap调试] ❌ 道路类型列表为空！请在RoadNetwork的Inspector中添加RoadType资产。");
            return;
        }

        Debug.Log($"[WorldMap调试] ----------------------------------------");
        Debug.Log($"[WorldMap调试] 已配置的道路类型:");

        foreach (var roadType in roadNetwork.roadTypes)
        {
            if (roadType == null)
            {
                Debug.LogWarning("[WorldMap调试] ⚠️ 发现null道路类型！");
                continue;
            }

            Debug.Log($"[WorldMap调试]   - {roadType.displayName} (ID: {roadType.roadTypeId}, Level: {roadType.level})");
            Debug.Log($"[WorldMap调试]     建造成本: ${roadType.moneyCost}");

            if (roadType.upgradeTo != null)
                Debug.Log($"[WorldMap调试]     可升级到: {roadType.upgradeTo.displayName} (Level {roadType.upgradeTo.level})");
            else
                Debug.Log($"[WorldMap调试]     可升级到: 无（最高等级）");
        }

        Debug.Log($"[WorldMap调试] ----------------------------------------");

        // 检查当前选中的道路
        if (roadSelector != null && roadSelector.SelectedCells.Count > 0)
        {
            Debug.Log($"[WorldMap调试] 当前选中道路数量: {roadSelector.SelectedCells.Count}");

            // HashSet不支持索引，使用枚举器获取第一个元素
            Vector2Int firstCell = default;
            foreach (var cell in roadSelector.SelectedCells)
            {
                firstCell = cell;
                break;
            }

            var segment = roadNetwork.GetRoadAt(firstCell);

            if (segment != null)
            {
                var currentType = roadNetwork.GetRoadType(segment.roadTypeId);
                if (currentType != null)
                {
                    Debug.Log($"[WorldMap调试] 选中道路类型: {currentType.displayName} (Level {currentType.level})");

                    // 检查可升级选项
                    Debug.Log($"[WorldMap调试] ----------------------------------------");
                    Debug.Log($"[WorldMap调试] 可升级到的类型:");

                    bool hasUpgradeOptions = false;
                    foreach (var targetType in roadNetwork.roadTypes)
                    {
                        if (targetType == null) continue;
                        if (targetType.level > currentType.level)
                        {
                            hasUpgradeOptions = true;
                            float cost = targetType.moneyCost - currentType.moneyCost;
                            Debug.Log($"[WorldMap调试]   ✓ {targetType.displayName} (Level {targetType.level}) - 升级成本: ${Mathf.Max(0, cost):F0}");
                        }
                    }

                    if (!hasUpgradeOptions)
                    {
                        Debug.LogWarning($"[WorldMap调试] ❌ 当前道路已是最高等级，无法升级！");
                    }
                }
                else
                {
                    Debug.LogError($"[WorldMap调试] ❌ 找不到道路类型ID: {segment.roadTypeId}");
                }
            }
            else
            {
                Debug.LogError($"[WorldMap调试] ❌ 选中的格子 {firstCell} 上没有道路！");
            }
        }
        else
        {
            Debug.Log($"[WorldMap调试] 当前没有选中任何道路");
        }

        Debug.Log($"[WorldMap调试] ========================================");
    }

    [ContextMenu("工具: 检查基地资源区信息")]
    private void CheckBaseResourceZones()
    {
        if (BaseManager.Instance == null)
        {
            Debug.LogError("[WorldMap调试] BaseManager.Instance 为 null！");
            return;
        }

        var allBases = BaseManager.Instance.AllBaseSaveData;
        if (allBases == null || allBases.Count == 0)
        {
            Debug.LogWarning("[WorldMap调试] 没有基地数据");
            return;
        }

        Debug.Log($"[WorldMap调试] ========================================");
        Debug.Log($"[WorldMap调试] 基地资源区检测报告");
        Debug.Log($"[WorldMap调试] ========================================");
        Debug.Log($"[WorldMap调试] 总基地数: {allBases.Count}");
        Debug.Log($"[WorldMap调试] ----------------------------------------");

        int inResourceZone = 0;
        Dictionary<string, int> zoneDistribution = new Dictionary<string, int>();

        foreach (var baseData in allBases)
        {
            if (baseData == null) continue;

            bool hasZone = !string.IsNullOrEmpty(baseData.resourceZoneTypeId);

            Debug.Log($"[WorldMap调试] 基地: {baseData.baseName}");
            Debug.Log($"[WorldMap调试]   位置: ({baseData.worldPosition.x:F0}, {baseData.worldPosition.z:F0})");

            if (hasZone)
            {
                var zoneType = BaseManager.Instance.FindResourceZoneType(baseData.resourceZoneTypeId);
                if (zoneType != null)
                {
                    inResourceZone++;

                    // 统计资源区分布
                    if (!zoneDistribution.ContainsKey(zoneType.zoneId))
                        zoneDistribution[zoneType.zoneId] = 0;
                    zoneDistribution[zoneType.zoneId]++;

                    Debug.Log($"[WorldMap调试]   资源区: <color=green>{zoneType.displayName}</color>");
                    Debug.Log($"[WorldMap调试]   效率加成: <color=yellow>{zoneType.efficiencyBonus}x</color>");
                    Debug.Log($"[WorldMap调试]   品质资源概率: {zoneType.qualityResourceChance:P0}");
                    Debug.Log($"[WorldMap调试]   副产品概率: {zoneType.byproductChance:P0}");

                    // 显示适用建筑
                    if (zoneType.compatibleBuildings != null && zoneType.compatibleBuildings.Length > 0)
                    {
                        Debug.Log($"[WorldMap调试]   适用建筑: {string.Join(", ", System.Array.ConvertAll(zoneType.compatibleBuildings, b => b != null ? b.name : "null"))}");
                    }
                    else
                    {
                        Debug.Log($"[WorldMap调试]   适用建筑: 无");
                    }
                }
                else
                {
                    Debug.LogWarning($"[WorldMap调试]   资源区ID '{baseData.resourceZoneTypeId}' 未找到对应的ResourceZoneType！");
                }
            }
            else
            {
                Debug.Log($"[WorldMap调试]   资源区: <color=grey>无</color>");
            }

            Debug.Log($"[WorldMap调试] ----------------------------------------");
        }

        // 统计摘要
        Debug.Log($"[WorldMap调试] ========================================");
        Debug.Log($"[WorldMap调试] 统计摘要");
        Debug.Log($"[WorldMap调试] ========================================");
        Debug.Log($"[WorldMap调试] 在资源区内的基地: {inResourceZone}/{allBases.Count}");

        if (zoneDistribution.Count > 0)
        {
            Debug.Log($"[WorldMap调试] 资源区分布:");
            foreach (var kvp in zoneDistribution)
            {
                var zoneType = BaseManager.Instance.FindResourceZoneType(kvp.Key);
                string zoneName = zoneType != null ? zoneType.displayName : kvp.Key;
                Debug.Log($"[WorldMap调试]   {zoneName}: {kvp.Value} 个基地");
            }
        }

        Debug.Log($"[WorldMap调试] ========================================");
    }

    [ContextMenu("测试: 清除威胁区")]
    private void TestClearThreatZone()
    {
        if (worldMapManager == null) return;

        worldMapManager.ClearThreatArea(threatZoneAnchor, threatZoneSize);
        Debug.Log("[WorldMap调试] 威胁区已清除");
    }

    [ContextMenu("测试: 建造单条道路")]
    private void TestBuildSingleRoad()
    {
        if (roadNetwork == null) return;

        var defaultType = roadNetwork.GetDefaultRoadType();
        if (defaultType == null) return;

        Vector2Int cell = new Vector2Int(20, 10);
        bool success = roadNetwork.TryBuildRoad(cell, defaultType.roadTypeId);
        Debug.Log($"[WorldMap调试] 在 {cell} 建造道路: {(success ? "成功" : "失败")}");
    }

    [ContextMenu("测试: 升级测试道路")]
    private void TestUpgradeRoad()
    {
        if (roadNetwork == null) return;

        int upgraded = 0;
        foreach (var cell in testRoadPath)
        {
            if (roadNetwork.TryUpgradeRoad(cell))
                upgraded++;
        }
        Debug.Log($"[WorldMap调试] 升级了 {upgraded} 段道路");
    }

    // ═══════════════════════════════════════════════
    //          🔧 辅助方法
    // ═══════════════════════════════════════════════

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

    // ═══════════════════════════════════════════════
    //          🎯 Gizmos 可视化
    // ═══════════════════════════════════════════════

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (worldMapManager == null) return;

        float cellSize = worldMapManager.cellSize;
        Vector3 origin = worldMapManager.origin;

        // 绘制资源区预览
        DrawZonePreview(mineralZoneAnchor, mineralZoneSize, new Color(0.5f, 0.5f, 0.5f, 0.3f), "矿产区", origin, cellSize);
        DrawZonePreview(forestZoneAnchor, forestZoneSize, new Color(0f, 0.6f, 0f, 0.3f), "森林区", origin, cellSize);
        DrawZonePreview(fertileZoneAnchor, fertileZoneSize, new Color(0.6f, 0.4f, 0f, 0.3f), "肥沃区", origin, cellSize);
        DrawZonePreview(waterZoneAnchor, waterZoneSize, new Color(0f, 0.4f, 0.8f, 0.3f), "水源区", origin, cellSize);

        // 绘制威胁区预览
        DrawZonePreview(threatZoneAnchor, threatZoneSize, new Color(1f, 0f, 0f, 0.4f), "威胁区", origin, cellSize);

        // 绘制不可建造区预览
        DrawZonePreview(unbuildableAnchor, unbuildableSize, new Color(0.3f, 0.3f, 0.3f, 0.5f), "不可建造", origin, cellSize);

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
