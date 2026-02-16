using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战术网格渲染器 - GL绘制网格线和高亮格子（移动范围/攻击范围/选中）
/// 遵循 WorldMapGridRenderer 的 GL 渲染模式，URP 兼容
/// </summary>
public class TacticalGridRenderer : MonoBehaviour
{
    [Header("References")]
    public TacticalGrid grid;

    [Header("Grid Lines")]
    [Tooltip("是否显示网格线")]
    public bool showGridLines = true;

    [Tooltip("网格线颜色")]
    public Color gridLineColor = new(1f, 1f, 1f, 0.15f);

    [Header("Highlight Colors")]
    [Tooltip("可移动范围颜色")]
    public Color walkableColor = new(0.2f, 0.5f, 1f, 0.25f);

    [Tooltip("攻击范围颜色")]
    public Color attackColor = new(1f, 0.2f, 0.2f, 0.3f);

    [Tooltip("选中单位所在格子颜色")]
    public Color selectedUnitColor = new(0.2f, 1f, 0.3f, 0.35f);

    [Tooltip("鼠标悬停格子颜色")]
    public Color hoverColor = new(1f, 1f, 0.3f, 0.3f);

    [Header("Rendering")]
    [Tooltip("高亮层Y轴偏移（防止Z-fighting）")]
    public float highlightHeight = 0.02f;

    // ============ Runtime ============

    private Material _lineMaterial;
    private Material _quadMaterial;

    // 高亮数据
    private HashSet<Vector2Int> _walkableCells = new();
    private HashSet<Vector2Int> _attackCells = new();
    private Vector2Int _selectedUnitCell = new(-1, -1);
    private Vector2Int _hoverCell = new(-1, -1);

    // ============ Lifecycle ============

    private void OnEnable()
    {
        CreateMaterials();
    }

    private void OnDisable()
    {
        if (_lineMaterial != null) DestroyImmediate(_lineMaterial);
        if (_quadMaterial != null) DestroyImmediate(_quadMaterial);
    }

    private void OnRenderObject()
    {
        if (grid == null) return;

        if (showGridLines)
            RenderGridLines();

        RenderHighlights();
    }

    // ============ Public API ============

    /// <summary>
    /// 设置可移动范围高亮
    /// </summary>
    public void SetWalkableHighlight(IEnumerable<Vector2Int> cells)
    {
        _walkableCells.Clear();
        if (cells != null)
        {
            foreach (var c in cells)
                _walkableCells.Add(c);
        }
    }

    /// <summary>
    /// 设置攻击范围高亮
    /// </summary>
    public void SetAttackHighlight(IEnumerable<Vector2Int> cells)
    {
        _attackCells.Clear();
        if (cells != null)
        {
            foreach (var c in cells)
                _attackCells.Add(c);
        }
    }

    /// <summary>
    /// 设置选中单位所在格子
    /// </summary>
    public void SetSelectedUnitCell(Vector2Int cell)
    {
        _selectedUnitCell = cell;
    }

    /// <summary>
    /// 设置鼠标悬停格子
    /// </summary>
    public void SetHoverCell(Vector2Int cell)
    {
        _hoverCell = cell;
    }

    /// <summary>
    /// 清除所有高亮
    /// </summary>
    public void ClearAllHighlights()
    {
        _walkableCells.Clear();
        _attackCells.Clear();
        _selectedUnitCell = new Vector2Int(-1, -1);
        _hoverCell = new Vector2Int(-1, -1);
    }

    // ============ Material Setup ============

    private void CreateMaterials()
    {
        Shader shader = Shader.Find("Hidden/Internal-Colored");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        // Line material
        _lineMaterial = new Material(shader);
        _lineMaterial.hideFlags = HideFlags.HideAndDontSave;
        _lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        _lineMaterial.SetInt("_ZWrite", 0);

        // Quad material
        _quadMaterial = new Material(shader);
        _quadMaterial.hideFlags = HideFlags.HideAndDontSave;
        _quadMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _quadMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _quadMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        _quadMaterial.SetInt("_ZWrite", 0);
    }

    // ============ Grid Lines ============

    private void RenderGridLines()
    {
        if (_lineMaterial == null) return;

        _lineMaterial.SetPass(0);
        GL.PushMatrix();
        GL.MultMatrix(Matrix4x4.identity);

        GL.Begin(GL.LINES);
        GL.Color(gridLineColor);

        float cellSize = grid.CellSize;
        int w = grid.Width;
        int h = grid.Height;

        // 竖线（沿X）
        for (int x = 0; x <= w; x++)
        {
            Vector3 start = new(x * cellSize, 0.01f, 0);
            Vector3 end = new(x * cellSize, 0.01f, h * cellSize);
            GL.Vertex(start);
            GL.Vertex(end);
        }

        // 横线（沿Z）
        for (int z = 0; z <= h; z++)
        {
            Vector3 start = new(0, 0.01f, z * cellSize);
            Vector3 end = new(w * cellSize, 0.01f, z * cellSize);
            GL.Vertex(start);
            GL.Vertex(end);
        }

        GL.End();
        GL.PopMatrix();
    }

    // ============ Cell Highlights ============

    private void RenderHighlights()
    {
        if (_quadMaterial == null) return;

        bool hasAny = _walkableCells.Count > 0 || _attackCells.Count > 0
                      || _selectedUnitCell.x >= 0 || _hoverCell.x >= 0;
        if (!hasAny) return;

        _quadMaterial.SetPass(0);
        GL.PushMatrix();
        GL.MultMatrix(Matrix4x4.identity);

        GL.Begin(GL.QUADS);

        // 1. 可移动范围（蓝色）
        if (_walkableCells.Count > 0)
        {
            GL.Color(walkableColor);
            foreach (var cell in _walkableCells)
            {
                // 不渲染选中单位自身所在格子（用另一个颜色）
                if (cell == _selectedUnitCell) continue;
                DrawCellQuad(cell);
            }
        }

        // 2. 攻击范围（红色）
        if (_attackCells.Count > 0)
        {
            GL.Color(attackColor);
            foreach (var cell in _attackCells)
                DrawCellQuad(cell);
        }

        // 3. 选中单位所在格子（绿色）
        if (_selectedUnitCell.x >= 0)
        {
            GL.Color(selectedUnitColor);
            DrawCellQuad(_selectedUnitCell);
        }

        // 4. 鼠标悬停（黄色） — 不与其他高亮重叠时显示
        if (_hoverCell.x >= 0 && !_walkableCells.Contains(_hoverCell)
            && !_attackCells.Contains(_hoverCell) && _hoverCell != _selectedUnitCell)
        {
            GL.Color(hoverColor);
            DrawCellQuad(_hoverCell);
        }

        GL.End();
        GL.PopMatrix();
    }

    /// <summary>
    /// 绘制单个格子的四边形
    /// </summary>
    private void DrawCellQuad(Vector2Int cell)
    {
        Vector3 center = grid.CellToWorldCenter(cell);
        float half = grid.CellSize * 0.5f;
        float y = highlightHeight;

        Vector3 v0 = new(center.x - half, y, center.z - half);
        Vector3 v1 = new(center.x + half, y, center.z - half);
        Vector3 v2 = new(center.x + half, y, center.z + half);
        Vector3 v3 = new(center.x - half, y, center.z + half);

        GL.Vertex(v0);
        GL.Vertex(v1);
        GL.Vertex(v2);
        GL.Vertex(v3);
    }
}
