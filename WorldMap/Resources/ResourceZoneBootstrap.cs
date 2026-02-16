using UnityEngine;

/// <summary>
/// 资源区一键初始化器 - 将此脚本挂到任意场景中，自动搭建 WorldMapManager + ResourceZoneVisualizer + 测试数据
/// 如果场景中已有 WorldMapManager 则不会重复创建
///
/// 使用方法：
///   1. 在场景中创建空 GameObject
///   2. 挂上此脚本
///   3. 在 Inspector 中设置基地所在的格子坐标（baseCell）和想要的资源区类型
///   4. 将 ResourceZoneType 资产拖入 testZoneType
///   5. 运行游戏即可看到效果
/// </summary>
public class ResourceZoneBootstrap : MonoBehaviour
{
    [Header("Grid Settings（与 WorldMapManager 同步）")]
    [Tooltip("网格原点")]
    public Vector3 gridOrigin = Vector3.zero;

    [Tooltip("每格大小")]
    [Min(0.1f)] public float cellSize = 10f;

    [Tooltip("网格宽度")]
    [Min(1)] public int gridWidth = 50;

    [Tooltip("网格高度")]
    [Min(1)] public int gridHeight = 50;

    [Header("Resource Zone Types（拖入已有的 ResourceZoneType 资产）")]
    public ResourceZoneType[] zoneTypes;

    [Header("Test Zone Setup")]
    [Tooltip("基地所在的世界坐标（自动转换为格子坐标）")]
    public bool autoDetectBase = true;

    [Tooltip("手动指定基地所在格子（autoDetectBase=false 时使用）")]
    public Vector2Int manualBaseCell = new Vector2Int(5, 5);

    [Tooltip("在基地周围生成的资源区（从 zoneTypes 中选第几个，-1=不生成）")]
    public int testZoneIndex = 0;

    [Tooltip("资源区大小")]
    public Vector2Int zoneSize = new Vector2Int(8, 8);

    [Header("Additional Test Zones")]
    [Tooltip("是否生成威胁区测试")]
    public bool generateThreatZone = true;
    public Vector2Int threatAnchor = new Vector2Int(20, 10);
    public Vector2Int threatSize = new Vector2Int(4, 4);

    [Tooltip("是否生成不可建造区测试")]
    public bool generateUnbuildableZone = true;
    public Vector2Int unbuildableAnchor = new Vector2Int(25, 5);
    public Vector2Int unbuildableSize = new Vector2Int(3, 6);

    [Header("Visualizer Settings")]
    [Tooltip("是否同时创建 ResourceZoneVisualizer")]
    public bool createVisualizer = true;

    [Tooltip("是否显示标签")]
    public bool showLabels = true;

    [Tooltip("是否显示边界线")]
    public bool showBorders = true;

    private void Awake()
    {
        EnsureWorldMapManager();
    }

    private void Start()
    {
        SetupTestData();

        if (createVisualizer)
            EnsureVisualizer();
    }

    /// <summary>
    /// 确保场景中有 WorldMapManager
    /// </summary>
    private void EnsureWorldMapManager()
    {
        if (WorldMapManager.Instance != null) return;

        var wmGO = new GameObject("WorldMapManager (Bootstrap)");
        var wm = wmGO.AddComponent<WorldMapManager>();
        wm.origin = gridOrigin;
        wm.cellSize = cellSize;
        wm.width = gridWidth;
        wm.height = gridHeight;
        wm.syncFromExistingGrid = true; // 如果有 WorldMapGrid 优先同步

        // 添加资源区类型
        if (zoneTypes != null)
        {
            foreach (var zt in zoneTypes)
            {
                if (zt != null)
                    wm.resourceZoneTypes.Add(zt);
            }
        }

        Debug.Log($"[ResourceZoneBootstrap] Created WorldMapManager: origin={gridOrigin}, cellSize={cellSize}, size={gridWidth}x{gridHeight}");
    }

