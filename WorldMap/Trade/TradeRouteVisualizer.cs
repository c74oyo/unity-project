using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 贸易路线可视化器 - 在Game视图中显示贸易路线连接
/// </summary>
public class TradeRouteVisualizer : MonoBehaviour
{
    [Header("References")]
    public TradeManager tradeManager;
    public RoadNetwork roadNetwork;
    public WorldMapManager worldMapManager;

    [Header("Visual Settings")]
    [Tooltip("路线高度（略高于道路）")]
    public float routeHeight = 0.15f;

    [Tooltip("路线宽度")]
    public float routeWidth = 1.5f;

    [Tooltip("是否动态更新")]
    public bool dynamicUpdate = true;

    [Tooltip("更新间隔")]
    public float updateInterval = 1f;

    [Header("Colors")]
    public Color activeRouteColor = new Color(0f, 1f, 0.5f, 0.8f);
    public Color inactiveRouteColor = new Color(1f, 1f, 0f, 0.5f);
    public Color invalidRouteColor = new Color(1f, 0f, 0f, 0.5f);

    [Header("Connection Indicators")]
    [Tooltip("是否显示起点/终点标记")]
    public bool showEndpointMarkers = true;

    [Tooltip("起点标记颜色（玩家基地）")]
    public Color sourceMarkerColor = Color.blue;

    [Tooltip("终点标记颜色（NPC据点）")]
    public Color targetMarkerColor = Color.green;

    [Tooltip("标记大小")]
    public float markerSize = 2f;

    // Runtime
    private Dictionary<string, GameObject> _routeVisuals = new();
    private Transform _routeContainer;
    private float _lastUpdateTime;

    // ============ Lifecycle ============

    private void Awake()
    {
        _routeContainer = new GameObject("TradeRouteVisualsContainer").transform;
        _routeContainer.SetParent(transform);

        if (tradeManager == null)
            tradeManager = FindObjectOfType<TradeManager>();
        if (roadNetwork == null)
            roadNetwork = FindObjectOfType<RoadNetwork>();
        if (worldMapManager == null)
            worldMapManager = FindObjectOfType<WorldMapManager>();
    }

    private void Start()
    {
        RebuildAllRoutes();

        // 订阅事件
        if (tradeManager != null)
        {
            tradeManager.OnTradeRouteCreated += OnRouteCreated;
            tradeManager.OnTradeRouteRemoved += OnRouteRemoved;
        }
    }

    private void OnDestroy()
    {
        if (tradeManager != null)
        {
            tradeManager.OnTradeRouteCreated -= OnRouteCreated;
            tradeManager.OnTradeRouteRemoved -= OnRouteRemoved;
        }
    }

    private void Update()
    {
        if (!dynamicUpdate) return;

        if (Time.time - _lastUpdateTime >= updateInterval)
        {
            _lastUpdateTime = Time.time;
            RebuildAllRoutes();
        }
    }

    // ============ Event Handlers ============

    private void OnRouteCreated(TradeRoute route)
    {
        CreateRouteVisual(route);
    }

    private void OnRouteRemoved(TradeRoute route)
    {
        if (route != null)
            RemoveRouteVisual(route.routeId);
    }

    // ============ Visual Building ============

    [ContextMenu("Rebuild All Routes")]
    public void RebuildAllRoutes()
    {
        ClearAllVisuals();

        if (tradeManager == null) return;

        var routes = tradeManager.GetAllTradeRoutes();
        foreach (var route in routes)
        {
            CreateRouteVisual(route);
        }
    }

    public void ClearAllVisuals()
    {
        foreach (var kvp in _routeVisuals)
        {
            if (kvp.Value != null)
                Destroy(kvp.Value);
        }
        _routeVisuals.Clear();
    }

