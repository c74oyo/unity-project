using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 路径验证结果 — 包含验证是否通过以及失败时的详细信息
/// </summary>
public struct PathValidationResult
{
    public bool isValid;
    public Vector2Int? blockedCell;
    public string blockReason;

    public static PathValidationResult Valid() => new PathValidationResult { isValid = true };

    public static PathValidationResult Invalid(Vector2Int cell, string reason) =>
        new PathValidationResult { isValid = false, blockedCell = cell, blockReason = reason };

    public override string ToString()
    {
        if (isValid) return "Valid";
        return $"Blocked at {blockedCell}: {blockReason}";
    }
}

/// <summary>
/// 道路建造器 - 处理玩家在大地图上建造道路的交互
/// </summary>
public class RoadBuilder : MonoBehaviour
{
    [Header("References")]
    public RoadNetwork roadNetwork;
    public WorldMapManager worldMapManager;
    public Camera mainCamera;

    [Header("Build Settings")]
    [Tooltip("当前选择的道路类型")]
    public RoadType selectedRoadType;

    [Tooltip("建造模式")]
    public bool isBuildMode = false;

    [Header("Preview")]
    [Tooltip("路径预览颜色（可建造）")]
    public Color previewColorValid = new Color(0f, 1f, 0f, 0.5f);

    [Tooltip("路径预览颜色（不可建造）")]
    public Color previewColorInvalid = new Color(1f, 0f, 0f, 0.5f);

    [Tooltip("预览线宽度")]
    public float previewLineWidth = 0.5f;

    [Header("Input")]
    public KeyCode buildModeKey = KeyCode.R;       // 切换建造模式
    public KeyCode cancelKey = KeyCode.Escape;     // 取消
    public KeyCode confirmKey = KeyCode.Return;    // 确认建造
    [Tooltip("地面层（如果未设置，则使用默认射线检测所有层）")]
    public LayerMask groundLayer = -1;             // 默认检测所有层

    // 建造状态
    private Vector2Int? _startCell;
    private Vector2Int? _endCell;
    private List<Vector2Int> _previewPath = new();
    private bool _isPathValid = false;

    // 预览渲染
    private LineRenderer _previewLineRenderer;

    // 事件
    public event System.Action<bool> OnBuildModeChanged;
    public event System.Action<List<Vector2Int>, float> OnPathPreviewUpdated;
    public event System.Action<List<Vector2Int>> OnRoadBuilt;

    // ============ Lifecycle ============

    private void Awake()
    {
        if (roadNetwork == null)
            roadNetwork = FindObjectOfType<RoadNetwork>();

        if (worldMapManager == null)
            worldMapManager = FindObjectOfType<WorldMapManager>();

        if (mainCamera == null)
            mainCamera = Camera.main;

        SetupPreviewLineRenderer();
    }