    /// <summary>
    /// 设置测试数据
    /// </summary>
    private void SetupTestData()
    {
        var wm = WorldMapManager.Instance;
        if (wm == null) return;

        // 找到基地位置
        Vector2Int baseCell = manualBaseCell;
        if (autoDetectBase)
        {
            var baseInst = FindObjectOfType<BaseInstance>();
            if (baseInst != null)
            {
                baseCell = wm.WorldToCell(baseInst.transform.position);
                Debug.Log($"[ResourceZoneBootstrap] Auto-detected base at cell {baseCell} (world pos: {baseInst.transform.position})");
            }
            else
            {
                Debug.LogWarning("[ResourceZoneBootstrap] No BaseInstance found, using manual baseCell");
            }
        }

        // 在基地所在位置设置资源区
        if (testZoneIndex >= 0 && zoneTypes != null && testZoneIndex < zoneTypes.Length && zoneTypes[testZoneIndex] != null)
        {
            var zt = zoneTypes[testZoneIndex];
            // 资源区覆盖基地周围（以基地为中心偏左下）
            Vector2Int zoneAnchor = new Vector2Int(
                baseCell.x - zoneSize.x / 2,
                baseCell.y - zoneSize.y / 2
            );

            wm.SetResourceZoneArea(zoneAnchor, zoneSize, zt.zoneId);
            Debug.Log($"[ResourceZoneBootstrap] Created resource zone '{zt.displayName}' at {zoneAnchor} size {zoneSize}, covering base at {baseCell}");
        }

        // 生成所有 zoneTypes 的测试区域（各自偏移）
        for (int i = 0; i < zoneTypes.Length; i++)
        {
            if (i == testZoneIndex) continue; // 已经在基地位置创建过了
            if (zoneTypes[i] == null) continue;

            // 在不同位置放置其他资源区
            Vector2Int offset = new Vector2Int(15 + i * 10, 5);
            Vector2Int size = new Vector2Int(5, 5);
            wm.SetResourceZoneArea(offset, size, zoneTypes[i].zoneId);
            Debug.Log($"[ResourceZoneBootstrap] Created resource zone '{zoneTypes[i].displayName}' at {offset}");
        }

        // 威胁区
        if (generateThreatZone)
        {
            wm.SetThreatZoneArea(threatAnchor, threatSize, 3);
            Debug.Log($"[ResourceZoneBootstrap] Created threat zone at {threatAnchor}");
        }

        // 不可建造区
        if (generateUnbuildableZone)
        {
            wm.SetUnbuildableArea(unbuildableAnchor, unbuildableSize);
            Debug.Log($"[ResourceZoneBootstrap] Created unbuildable zone at {unbuildableAnchor}");
        }

        // 直接把资源区信息设到 BaseInstance 上（确保在基地场景中也能正常工作）
        var baseInst2 = FindObjectOfType<BaseInstance>();
        if (baseInst2 != null && baseInst2.ResourceZone == null)
        {
            // 查询基地位置的资源区
            Vector2Int bc = wm.WorldToCell(baseInst2.transform.position);
            var zone = wm.GetCellResourceZone(bc);
            if (zone != null)
            {
                baseInst2.SetResourceZone(zone);
                Debug.Log($"[ResourceZoneBootstrap] 直接设置基地资源区: {zone.displayName}");
            }
        }

        // 刷新所有 ProducerBuilding 的资源区检查
        var producers = FindObjectsOfType<ProducerBuilding>();
        foreach (var p in producers)
        {
            p.RefreshResourceZone();
        }
        Debug.Log($"[ResourceZoneBootstrap] Refreshed {producers.Length} ProducerBuildings");
    }

    /// <summary>
    /// 确保场景中有 ResourceZoneVisualizer
    /// </summary>
    private void EnsureVisualizer()
    {
        var existing = FindObjectOfType<ResourceZoneVisualizer>();
        if (existing != null)
        {
            existing.ForceRebuild();
            return;
        }

        var vizGO = new GameObject("ResourceZoneVisualizer (Bootstrap)");
        var viz = vizGO.AddComponent<ResourceZoneVisualizer>();
        viz.worldMapManager = WorldMapManager.Instance;
        viz.showLabels = showLabels;
        viz.showBorders = showBorders;
        viz.dynamicUpdate = true;
        viz.updateInterval = 1f;

        Debug.Log("[ResourceZoneBootstrap] Created ResourceZoneVisualizer");
    }

#if UNITY_EDITOR
    [ContextMenu("Force Rebuild Visualizer")]
    private void ForceRebuildVisualizer()
    {
        var viz = FindObjectOfType<ResourceZoneVisualizer>();
        if (viz != null)
            viz.ForceRebuild();
    }

    [ContextMenu("Refresh All Producers")]
    private void RefreshAllProducers()
    {
        var producers = FindObjectsOfType<ProducerBuilding>();
        foreach (var p in producers)
            p.RefreshResourceZone();
        Debug.Log($"[ResourceZoneBootstrap] Refreshed {producers.Length} ProducerBuildings");
    }

    [ContextMenu("Debug: Print Base Cell")]
    private void DebugPrintBaseCell()
    {
        var wm = WorldMapManager.Instance;
        if (wm == null)
        {
            Debug.Log("[Debug] No WorldMapManager");
            return;
        }

        var baseInst = FindObjectOfType<BaseInstance>();
        if (baseInst != null)
        {
            var cell = wm.WorldToCell(baseInst.transform.position);
            var zoneType = wm.GetCellResourceZone(cell);
            Debug.Log($"[Debug] Base at world={baseInst.transform.position}, cell={cell}, resourceZone={zoneType?.displayName ?? "None"}");
        }
        else
        {
            Debug.Log("[Debug] No BaseInstance found in scene");
        }
    }
#endif
}