    private void CreateRouteVisual(TradeRoute route)
    {
        if (route == null || worldMapManager == null) return;

        // 移除旧的
        RemoveRouteVisual(route.routeId);

        var go = new GameObject($"TradeRoute_{route.routeId}");
        go.transform.SetParent(_routeContainer);

        // 确定颜色
        Color routeColor;
        if (!route.isValid)
            routeColor = invalidRouteColor;
        else if (route.isActive)
            routeColor = activeRouteColor;
        else
            routeColor = inactiveRouteColor;

        // 绘制路径
        if (route.roadPath != null && route.roadPath.Count > 1)
        {
            CreatePathLine(go, route.roadPath, routeColor);
        }
        else
        {
            // 没有路径时，绘制直线连接
            CreateDirectLine(go, route.sourceCell, route.targetCell, routeColor);
        }

        // 绘制端点标记
        if (showEndpointMarkers)
        {
            CreateEndpointMarker(go, route.sourceCell, sourceMarkerColor, "Source");
            CreateEndpointMarker(go, route.targetCell, targetMarkerColor, "Target");
        }

        _routeVisuals[route.routeId] = go;
    }

    private void CreatePathLine(GameObject parent, List<Vector2Int> path, Color color)
    {
        // 使用LineRenderer绘制路径
        var lineObj = new GameObject("PathLine");
        lineObj.transform.SetParent(parent.transform);

        var lineRenderer = lineObj.AddComponent<LineRenderer>();
        lineRenderer.positionCount = path.Count;
        lineRenderer.startWidth = routeWidth;
        lineRenderer.endWidth = routeWidth;

        // 设置材质
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;

        // 设置点位置
        for (int i = 0; i < path.Count; i++)
        {
            Vector3 worldPos = worldMapManager.CellToWorldCenter(path[i]);
            worldPos.y = routeHeight;
            lineRenderer.SetPosition(i, worldPos);
        }
    }

    private void CreateDirectLine(GameObject parent, Vector2Int start, Vector2Int end, Color color)
    {
        var lineObj = new GameObject("DirectLine");
        lineObj.transform.SetParent(parent.transform);

        var lineRenderer = lineObj.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = routeWidth;
        lineRenderer.endWidth = routeWidth;

        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;

        Vector3 startPos = worldMapManager.CellToWorldCenter(start);
        Vector3 endPos = worldMapManager.CellToWorldCenter(end);
        startPos.y = routeHeight;
        endPos.y = routeHeight;

        lineRenderer.SetPosition(0, startPos);
        lineRenderer.SetPosition(1, endPos);
    }

