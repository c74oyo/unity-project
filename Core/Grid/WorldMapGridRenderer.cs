using UnityEngine;

/// <summary>
/// WorldMapGridRenderer - 运行时网格可视化
/// 在 Play 模式下显示网格线和鼠标悬停高亮
/// </summary>
[RequireComponent(typeof(WorldMapGrid))]
public class WorldMapGridRenderer : MonoBehaviour
{
    [Header("References")]
    public WorldMapGrid grid;
    public Camera mainCamera;

    [Header("Grid Lines")]
    public bool showGridLines = true;
    public Color gridLineColor = new Color(1f, 1f, 0f, 0.3f); // 黄色半透明
    public float gridLineWidth = 0.05f;

    [Header("Mouse Hover Highlight")]
    public bool showMouseHighlight = true;
    public Color hoverColor = new Color(0f, 1f, 0f, 0.5f); // 绿色半透明
    public float hoverCellHeight = 0.1f; // 高亮方块的高度

    [Header("Base Placement Preview")]
    public bool showPlacementPreview = true;
    public Color validPlacementColor = new Color(0f, 1f, 0f, 0.3f);
    public Color invalidPlacementColor = new Color(1f, 0f, 0f, 0.3f);
    public Vector2Int previewSize = new Vector2Int(3, 3); // 基地占用大小

    [Header("Performance")]
    [Tooltip("网格线采样间隔（每 N 个格子画一条线，1=全画）")]
    [Min(1)] public int gridLineInterval = 1;

    private Vector2Int _currentHoverCell = new Vector2Int(-1, -1);
    private bool _isHoverValid = false;

    // 材质缓存
    private Material _lineMaterial;
    private Material _quadMaterial;

    private void Awake()
    {
        if (grid == null)
            grid = GetComponent<WorldMapGrid>();

        if (mainCamera == null)
            mainCamera = Camera.main;

        CreateMaterials();
    }

    private void Update()
    {
        if (showMouseHighlight)
            UpdateMouseHover();
    }

    private void OnRenderObject()
    {
        if (grid == null) return;

        if (showGridLines)
            RenderGridLines();

        if (showMouseHighlight && _currentHoverCell.x >= 0)
            RenderHoverHighlight();
    }

