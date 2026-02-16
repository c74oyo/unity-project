using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 道路选择器 - 处理道路的点选和框选
/// </summary>
public class RoadSelector : MonoBehaviour
{
    public static RoadSelector Instance { get; private set; }

    [Header("References")]
    public RoadNetwork roadNetwork;
    public WorldMapManager worldMapManager;
    public Camera mainCamera;

    [Header("Selection Settings")]
    [Tooltip("选中时的高亮颜色")]
    public Color selectionColor = new Color(0f, 1f, 1f, 0.6f);

    [Tooltip("悬停时的高亮颜色")]
    public Color hoverColor = new Color(1f, 1f, 0f, 0.4f);

    [Tooltip("是否启用选择功能（由 WorldMapToolbar 控制，开局默认关闭）")]
    public bool isSelectionEnabled = false;

    [Header("Input")]
    public KeyCode addToSelectionKey = KeyCode.LeftShift;  // 按住添加到选择
    public KeyCode clearSelectionKey = KeyCode.Escape;     // 清除选择

    // 选中的道路格子
    private HashSet<Vector2Int> _selectedCells = new();
    private Vector2Int? _hoveredCell;

    // 框选状态
    private bool _isBoxSelecting = false;
    private Vector2 _boxSelectStart;
    private Vector2 _boxSelectEnd;

    // 事件
    public event Action<HashSet<Vector2Int>> OnSelectionChanged;

    // ============ Properties ============

    /// <summary>
    /// 当前选中的格子数量
    /// </summary>
    public int SelectionCount => _selectedCells.Count;

    /// <summary>
    /// 是否有选中的道路
    /// </summary>
    public bool HasSelection => _selectedCells.Count > 0;

    /// <summary>
    /// 获取选中的格子（只读副本）
    /// </summary>
    public HashSet<Vector2Int> SelectedCells => new HashSet<Vector2Int>(_selectedCells);