    private void SetupPreviewLineRenderer()
    {
        _previewLineRenderer = GetComponent<LineRenderer>();
        if (_previewLineRenderer == null)
        {
            _previewLineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        _previewLineRenderer.startWidth = previewLineWidth;
        _previewLineRenderer.endWidth = previewLineWidth;
        _previewLineRenderer.positionCount = 0;
        _previewLineRenderer.enabled = false;

        // 使用URP兼容的材质
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        _previewLineRenderer.material = new Material(shader);
        _previewLineRenderer.material.color = previewColorValid;
    }

    private void Start()
    {
        // 检查必要引用
        if (roadNetwork == null)
            Debug.LogError("[RoadBuilder] RoadNetwork is not assigned!");
        if (worldMapManager == null)
            Debug.LogError("[RoadBuilder] WorldMapManager is not assigned!");
        if (mainCamera == null)
            Debug.LogError("[RoadBuilder] MainCamera is not assigned!");
        if (selectedRoadType == null)
            Debug.LogWarning("[RoadBuilder] No road type selected - you must select a road type to build roads!");

        Debug.Log($"[RoadBuilder] Initialized. Press '{buildModeKey}' to toggle build mode.");
    }

    private void Update()
    {
        // 快捷键由 WorldMapToolbar 统一管理，不再自行切换

        if (!isBuildMode) return;

        // 取消
        if (Input.GetKeyDown(cancelKey))
        {
            CancelBuild();
            return;
        }

        // 获取鼠标指向的格子
        Vector2Int? hoveredCell = GetCellUnderMouse();

        // 左键点击
        if (Input.GetMouseButtonDown(0))
        {
            if (hoveredCell.HasValue)
            {
                Debug.Log($"[RoadBuilder] Click detected at cell: {hoveredCell.Value}");
                HandleClick(hoveredCell.Value);
            }
            else
            {
                Debug.LogWarning("[RoadBuilder] Click detected but no valid cell under mouse!");
            }
        }

        // 右键取消起点
        if (Input.GetMouseButtonDown(1))
        {
            ClearStartPoint();
        }

        // 更新预览
        if (_startCell.HasValue && hoveredCell.HasValue)
        {
            UpdatePathPreview(_startCell.Value, hoveredCell.Value);
        }

        // 确认键建造
        if (Input.GetKeyDown(confirmKey) && _isPathValid && _previewPath.Count > 0)
        {
            ConfirmBuild();
        }
    }

    // ============ Build Mode ============

    public void ToggleBuildMode()
    {
        SetBuildMode(!isBuildMode);
    }

    public void SetBuildMode(bool enabled)
    {
        isBuildMode = enabled;

        if (!isBuildMode)
        {
            ClearPreview();
        }

        OnBuildModeChanged?.Invoke(isBuildMode);
        Debug.Log($"[RoadBuilder] Build mode: {(isBuildMode ? "ON" : "OFF")}");
    }

    public void SetSelectedRoadType(RoadType roadType)
    {
        selectedRoadType = roadType;
        // 重新计算预览
        if (_startCell.HasValue && _endCell.HasValue)
        {
            UpdatePathPreview(_startCell.Value, _endCell.Value);
        }
    }

    // ============ Input Handling ============

    private Vector2Int? GetCellUnderMouse()
    {
        if (mainCamera == null || worldMapManager == null)
            return null;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        // 首先尝试使用物理射线检测
        if (groundLayer.value != 0 && Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayer))
        {
            Vector2Int cell = worldMapManager.WorldToCell(hit.point);
            if (worldMapManager.IsInBounds(cell))
            {
                return cell;
            }
        }

        // 如果物理检测失败，使用平面检测作为后备方案
        // 假设地面在Y=0平面
        Plane groundPlane = new Plane(Vector3.up, worldMapManager.origin);
        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 worldPoint = ray.GetPoint(distance);
            Vector2Int cell = worldMapManager.WorldToCell(worldPoint);
            if (worldMapManager.IsInBounds(cell))
            {
                return cell;
            }
        }

