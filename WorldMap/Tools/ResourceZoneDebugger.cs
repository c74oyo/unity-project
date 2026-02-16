using UnityEngine;

/// <summary>
/// 资源区调试器 - 帮助诊断资源区显示问题
/// </summary>
public class ResourceZoneDebugger : MonoBehaviour
{
    [ContextMenu("1. 检查WorldMapManager状态")]
    private void CheckWorldMapManager()
    {
        var wmm = WorldMapManager.Instance;
        if (wmm == null)
        {
            Debug.LogError("[资源区调试] WorldMapManager.Instance 为 null！请确保场景中有WorldMapManager组件");
            return;
        }

        Debug.Log($"[资源区调试] WorldMapManager 存在");
        Debug.Log($"[资源区调试] 网格设置: origin={wmm.origin}, cellSize={wmm.cellSize}, size={wmm.width}x{wmm.height}");

        // 检查资源区类型
        if (wmm.resourceZoneTypes == null || wmm.resourceZoneTypes.Count == 0)
        {
            Debug.LogWarning("[资源区调试] WorldMapManager.resourceZoneTypes 为空！需要在Inspector中添加ResourceZoneType资产");
        }
        else
        {
            Debug.Log($"[资源区调试] 已配置 {wmm.resourceZoneTypes.Count} 个资源区类型:");
            foreach (var zt in wmm.resourceZoneTypes)
            {
                if (zt != null)
                    Debug.Log($"  - {zt.displayName} (ID: {zt.zoneId})");
            }
        }

        // 检查格子数据
        var allCells = wmm.GetAllCellDataForSave();
        Debug.Log($"[资源区调试] 当前有 {allCells.Count} 个格子有数据");

        // 检查资源区格子数量
        int resourceZoneCellCount = 0;
        foreach (var cell in allCells)
        {
            if (cell.HasResourceZone)
                resourceZoneCellCount++;
        }
        Debug.Log($"[资源区调试] 其中有 {resourceZoneCellCount} 个格子属于资源区");

        if (resourceZoneCellCount == 0)
        {
            Debug.LogWarning("[资源区调试] 没有任何资源区格子！需要调用 WorldMapManager.SetResourceZoneArea() 来创建资源区");
            Debug.LogWarning("[资源区调试] 解决方案：在场景中添加 WorldMapTesterPhase1 或 ResourceZoneBootstrap 组件来生成测试数据");
        }
    }

    [ContextMenu("2. 检查ResourceZoneVisualizer状态")]
    private void CheckVisualizer()
    {
        var viz = FindObjectOfType<ResourceZoneVisualizer>();
        if (viz == null)
        {
            Debug.LogError("[资源区调试] 场景中没有 ResourceZoneVisualizer 组件！");
            Debug.LogError("[资源区调试] 解决方案：添加一个空GameObject，挂上ResourceZoneVisualizer组件");
            return;
        }

        Debug.Log($"[资源区调试] ResourceZoneVisualizer 存在于 {viz.gameObject.name}");
        Debug.Log($"[资源区调试] worldMapManager引用: {(viz.worldMapManager != null ? "已连接" : "未连接")}");
        Debug.Log($"[资源区调试] showResourceZones: {viz.showResourceZones}");
        Debug.Log($"[资源区调试] showLabels: {viz.showLabels}");
        Debug.Log($"[资源区调试] showBorders: {viz.showBorders}");
        Debug.Log($"[资源区调试] dynamicUpdate: {viz.dynamicUpdate}");

        // 检查预制体映射
        Debug.Log($"[资源区调试] 已配置 {viz.resourceZonePrefabs.Count} 个资源区预制体映射");
        foreach (var mapping in viz.resourceZonePrefabs)
        {
            string prefabStatus = mapping.prefab != null ? "有预制体" : "无预制体(使用颜色)";
            Debug.Log($"  - {mapping.displayName} (ID: {mapping.zoneTypeId}): {prefabStatus}");
        }
    }

    [ContextMenu("3. 强制重建资源区可视化")]
    private void ForceRebuild()
    {
        var viz = FindObjectOfType<ResourceZoneVisualizer>();
        if (viz == null)
        {
            Debug.LogError("[资源区调试] 场景中没有 ResourceZoneVisualizer！");
            return;
        }

        Debug.Log("[资源区调试] 开始强制重建资源区可视化...");
        viz.ForceRebuild();
        Debug.Log("[资源区调试] 重建完成！检查Scene视图和Game视图");
    }

    [ContextMenu("4. 生成测试资源区数据")]
    private void GenerateTestResourceZones()
    {
        var wmm = WorldMapManager.Instance;
        if (wmm == null)
        {
            Debug.LogError("[资源区调试] WorldMapManager.Instance 为 null！");
            return;
        }

        if (wmm.resourceZoneTypes == null || wmm.resourceZoneTypes.Count == 0)
        {
            Debug.LogError("[资源区调试] WorldMapManager.resourceZoneTypes 为空！请先在Inspector中添加ResourceZoneType资产");
            return;
        }

        Debug.Log("[资源区调试] 开始生成测试资源区...");

        // 为每个资源区类型生成一个测试区域
        for (int i = 0; i < wmm.resourceZoneTypes.Count; i++)
        {
            var zt = wmm.resourceZoneTypes[i];
            if (zt == null) continue;

            Vector2Int anchor = new Vector2Int(5 + i * 10, 5);
            Vector2Int size = new Vector2Int(6, 6);

            wmm.SetResourceZoneArea(anchor, size, zt.zoneId);
            Debug.Log($"[资源区调试] 创建了 '{zt.displayName}' 资源区在 {anchor}，大小 {size}");
        }

        Debug.Log($"[资源区调试] 测试资源区生成完成！共生成 {wmm.resourceZoneTypes.Count} 个区域");
        Debug.Log("[资源区调试] 现在请运行 '3. 强制重建资源区可视化' 来查看效果");
    }

    [ContextMenu("5. 完整诊断报告")]
    private void FullDiagnostic()
    {
        Debug.Log("========== 资源区完整诊断报告 ==========");
        CheckWorldMapManager();
        Debug.Log("---");
        CheckVisualizer();
        Debug.Log("========================================");
    }

    [ContextMenu("6. 一键修复（生成数据+重建可视化）")]
    private void QuickFix()
    {
        Debug.Log("[资源区调试] 开始一键修复...");
        GenerateTestResourceZones();
        ForceRebuild();
        Debug.Log("[资源区调试] 一键修复完成！请查看Game视图");
    }
}
