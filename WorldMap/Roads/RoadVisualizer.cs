using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 道路可视化器 - 在大地图上渲染道路
/// </summary>
public class RoadVisualizer : MonoBehaviour
{
    [Header("References")]
    public RoadNetwork roadNetwork;
    public WorldMapManager worldMapManager;

    [Header("Default Materials")]
    public Material defaultRoadMaterial;
    public float roadHeight = 0.05f;

    [Header("Settings")]
    [Tooltip("是否在运行时动态更新")]
    public bool dynamicUpdate = true;

    [Tooltip("是否使用道路预制体（如果有）")]
    public bool useRoadPrefabs = true;

    // Runtime
    private Dictionary<Vector2Int, GameObject> _roadVisuals = new();
    private Transform _roadContainer;

    // ============ Lifecycle ============

    private void Awake()
    {
        // 创建道路容器
        _roadContainer = new GameObject("RoadContainer").transform;
        _roadContainer.SetParent(transform);

        if (roadNetwork == null)
            roadNetwork = FindObjectOfType<RoadNetwork>();

        if (worldMapManager == null)
            worldMapManager = FindObjectOfType<WorldMapManager>();
    }

    private void Start()
    {
        // 初始渲染所有道路
        RebuildAllRoads();

        // 订阅道路事件
        if (roadNetwork != null && dynamicUpdate)
        {
            roadNetwork.OnRoadBuilt += OnRoadBuilt;
            roadNetwork.OnRoadRemoved += OnRoadRemoved;
            roadNetwork.OnRoadUpgraded += OnRoadUpgraded;
        }
    }

    private void OnDestroy()
    {
        // 取消订阅
        if (roadNetwork != null)
        {
            roadNetwork.OnRoadBuilt -= OnRoadBuilt;
            roadNetwork.OnRoadRemoved -= OnRoadRemoved;
            roadNetwork.OnRoadUpgraded -= OnRoadUpgraded;
        }
    }

    // ============ Event Handlers ============

    private void OnRoadBuilt(Vector2Int cell, RoadSegment segment)
    {
        CreateRoadVisual(cell, segment);

        // 更新相邻道路的视觉（连接可能变化）
        UpdateAdjacentRoadVisuals(cell);
    }

    private void OnRoadRemoved(Vector2Int cell)
    {
        RemoveRoadVisual(cell);

        // 更新相邻道路的视觉
        UpdateAdjacentRoadVisuals(cell);
    }

    private void OnRoadUpgraded(Vector2Int cell, RoadSegment segment)
    {
        // 重新创建视觉效果
        RemoveRoadVisual(cell);
        CreateRoadVisual(cell, segment);
    }

    // ============ Visual Creation ============

    /// <summary>
    /// 重建所有道路视觉
    /// </summary>
    public void RebuildAllRoads()
    {
        // 清除所有现有视觉
        ClearAllVisuals();

        if (roadNetwork == null) return;

        // 创建所有道路的视觉
        var allRoads = roadNetwork.GetAllRoads();
        foreach (var segment in allRoads)
        {
            CreateRoadVisual(segment.cell, segment);
        }
    }

    /// <summary>
    /// 清除所有道路视觉
    /// </summary>
    public void ClearAllVisuals()
    {
        foreach (var kvp in _roadVisuals)
        {
            if (kvp.Value != null)
                Destroy(kvp.Value);
        }
        _roadVisuals.Clear();
    }

    /// <summary>
    /// 创建单个道路视觉
    /// </summary>
    private void CreateRoadVisual(Vector2Int cell, RoadSegment segment)
    {
        if (worldMapManager == null) return;

        // 如果已存在，先移除
        RemoveRoadVisual(cell);

        var roadType = roadNetwork?.GetRoadType(segment.roadTypeId);
        GameObject visual = null;

        // 尝试使用预制体
        if (useRoadPrefabs && roadType != null && roadType.roadPrefab != null)
        {
            visual = Instantiate(roadType.roadPrefab, _roadContainer);
        }
        else
        {
            // 使用默认quad
            visual = CreateDefaultRoadVisual(segment, roadType);
        }

        if (visual == null) return;

        // 设置位置
        Vector3 worldPos = worldMapManager.CellToWorldCenter(cell);
        worldPos.y = roadHeight;
        visual.transform.position = worldPos;

        // 存储引用
        _roadVisuals[cell] = visual;
    }

    /// <summary>
    /// 创建默认道路视觉（使用quad）
    /// </summary>
    private GameObject CreateDefaultRoadVisual(RoadSegment segment, RoadType roadType)
    {
        var roadGO = new GameObject($"Road_{segment.cell.x}_{segment.cell.y}");
        roadGO.transform.SetParent(_roadContainer);

        // 创建主体
        var mainQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        mainQuad.transform.SetParent(roadGO.transform);
        mainQuad.transform.localRotation = Quaternion.Euler(90, 0, 0);

        float cellSize = worldMapManager != null ? worldMapManager.cellSize : 10f;

        // 道路占满整格
        mainQuad.transform.localScale = new Vector3(cellSize, cellSize, 1);
        mainQuad.transform.localPosition = Vector3.zero;

        // 设置材质 - 使用Unlit避免光照问题
        var renderer = mainQuad.GetComponent<Renderer>();
        if (renderer != null)
        {
            // 创建Unlit材质，避免光照导致黑色
            Material mat;
            if (defaultRoadMaterial != null)
            {
                mat = new Material(defaultRoadMaterial);
            }
            else
            {
                // 使用Unlit/Color shader，不受光照影响
                mat = new Material(Shader.Find("Unlit/Color"));
            }

            // 应用道路颜色
            if (roadType != null)
            {
                mat.color = roadType.roadColor;
            }
            else
            {
                mat.color = Color.gray;
            }

            renderer.material = mat;
        }

        // 移除碰撞体（避免影响游戏性）
        var collider = mainQuad.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        return roadGO;
    }

    /// <summary>
    /// 移除单个道路视觉
    /// </summary>
    private void RemoveRoadVisual(Vector2Int cell)
    {
        if (_roadVisuals.TryGetValue(cell, out var visual))
        {
            if (visual != null)
                Destroy(visual);
            _roadVisuals.Remove(cell);
        }
    }

    /// <summary>
    /// 更新相邻道路的视觉
    /// </summary>
    private void UpdateAdjacentRoadVisuals(Vector2Int cell)
    {
        var adjacent = RoadSegment.GetAdjacentCells(cell);
        foreach (var adjCell in adjacent)
        {
            var segment = roadNetwork?.GetRoadAt(adjCell);
            if (segment != null)
            {
                CreateRoadVisual(adjCell, segment);
            }
        }
    }

    // ============ Debug ============
#if UNITY_EDITOR
    [ContextMenu("Debug: Rebuild All Road Visuals")]
    private void DebugRebuildVisuals()
    {
        if (!Application.isPlaying) return;
        RebuildAllRoads();
    }

    [ContextMenu("Debug: Clear All Road Visuals")]
    private void DebugClearVisuals()
    {
        if (!Application.isPlaying) return;
        ClearAllVisuals();
    }
#endif
}
