using UnityEngine;

/// <summary>
/// 自动配置摄像机初始位置，适配地形大小
/// </summary>
public class CameraInitializer : MonoBehaviour
{
    [Header("地形配置")]
    [Tooltip("地形的网格尺寸（例如 100×100）")]
    public Vector2Int terrainSize = new Vector2Int(100, 100);

    [Header("摄像机引用")]
    public BuildCameraController cameraController;

    [Header("初始位置设置")]
    [Tooltip("是否在启动时自动居中")]
    public bool centerOnStart = true;

    [Tooltip("初始缩放距离（相机高度）")]
    [Range(20f, 100f)]
    public float initialZoomDistance = 45f;

    [Tooltip("是否自动调整缩放范围")]
    public bool autoAdjustZoomLimits = true;

    void Start()
    {
        if (cameraController == null)
        {
            cameraController = FindObjectOfType<BuildCameraController>();
            if (cameraController == null)
            {
                Debug.LogError("[CameraInitializer] Cannot find BuildCameraController!");
                return;
            }
        }

        if (centerOnStart)
        {
            CenterCamera();
        }

        if (autoAdjustZoomLimits)
        {
            AdjustZoomLimits();
        }

        // 应用初始缩放
        SetZoomDistance(initialZoomDistance);
    }

    /// <summary>
    /// 将摄像机居中到地形中心
    /// </summary>
    [ContextMenu("居中摄像机")]
    public void CenterCamera()
    {
        if (cameraController == null) return;

        // 计算地形中心（假设地形从 (0, 0) 开始）
        Vector3 center = new Vector3(terrainSize.x * 0.5f, 0f, terrainSize.y * 0.5f);

        // 保持当前 Y 轴高度
        center.y = cameraController.transform.position.y;

        cameraController.JumpTo(center);

        Debug.Log($"[CameraInitializer] Camera centered at {center}");
    }

    /// <summary>
    /// 根据地形大小自动调整缩放范围
    /// </summary>
    [ContextMenu("自动调整缩放范围")]
    public void AdjustZoomLimits()
    {
        if (cameraController == null) return;

        // 根据地形大小计算合适的缩放范围
        float maxDimension = Mathf.Max(terrainSize.x, terrainSize.y);

        // 最小距离：能看清单个建筑（2-3个格子）
        float recommendedMin = Mathf.Max(8f, maxDimension * 0.08f);

        // 最大距离：能看到整个地形的 80%
        float recommendedMax = Mathf.Max(60f, maxDimension * 1.2f);

        // 初始距离：能看到 30-40% 的地形
        float recommendedStart = Mathf.Clamp(maxDimension * 0.45f, recommendedMin, recommendedMax);

        // 应用设置
        cameraController.minDistance = recommendedMin;
        cameraController.maxDistance = recommendedMax;
        cameraController.startDistance = recommendedStart;

        Debug.Log($"[CameraInitializer] Zoom limits adjusted:");
        Debug.Log($"  Min: {recommendedMin:F1}  Start: {recommendedStart:F1}  Max: {recommendedMax:F1}");
    }

    /// <summary>
    /// 设置摄像机缩放距离
    /// </summary>
    public void SetZoomDistance(float distance)
    {
        if (cameraController == null) return;

        // 通过反射访问私有字段（因为 distance 是私有的）
        var field = typeof(BuildCameraController).GetField("distance",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (field != null)
        {
            distance = Mathf.Clamp(distance, cameraController.minDistance, cameraController.maxDistance);
            field.SetValue(cameraController, distance);

            // 触发更新
            var method = typeof(BuildCameraController).GetMethod("ApplyZoomImmediate",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method?.Invoke(cameraController, null);

            Debug.Log($"[CameraInitializer] Zoom distance set to {distance:F1}");
        }
    }

    /// <summary>
    /// 移动到指定网格位置
    /// </summary>
    public void MoveToGridPosition(Vector2Int gridPos)
    {
        if (cameraController == null) return;

        Vector3 worldPos = new Vector3(gridPos.x, 0f, gridPos.y);
        cameraController.MoveTo(worldPos);
    }

    // Editor 辅助功能
    void OnDrawGizmosSelected()
    {
        // 绘制地形边界
        Gizmos.color = Color.yellow;
        Vector3 center = new Vector3(terrainSize.x * 0.5f, 0f, terrainSize.y * 0.5f);
        Vector3 size = new Vector3(terrainSize.x, 1f, terrainSize.y);
        Gizmos.DrawWireCube(center, size);

        // 绘制中心点
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(center, 2f);
    }
}