    private void CreateEndpointMarker(GameObject parent, Vector2Int cell, Color color, string label)
    {
        var markerObj = new GameObject($"Marker_{label}");
        markerObj.transform.SetParent(parent.transform);

        // 创建圆环标记
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.transform.SetParent(markerObj.transform);

        Vector3 worldPos = worldMapManager.CellToWorldCenter(cell);
        worldPos.y = routeHeight + 0.1f;
        markerObj.transform.position = worldPos;

        ring.transform.localPosition = Vector3.zero;
        ring.transform.localScale = new Vector3(markerSize, 0.1f, markerSize);

        var renderer = ring.GetComponent<Renderer>();
        if (renderer != null)
        {
            var mat = new Material(Shader.Find("Unlit/Color"));
            mat.color = color;
            renderer.material = mat;
        }

        var collider = ring.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);
    }

    private void RemoveRouteVisual(string routeId)
    {
        if (_routeVisuals.TryGetValue(routeId, out var visual))
        {
            if (visual != null)
                Destroy(visual);
            _routeVisuals.Remove(routeId);
        }
    }

    // ============ Debug Tools ============

    /// <summary>
    /// 检查两点之间是否有道路连接
    /// </summary>
    public bool CheckRoadConnection(Vector2Int from, Vector2Int to)
    {
        if (roadNetwork == null)
        {
            Debug.LogWarning("[TradeRouteVisualizer] RoadNetwork not found!");
            return false;
        }

        var path = roadNetwork.FindPath(from, to);
        bool connected = path != null && path.Count > 0;

        Debug.Log($"[TradeRouteVisualizer] Road connection from {from} to {to}: {(connected ? "CONNECTED" : "NOT CONNECTED")}");
        if (connected)
        {
            Debug.Log($"  Path length: {path.Count} cells");
            Debug.Log($"  Path: {string.Join(" -> ", path)}");
        }

        return connected;
    }

    /// <summary>
    /// 验证贸易路线的有效性
    /// </summary>
    public void ValidateTradeRoute(string routeId)
    {
        if (tradeManager == null)
        {
            Debug.LogWarning("[TradeRouteVisualizer] TradeManager not found!");
            return;
        }

        var routes = tradeManager.GetAllTradeRoutes();
        TradeRoute targetRoute = null;

        foreach (var route in routes)
        {
            if (route.routeId == routeId)
            {
                targetRoute = route;
                break;
            }
        }

        if (targetRoute == null)
        {
            Debug.LogWarning($"[TradeRouteVisualizer] Route not found: {routeId}");
            return;
        }

        Debug.Log($"========== Trade Route Validation: {targetRoute.displayName} ==========");
        Debug.Log($"  Route ID: {targetRoute.routeId}");
        Debug.Log($"  Source: {targetRoute.sourceBaseId} at {targetRoute.sourceCell}");
        Debug.Log($"  Target: {targetRoute.targetOutpostId} at {targetRoute.targetCell}");
        Debug.Log($"  Is Valid: {targetRoute.isValid}");
        Debug.Log($"  Is Active: {targetRoute.isActive}");
        Debug.Log($"  Road Path: {(targetRoute.roadPath != null ? targetRoute.roadPath.Count + " cells" : "NULL")}");
        Debug.Log($"  Cargo Items: {targetRoute.cargoItems.Count}");
        Debug.Log("=======================================================");
    }

    [ContextMenu("Debug: Check All Connections")]
    public void DebugCheckAllConnections()
    {
        if (tradeManager == null)
        {
            Debug.LogWarning("[TradeRouteVisualizer] TradeManager not found!");
            return;
        }

        var routes = tradeManager.GetAllTradeRoutes();
        Debug.Log($"========== All Trade Routes ({routes.Count}) ==========");

        foreach (var route in routes)
        {
            string status = route.isValid ? (route.isActive ? "ACTIVE" : "INACTIVE") : "INVALID";
            Debug.Log($"  [{status}] {route.displayName}: {route.sourceCell} -> {route.targetCell}");

            if (route.roadPath != null)
            {
                Debug.Log($"    Path: {route.roadPath.Count} cells");
            }
            else
            {
                Debug.Log($"    Path: NO PATH (checking road connection...)");
                CheckRoadConnection(route.sourceCell, route.targetCell);
            }
        }

        Debug.Log("====================================================");
    }

    [ContextMenu("Debug: Show Road Network Stats")]
    public void DebugShowRoadNetworkStats()
    {
        if (roadNetwork == null)
        {
            Debug.LogWarning("[TradeRouteVisualizer] RoadNetwork not found!");
            return;
        }

        var allRoads = roadNetwork.GetAllRoads();
        Debug.Log($"========== Road Network Stats ==========");
        Debug.Log($"  Total road segments: {allRoads.Count}");

        if (allRoads.Count > 0)
        {
            // 找出道路覆盖的边界
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;

            foreach (var road in allRoads)
            {
                minX = Mathf.Min(minX, road.cell.x);
                maxX = Mathf.Max(maxX, road.cell.x);
                minY = Mathf.Min(minY, road.cell.y);
                maxY = Mathf.Max(maxY, road.cell.y);
            }

            Debug.Log($"  Coverage: X[{minX}-{maxX}], Y[{minY}-{maxY}]");
        }

        Debug.Log("=========================================");
    }
}
