using UnityEngine;

/// <summary>
/// 简单网格渲染器 - 使用 OnDrawGizmos 始终显示网格
/// 兼容所有渲染管线
/// </summary>
public class SimpleGridRenderer : MonoBehaviour
{
    [Header("References")]
    public GridSystem grid;

    [Header("Visual Settings")]
    public Color lineColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
    public float yOffset = 0.1f;
    public bool showOnlyInPlayMode = false;
    public bool showOnlyInBuildMode = false;

    [Header("Optional: Build Mode Hook")]
    public PlacementManager placementManager;

    private void OnDrawGizmos()
    {
        if (grid == null) return;

        // 如果设置了只在Play模式显示
        if (showOnlyInPlayMode && !Application.isPlaying)
            return;

        // 如果设置了只在建造模式显示
        if (showOnlyInBuildMode && placementManager != null)
        {
            if (!Application.isPlaying || !placementManager.BuildMode)
                return;
        }

        DrawGrid();
    }

    private void DrawGrid()
    {
        Gizmos.color = lineColor;

        Vector3 origin = grid.origin;
        float cellSize = grid.cellSize;
        int width = grid.width;
        int height = grid.height;

        float sizeX = width * cellSize;
        float sizeZ = height * cellSize;
        float y = origin.y + yOffset;

        // 绘制竖线（沿 Z 方向）
        for (int x = 0; x <= width; x++)
        {
            float px = origin.x + x * cellSize;
            Vector3 start = new Vector3(px, y, origin.z);
            Vector3 end = new Vector3(px, y, origin.z + sizeZ);
            Gizmos.DrawLine(start, end);
        }

        // 绘制横线（沿 X 方向）
        for (int z = 0; z <= height; z++)
        {
            float pz = origin.z + z * cellSize;
            Vector3 start = new Vector3(origin.x, y, pz);
            Vector3 end = new Vector3(origin.x + sizeX, y, pz);
            Gizmos.DrawLine(start, end);
        }
    }

#if UNITY_EDITOR
    // 在 Scene 视图中也显示（编辑模式）
    private void OnDrawGizmosSelected()
    {
        if (grid == null) return;

        // 选中时用更亮的颜色
        Gizmos.color = new Color(lineColor.r, lineColor.g, lineColor.b, lineColor.a * 1.5f);
        DrawGrid();
    }
#endif
}