    // ============ Lifecycle ============

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (roadNetwork == null)
            roadNetwork = FindObjectOfType<RoadNetwork>();
        if (worldMapManager == null)
            worldMapManager = FindObjectOfType<WorldMapManager>();
        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!isSelectionEnabled) return;

        // 更新悬停格子
        UpdateHoveredCell();

        // ESC 退出选择模式（通知 Toolbar）
        if (Input.GetKeyDown(clearSelectionKey))
        {
            ClearSelection();
            if (WorldMapToolbar.Instance != null)
                WorldMapToolbar.Instance.SetMode(WorldMapToolbar.ToolMode.None);
            return;
        }

        // 检测是否正在建造模式（道路建造模式下不启用选择）
        if (WorldMapBuildModeController.Instance != null &&
            WorldMapBuildModeController.Instance.IsInRoadBuildMode)
        {
            return;
        }

        // 框选逻辑
        if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
        {
            _boxSelectStart = Input.mousePosition;
            _isBoxSelecting = false; // 开始时不确定是点选还是框选
        }

        if (Input.GetMouseButton(0) && !IsPointerOverUI())
        {
            // 检测是否开始框选（拖动超过阈值）
            if (!_isBoxSelecting)
            {
                float dragDistance = Vector2.Distance(_boxSelectStart, Input.mousePosition);
                if (dragDistance > 10f) // 10像素阈值
                {
                    _isBoxSelecting = true;
                }
            }

            if (_isBoxSelecting)
            {
                _boxSelectEnd = Input.mousePosition;
            }
        }

        if (Input.GetMouseButtonUp(0) && !IsPointerOverUI())
        {
            bool addToSelection = Input.GetKey(addToSelectionKey);

            if (_isBoxSelecting)
            {
                // 完成框选
                EndBoxSelect(addToSelection);
            }
            else
            {
                // 点选
                if (_hoveredCell.HasValue && roadNetwork != null && roadNetwork.HasRoadAt(_hoveredCell.Value))
                {
                    SelectCell(_hoveredCell.Value, addToSelection);
                }
                else if (!addToSelection)
                {
                    // 点击空白区域，清除选择
                    ClearSelection();
                }
            }

            _isBoxSelecting = false;
        }
    }

    // ============ Hover ============

    private void UpdateHoveredCell()
    {
        _hoveredCell = GetCellUnderMouse();
    }

    private Vector2Int? GetCellUnderMouse()
    {
        if (mainCamera == null || worldMapManager == null)
            return null;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, worldMapManager.origin);

        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 worldPoint = ray.GetPoint(distance);
            Vector2Int cell = worldMapManager.WorldToCell(worldPoint);

            if (worldMapManager.IsInBounds(cell))
                return cell;
        }

        return null;
    }

    // ============ Selection ============

    /// <summary>
    /// 选择单个格子
    /// </summary>
    public void SelectCell(Vector2Int cell, bool addToSelection = false)
    {
        if (!addToSelection)
        {
            _selectedCells.Clear();
        }

        if (roadNetwork != null && roadNetwork.HasRoadAt(cell))
        {
            if (_selectedCells.Contains(cell))
            {
                // 如果已选中，则取消选择（切换）
                _selectedCells.Remove(cell);
            }
            else
            {
                _selectedCells.Add(cell);
            }
        }

        OnSelectionChanged?.Invoke(_selectedCells);
    }

    /// <summary>
    /// 选择多个格子
    /// </summary>
    public void SelectCells(IEnumerable<Vector2Int> cells, bool addToSelection = false)
    {
        if (!addToSelection)
        {
            _selectedCells.Clear();
        }

        if (roadNetwork != null)
        {
            foreach (var cell in cells)
            {
                if (roadNetwork.HasRoadAt(cell))
                {
                    _selectedCells.Add(cell);
                }
            }
        }

        OnSelectionChanged?.Invoke(_selectedCells);
    }

    /// <summary>
    /// 清除所有选择
    /// </summary>
    public void ClearSelection()
    {
        if (_selectedCells.Count > 0)
        {
            _selectedCells.Clear();
            OnSelectionChanged?.Invoke(_selectedCells);
        }
    }

    // ============ Box Selection ============

    private void EndBoxSelect(bool addToSelection)
    {
        if (worldMapManager == null || roadNetwork == null) return;

        // 获取框选区域内的所有格子
        var cellsInBox = GetCellsInScreenRect(_boxSelectStart, _boxSelectEnd);

        // 过滤出有道路的格子
        var roadCells = new List<Vector2Int>();
        foreach (var cell in cellsInBox)
        {
            if (roadNetwork.HasRoadAt(cell))
            {
                roadCells.Add(cell);
            }
        }

        SelectCells(roadCells, addToSelection);
    }

    private List<Vector2Int> GetCellsInScreenRect(Vector2 screenStart, Vector2 screenEnd)
    {
        var result = new List<Vector2Int>();
        if (mainCamera == null || worldMapManager == null) return result;

        // 计算屏幕矩形
        float minX = Mathf.Min(screenStart.x, screenEnd.x);
        float maxX = Mathf.Max(screenStart.x, screenEnd.x);
        float minY = Mathf.Min(screenStart.y, screenEnd.y);
        float maxY = Mathf.Max(screenStart.y, screenEnd.y);

        // 将屏幕四角转换为世界坐标
        Plane groundPlane = new Plane(Vector3.up, worldMapManager.origin);

        Vector2Int? minCell = ScreenToCell(new Vector2(minX, minY), groundPlane);
        Vector2Int? maxCell = ScreenToCell(new Vector2(maxX, maxY), groundPlane);

        if (!minCell.HasValue || !maxCell.HasValue) return result;

        // 扩展范围以确保覆盖边缘
        int cellMinX = Mathf.Min(minCell.Value.x, maxCell.Value.x) - 1;
        int cellMaxX = Mathf.Max(minCell.Value.x, maxCell.Value.x) + 1;
        int cellMinY = Mathf.Min(minCell.Value.y, maxCell.Value.y) - 1;
        int cellMaxY = Mathf.Max(minCell.Value.y, maxCell.Value.y) + 1;

        // 遍历范围内的格子，检查是否在屏幕矩形内
        for (int x = cellMinX; x <= cellMaxX; x++)
        {
            for (int y = cellMinY; y <= cellMaxY; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (!worldMapManager.IsInBounds(cell)) continue;

                // 检查格子中心是否在屏幕矩形内
                Vector3 worldPos = worldMapManager.CellToWorldCenter(cell);
                Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

                if (screenPos.x >= minX && screenPos.x <= maxX &&
                    screenPos.y >= minY && screenPos.y <= maxY &&
                    screenPos.z > 0) // 确保在相机前方
                {
                    result.Add(cell);
                }
            }
        }

        return result;
    }

    private Vector2Int? ScreenToCell(Vector2 screenPos, Plane groundPlane)
    {
        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 worldPoint = ray.GetPoint(distance);
            return worldMapManager.WorldToCell(worldPoint);
        }
        return null;
    }

    // ============ Selection Info ============

    /// <summary>
    /// 获取选中道路的统计信息
    /// </summary>
    public RoadSelectionInfo GetSelectionInfo()
    {
        var info = new RoadSelectionInfo();
        info.totalCells = _selectedCells.Count;

        if (_selectedCells.Count == 0 || roadNetwork == null)
            return info;

        float totalDurability = 0f;
        int durabilityCount = 0;

        foreach (var cell in _selectedCells)
        {
            var segment = roadNetwork.GetRoadAt(cell);
            if (segment == null) continue;

            // 统计道路类型
            if (!info.roadTypeCounts.ContainsKey(segment.roadTypeId))
                info.roadTypeCounts[segment.roadTypeId] = 0;
            info.roadTypeCounts[segment.roadTypeId]++;

            // 统计耐久度
            totalDurability += segment.durability;
            durabilityCount++;

            // 统计损坏
            if (segment.GetDamageLevel() == RoadSegment.DamageLevel.Broken)
                info.brokenCount++;

            // 计算修复成本
            var roadType = roadNetwork.GetRoadType(segment.roadTypeId);
            if (roadType != null)
            {
                info.totalRepairCost += segment.CalculateRepairCost(roadType);
            }
        }

        info.averageDurability = durabilityCount > 0 ? totalDurability / durabilityCount : 0f;

        return info;
    }

    // ============ Utility ============

    private bool IsPointerOverUI()
    {
        return UnityEngine.EventSystems.EventSystem.current != null &&
               UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
    }

    // ============ Visualization ============

    private void OnGUI()
    {
        // 绘制框选矩形
        if (_isBoxSelecting)
        {
            DrawScreenRect(_boxSelectStart, _boxSelectEnd, new Color(0, 1, 1, 0.25f), new Color(0, 1, 1, 0.8f));
        }
    }

    private void DrawScreenRect(Vector2 start, Vector2 end, Color fillColor, Color borderColor)
    {
        // 计算矩形
        float x = Mathf.Min(start.x, end.x);
        float y = Screen.height - Mathf.Max(start.y, end.y);
        float width = Mathf.Abs(end.x - start.x);
        float height = Mathf.Abs(end.y - start.y);

        Rect rect = new Rect(x, y, width, height);

        // 绘制填充
        GUI.color = fillColor;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);

        // 绘制边框
        GUI.color = borderColor;
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - 2, rect.width, 2), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y, 2, rect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x + rect.width - 2, rect.y, 2, rect.height), Texture2D.whiteTexture);

        GUI.color = Color.white;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (worldMapManager == null) return;

        // 绘制选中的格子
        Gizmos.color = selectionColor;
        foreach (var cell in _selectedCells)
        {
            Vector3 center = worldMapManager.CellToWorldCenter(cell);
            center.y = 0.15f;
            float size = worldMapManager.cellSize * 0.9f;
            Gizmos.DrawCube(center, new Vector3(size, 0.1f, size));
        }

        // 绘制悬停的格子
        if (_hoveredCell.HasValue && roadNetwork != null && roadNetwork.HasRoadAt(_hoveredCell.Value))
        {
            Gizmos.color = hoverColor;
            Vector3 center = worldMapManager.CellToWorldCenter(_hoveredCell.Value);
            center.y = 0.2f;
            float size = worldMapManager.cellSize * 0.85f;
            Gizmos.DrawCube(center, new Vector3(size, 0.05f, size));
        }
    }
#endif
}

/// <summary>
/// 道路选择统计信息
/// </summary>
[Serializable]
public class RoadSelectionInfo
{
    public int totalCells = 0;
    public Dictionary<string, int> roadTypeCounts = new();
    public float averageDurability = 100f;
    public int brokenCount = 0;
    public float totalRepairCost = 0f;

    /// <summary>
    /// 获取指定道路类型的数量
    /// </summary>
    public int GetTypeCount(string roadTypeId)
    {
        return roadTypeCounts.TryGetValue(roadTypeId, out int count) ? count : 0;
    }

    /// <summary>
    /// 是否有损坏的道路
    /// </summary>
    public bool HasDamaged => averageDurability < 100f;

    /// <summary>
    /// 是否有完全损坏的道路
    /// </summary>
    public bool HasBroken => brokenCount > 0;
}