        return null;
    }

    private void HandleClick(Vector2Int cell)
    {
        if (!_startCell.HasValue)
        {
            // 检查起点是否可建路
            bool canBuild = roadNetwork != null && roadNetwork.CanBuildRoadAt(cell);
            bool hasRoad = roadNetwork != null && roadNetwork.HasRoadAt(cell);

            if (!canBuild && !hasRoad)
            {
                // 打印详细原因
                string reason = GetCellBlockReason(cell);
                Debug.LogWarning($"[RoadBuilder] Cannot set start at {cell}: {reason}");
                return;
            }

            // 设置起点
            _startCell = cell;
            Debug.Log($"[RoadBuilder] Start point set: {cell} (hasRoad={hasRoad})");
        }
        else
        {
            // 设置终点并尝试建造
            _endCell = cell;
            if (_isPathValid && _previewPath.Count > 0)
            {
                ConfirmBuild();
            }
            else
            {
                string detail = _lastValidationResult.isValid
                    ? $"PathCount={_previewPath.Count}"
                    : _lastValidationResult.ToString();
                Debug.LogWarning($"[RoadBuilder] Cannot build from {_startCell.Value} to {cell}: {detail}");
            }
        }
    }

    /// <summary>
    /// 获取格子不能建路的原因
    /// </summary>
    private string GetCellBlockReason(Vector2Int cell)
    {
        if (worldMapManager == null) return "WorldMapManager is null";
        if (!worldMapManager.IsInBounds(cell)) return "Out of bounds";

        var data = worldMapManager.GetCellData(cell);
        if (data == null) return "No cell data";

        if (data.zoneState == WorldMapCellData.ZoneState.Unbuildable)
            return $"Unbuildable zone";

        if (data.occupation != WorldMapCellData.OccupationType.None &&
            data.occupation != WorldMapCellData.OccupationType.Road)
            return $"Occupied by {data.occupation} (id={data.occupationId})";

        return "Unknown reason";
    }

    // ============ Path Preview ============

    // 缓存最近一次的验证结果，供 UI/日志使用
    private PathValidationResult _lastValidationResult;

    private void UpdatePathPreview(Vector2Int start, Vector2Int end)
    {
        _previewPath.Clear();
        _isPathValid = false;

        if (selectedRoadType == null)
        {
            ClearPreviewVisual();
            Debug.LogWarning("[RoadBuilder] No road type selected! Assign a RoadType in Inspector.");
            return;
        }

        // 计算路径（简单的直线/L形路径）
        _previewPath = CalculateSimplePath(start, end);

        // 检查路径有效性
        _lastValidationResult = ValidatePath(_previewPath);
        _isPathValid = _lastValidationResult.isValid;

        if (!_isPathValid)
        {
            Debug.LogWarning($"[RoadBuilder] Path invalid: {_lastValidationResult}");
        }

        // 计算成本
        float totalCost = 0f;
        if (_isPathValid && roadNetwork != null)
        {
            totalCost = roadNetwork.CalculatePathBuildCost(_previewPath, selectedRoadType.roadTypeId);
        }

        // 更新视觉预览
        UpdatePreviewVisual();

        // 触发事件
        OnPathPreviewUpdated?.Invoke(_previewPath, totalCost);
    }

    /// <summary>
    /// 计算简单路径（先水平后垂直）
    /// </summary>
    private List<Vector2Int> CalculateSimplePath(Vector2Int start, Vector2Int end)
    {
        var path = new List<Vector2Int>();

        int x = start.x;
        int y = start.y;

        // 先水平移动
        int dx = end.x > start.x ? 1 : -1;
        while (x != end.x)
        {
            path.Add(new Vector2Int(x, y));
            x += dx;
        }

        // 再垂直移动
        int dy = end.y > start.y ? 1 : -1;
        while (y != end.y)
        {
            path.Add(new Vector2Int(x, y));
            y += dy;
        }

        // 添加终点
        path.Add(end);

        return path;
    }

    /// <summary>
    /// 验证路径是否可建造，返回结构化验证结果
    /// </summary>
    private PathValidationResult ValidatePath(List<Vector2Int> path)
    {
        if (roadNetwork == null)
            return PathValidationResult.Invalid(default, "RoadNetwork is null");

        foreach (var cell in path)
        {
            // 检查是否已有道路（已有道路的格子可以跳过）
            if (roadNetwork.HasRoadAt(cell))
                continue;

            // 检查是否可以建造
            if (!roadNetwork.CanBuildRoadAt(cell))
            {
                string reason = GetCellBlockReason(cell);
                return PathValidationResult.Invalid(cell, reason);
            }
        }
        return PathValidationResult.Valid();
    }

    private void UpdatePreviewVisual()
    {
        if (_previewLineRenderer == null || _previewPath.Count == 0)
        {
            if (_previewLineRenderer != null)
                _previewLineRenderer.enabled = false;
            return;
        }

        _previewLineRenderer.enabled = true;
        _previewLineRenderer.positionCount = _previewPath.Count;

        Color color = _isPathValid ? previewColorValid : previewColorInvalid;
        _previewLineRenderer.startColor = color;
        _previewLineRenderer.endColor = color;
        // URP Unlit 材质需要通过 material.color 设置颜色
        if (_previewLineRenderer.material != null)
            _previewLineRenderer.material.color = color;

        for (int i = 0; i < _previewPath.Count; i++)
        {
            Vector3 worldPos = worldMapManager.CellToWorldCenter(_previewPath[i]);
            worldPos.y = 0.2f; // 稍微抬高显示
            _previewLineRenderer.SetPosition(i, worldPos);
        }
    }

    /// <summary>
    /// 只清除视觉预览（LineRenderer + 路径数据），不重置建造状态
    /// </summary>
    private void ClearPreviewVisual()
    {
        _previewPath.Clear();
        _isPathValid = false;

        if (_previewLineRenderer != null)
        {
            _previewLineRenderer.enabled = false;
            _previewLineRenderer.positionCount = 0;
        }
    }

    /// <summary>
    /// 完全清除：视觉预览 + 建造状态（起点/终点）
    /// </summary>
    private void ClearPreview()
    {
        _startCell = null;
        _endCell = null;
        ClearPreviewVisual();
    }

    // ============ Build Actions ============

    private void ConfirmBuild()
    {
        if (!_isPathValid || _previewPath.Count == 0 || selectedRoadType == null)
        {
            Debug.LogWarning($"[RoadBuilder] ConfirmBuild aborted: PathValid={_isPathValid}, " +
                $"PathCount={_previewPath.Count}, RoadType={selectedRoadType?.displayName ?? "NULL"}");
            return;
        }

        // 过滤掉已有道路的格子
        var cellsToBuild = new List<Vector2Int>();
        foreach (var cell in _previewPath)
        {
            if (!roadNetwork.HasRoadAt(cell))
            {
                cellsToBuild.Add(cell);
            }
        }

        if (cellsToBuild.Count == 0)
        {
            Debug.Log("[RoadBuilder] All cells already have roads");
            ClearPreview();
            return;
        }

        // 检查资源/金钱是否足够
        if (BaseManager.Instance != null)
        {
            // 获取当前激活基地
            var activeBase = BaseManager.Instance.GetActiveBaseSaveData();
            if (activeBase == null)
            {
                // 无激活基地时，使用第一个基地
                var allBases = BaseManager.Instance.AllBaseSaveData;
                if (allBases.Count > 0) activeBase = allBases[0];
            }

            if (activeBase != null)
            {
                // 1) 检查金钱
                float totalMoneyCost = roadNetwork.CalculatePathBuildCost(cellsToBuild, selectedRoadType.roadTypeId);
                if (activeBase.money < totalMoneyCost)
                {
                    Debug.LogWarning($"[RoadBuilder] Not enough money! Need ${totalMoneyCost:F0}, have ${activeBase.money:F0}");
                    return;
                }

                // 2) 检查资源
                if (selectedRoadType.buildCost != null)
                {
                    foreach (var cost in selectedRoadType.buildCost)
                    {
                        if (cost.res == null) continue;
                        int totalNeeded = cost.amount * cellsToBuild.Count;
                        float available = activeBase.GetResourceAmount(cost.res.id);
                        if (available < totalNeeded)
                        {
                            Debug.LogWarning($"[RoadBuilder] Not enough {cost.res.displayName}! Need {totalNeeded}, have {available:F0}");
                            return;
                        }
                    }
                }

                // 扣除金钱
                activeBase.money -= totalMoneyCost;

                // 扣除资源
                if (selectedRoadType.buildCost != null)
                {
                    foreach (var cost in selectedRoadType.buildCost)
                    {
                        if (cost.res == null) continue;
                        int totalNeeded = cost.amount * cellsToBuild.Count;
                        activeBase.TryConsumeResource(cost.res.id, totalNeeded);
                    }
                }

                // 保存变更
                BaseManager.Instance.UpdateBaseSaveData(activeBase);
            }
        }

        // 建造道路
        bool success = roadNetwork.TryBuildRoadPath(cellsToBuild, selectedRoadType.roadTypeId);

        if (success)
        {
            Debug.Log($"[RoadBuilder] Built {cellsToBuild.Count} road segments");
            OnRoadBuilt?.Invoke(cellsToBuild);
        }
        else
        {
            Debug.LogWarning("[RoadBuilder] Failed to build road, refunding costs");

            // 建造失败，退还已扣除的金钱和资源
            if (BaseManager.Instance != null)
            {
                var refundBase = BaseManager.Instance.GetActiveBaseSaveData();
                if (refundBase == null)
                {
                    var allBases = BaseManager.Instance.AllBaseSaveData;
                    if (allBases.Count > 0) refundBase = allBases[0];
                }

                if (refundBase != null)
                {
                    float totalMoneyCost = roadNetwork.CalculatePathBuildCost(cellsToBuild, selectedRoadType.roadTypeId);
                    refundBase.money += totalMoneyCost;

                    if (selectedRoadType.buildCost != null)
                    {
                        foreach (var cost in selectedRoadType.buildCost)
                        {
                            if (cost.res == null) continue;
                            int totalNeeded = cost.amount * cellsToBuild.Count;
                            refundBase.AddResource(cost.res.id, totalNeeded);
                        }
                    }

                    BaseManager.Instance.UpdateBaseSaveData(refundBase);
                }
            }
        }

        ClearPreview();
    }

    private void CancelBuild()
    {
        ClearPreview();
        // 通知 Toolbar 退出（如果存在），否则直接关闭
        if (WorldMapToolbar.Instance != null)
            WorldMapToolbar.Instance.SetMode(WorldMapToolbar.ToolMode.None);
        else
            SetBuildMode(false);
    }

    private void ClearStartPoint()
    {
        _startCell = null;
        _endCell = null;
        _previewPath.Clear();
        _isPathValid = false;
        UpdatePreviewVisual();
    }

    // ============ Public Methods ============

    /// <summary>
    /// 获取当前预览路径
    /// </summary>
    public List<Vector2Int> GetPreviewPath()
    {
        return new List<Vector2Int>(_previewPath);
    }

    /// <summary>
    /// 获取预览路径的建造成本
    /// </summary>
    public float GetPreviewPathCost()
    {
        if (!_isPathValid || selectedRoadType == null || roadNetwork == null)
            return 0f;

        return roadNetwork.CalculatePathBuildCost(_previewPath, selectedRoadType.roadTypeId);
    }

    /// <summary>
    /// 是否有有效的预览路径
    /// </summary>
    public bool HasValidPreview => _isPathValid && _previewPath.Count > 0;

    /// <summary>
    /// 获取最近一次路径验证结果（包含阻塞位置和原因）
    /// </summary>
    public PathValidationResult LastValidationResult => _lastValidationResult;

    // OnGUI debug panel removed — replaced by WorldMapToolbar UI

    // ============ Debug Gizmos ============
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!isBuildMode) return;

        // 绘制起点
        if (_startCell.HasValue && worldMapManager != null)
        {
            Vector3 startPos = worldMapManager.CellToWorldCenter(_startCell.Value);
            startPos.y = 0.3f;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(startPos, worldMapManager.cellSize * 0.3f);
        }
    }
#endif
}