    /// <summary>
    /// 更新鼠标悬停的网格
    /// </summary>
    private void UpdateMouseHover()
    {
        if (mainCamera == null || grid == null)
        {
            _currentHoverCell = new Vector2Int(-1, -1);
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
        {
            // 将世界坐标转换为网格坐标
            Vector2Int cell = grid.WorldToCell(hit.point);

            if (grid.IsInBounds(cell))
            {
                _currentHoverCell = cell;

                // 检查是否可以放置基地（检查整个区域）
                if (showPlacementPreview)
                {
                    _isHoverValid = grid.CanOccupyArea(cell, previewSize);
                }
                else
                {
                    _isHoverValid = !grid.IsCellOccupied(cell);
                }
            }
            else
            {
                _currentHoverCell = new Vector2Int(-1, -1);
            }
        }
        else
        {
            _currentHoverCell = new Vector2Int(-1, -1);
        }
    }

    /// <summary>
    /// 渲染网格线
    /// </summary>
    private void RenderGridLines()
    {
        if (_lineMaterial == null) return;

        _lineMaterial.SetPass(0);
        GL.PushMatrix();
        GL.MultMatrix(transform.localToWorldMatrix);

        GL.Begin(GL.LINES);
        GL.Color(gridLineColor);

        Vector3 origin = grid.origin;
        float cellSize = grid.cellSize;
        int width = grid.width;
        int height = grid.height;

        // 绘制垂直线（沿 X 轴）
        for (int x = 0; x <= width; x += gridLineInterval)
        {
            Vector3 start = origin + new Vector3(x * cellSize, 0.01f, 0);
            Vector3 end = origin + new Vector3(x * cellSize, 0.01f, height * cellSize);
            GL.Vertex(start);
            GL.Vertex(end);
        }

        // 绘制水平线（沿 Z 轴）
        for (int z = 0; z <= height; z += gridLineInterval)
        {
            Vector3 start = origin + new Vector3(0, 0.01f, z * cellSize);
            Vector3 end = origin + new Vector3(width * cellSize, 0.01f, z * cellSize);
            GL.Vertex(start);
            GL.Vertex(end);
        }

        GL.End();
        GL.PopMatrix();
    }

    /// <summary>
    /// 渲染鼠标悬停高亮
    /// </summary>
    private void RenderHoverHighlight()
    {
        if (_quadMaterial == null) return;

        _quadMaterial.SetPass(0);
        GL.PushMatrix();
        GL.MultMatrix(Matrix4x4.identity);

        GL.Begin(GL.QUADS);

        if (showPlacementPreview)
        {
            // 显示整个基地放置区域（3x3 或其他大小）
            Color color = _isHoverValid ? validPlacementColor : invalidPlacementColor;
            GL.Color(color);

            for (int dx = 0; dx < previewSize.x; dx++)
            {
                for (int dz = 0; dz < previewSize.y; dz++)
                {
                    Vector2Int cell = _currentHoverCell + new Vector2Int(dx, dz);
                    if (grid.IsInBounds(cell))
                    {
                        DrawCellQuad(cell);
                    }
                }
            }
        }
        else
        {
            // 只显示单个格子
            GL.Color(hoverColor);
            DrawCellQuad(_currentHoverCell);
        }

        GL.End();
        GL.PopMatrix();
    }

    /// <summary>
    /// 绘制单个格子的四边形
    /// </summary>
    private void DrawCellQuad(Vector2Int cell)
    {
        Vector3 worldPos = grid.CellToWorldCenter(cell);
        float halfSize = grid.cellSize * 0.5f;
        float y = worldPos.y + hoverCellHeight;

        // 四个顶点（逆时针）
        Vector3 v0 = new Vector3(worldPos.x - halfSize, y, worldPos.z - halfSize);
        Vector3 v1 = new Vector3(worldPos.x + halfSize, y, worldPos.z - halfSize);
        Vector3 v2 = new Vector3(worldPos.x + halfSize, y, worldPos.z + halfSize);
        Vector3 v3 = new Vector3(worldPos.x - halfSize, y, worldPos.z + halfSize);

        GL.Vertex(v0);
        GL.Vertex(v1);
        GL.Vertex(v2);
        GL.Vertex(v3);
    }

    /// <summary>
    /// 创建渲染材质
    /// </summary>
    private void CreateMaterials()
    {
        // 创建简单的无光照材质用于 GL 绘制
        Shader shader = Shader.Find("Hidden/Internal-Colored");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        _lineMaterial = new Material(shader);
        _lineMaterial.hideFlags = HideFlags.HideAndDontSave;
        _lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        _lineMaterial.SetInt("_ZWrite", 0);

        _quadMaterial = new Material(shader);
        _quadMaterial.hideFlags = HideFlags.HideAndDontSave;
        _quadMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _quadMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _quadMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        _quadMaterial.SetInt("_ZWrite", 0);
    }

    private void OnDestroy()
    {
        if (_lineMaterial != null)
            DestroyImmediate(_lineMaterial);
        if (_quadMaterial != null)
            DestroyImmediate(_quadMaterial);
    }

    /// <summary>
    /// 获取当前鼠标悬停的网格坐标
    /// </summary>
    public Vector2Int GetCurrentHoverCell()
    {
        return _currentHoverCell;
    }

    /// <summary>
    /// 检查当前悬停位置是否可以放置
    /// </summary>
    public bool IsCurrentHoverValid()
    {
        return _isHoverValid && _currentHoverCell.x >= 0;
    }
}