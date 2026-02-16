using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 资源区可视化器 - 在Game视图中显示资源区域、基地、NPC据点等
/// 支持为每种类型设置自定义3D预制体图标
/// 在Game窗口中也可正常显示（使用Quad + TextMesh，不依赖Gizmos）
/// </summary>
public class ResourceZoneVisualizer : MonoBehaviour
{
    [Header("References")]
    public WorldMapManager worldMapManager;

    [Header("Visual Settings")]
    [Tooltip("可视化平面的高度（仅用于默认Quad显示）")]
    public float visualHeight = 0.02f;

    [Tooltip("3D图标的高度偏移")]
    public float iconHeight = 0.5f;

    [Tooltip("是否在运行时动态更新")]
    public bool dynamicUpdate = true;

    [Tooltip("更新间隔（秒），0表示每帧更新")]
    public float updateInterval = 0.5f;

    [Header("Label Settings")]
    [Tooltip("是否显示区域名称标签")]
    public bool showLabels = true;

    [Tooltip("标签高度偏移")]
    public float labelHeight = 1.5f;

    [Tooltip("标签字体大小")]
    public float labelFontSize = 32f;

    [Tooltip("标签颜色")]
    public Color labelColor = Color.white;

    [Tooltip("是否显示区域边界线")]
    public bool showBorders = true;

    [Tooltip("边界线高度")]
    public float borderHeight = 0.15f;

    [Header("3D Prefab Icons")]
    [Tooltip("玩家基地3D图标预制体（放置在基地区域中心）")]
    public GameObject basePrefab;

    [Tooltip("NPC据点3D图标预制体")]
    public GameObject npcOutpostPrefab;

    [Tooltip("威胁区域3D图标预制体")]
    public GameObject threatPrefab;

    [Tooltip("不可建造区域3D图标预制体")]
    public GameObject unbuildablePrefab;

    [Tooltip("NPC领地3D图标预制体")]
    public GameObject npcTerritoryPrefab;

    [Header("Resource Zone Prefabs")]
    [Tooltip("为每种资源区类型配置3D预制体")]
    public List<ResourceZonePrefabMapping> resourceZonePrefabs = new();

    [Header("Default Colors (Quad fallback)")]
    public Color mineralColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
    public Color forestColor = new Color(0f, 0.6f, 0f, 0.5f);
    public Color fertileColor = new Color(0.6f, 0.4f, 0f, 0.5f);
    public Color waterColor = new Color(0f, 0.4f, 0.8f, 0.5f);
    public Color threatColor = new Color(1f, 0f, 0f, 0.4f);
    public Color unbuildableColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
    public Color baseColor = new Color(0f, 0.8f, 0f, 0.5f);
    public Color npcOutpostColor = new Color(0.8f, 0.5f, 0f, 0.5f);
    public Color npcTerritoryColor = new Color(0.8f, 0.6f, 0.2f, 0.3f);

    [Header("Display Options")]
    public bool showResourceZones = true;
    public bool showThreatZones = true;
    public bool showUnbuildableZones = true;
    public bool showBaseZones = true;
    public bool showNPCOutposts = true;
    public bool showNPCTerritory = true;

    // Runtime
    private Dictionary<Vector2Int, GameObject> _zoneVisuals = new();
    private Dictionary<string, GameObject> _areaIconVisuals = new();
    private List<GameObject> _labelObjects = new();
    private List<GameObject> _borderObjects = new();
    private Transform _zoneContainer;
    private Transform _iconContainer;
    private Transform _labelContainer;
    private Transform _borderContainer;
    private float _lastUpdateTime;
    private int _lastCellCount = -1; // 优化：只在数据变化时重建

    /// <summary>
    /// 资源区预制体映射
    /// </summary>
    [Serializable]
    public class ResourceZonePrefabMapping
    {
        [Tooltip("资源区类型ID（对应ResourceZoneType.zoneId）")]
        public string zoneTypeId;

        [Tooltip("资源区显示名称（仅供编辑器参考）")]
        public string displayName;

        [Tooltip("该资源区的3D预制体图标")]
        public GameObject prefab;

        [Tooltip("如果没有预制体，使用的颜色")]
        public Color fallbackColor = Color.white;
    }

    // ============ Lifecycle ============

    private void Awake()
    {
        _zoneContainer = new GameObject("ZoneVisualsContainer").transform;
        _zoneContainer.SetParent(transform);

        _iconContainer = new GameObject("IconContainer").transform;
        _iconContainer.SetParent(transform);

        _labelContainer = new GameObject("LabelContainer").transform;
        _labelContainer.SetParent(transform);

        _borderContainer = new GameObject("BorderContainer").transform;
        _borderContainer.SetParent(transform);

        if (worldMapManager == null)
            worldMapManager = FindObjectOfType<WorldMapManager>();
    }

    private void Start()
    {
        // 延迟一帧重建，确保 WorldMapTesterPhase1 等已生成测试数据
        Invoke(nameof(RebuildAllZones), 0.1f);
    }

    private void Update()
    {
        if (!dynamicUpdate) return;

        if (updateInterval > 0)
        {
            if (Time.time - _lastUpdateTime >= updateInterval)
            {
                _lastUpdateTime = Time.time;

                // 优化：只在格子数据数量变化时重建
                if (worldMapManager != null)
                {
                    int currentCount = worldMapManager.GetAllCellDataForSave().Count;
                    if (currentCount != _lastCellCount)
                    {
                        _lastCellCount = currentCount;
                        RebuildAllZones();
                    }
                }
            }
        }

        // 标签朝向摄像机
        if (showLabels && Camera.main != null)
        {
            var camTransform = Camera.main.transform;
            foreach (var label in _labelObjects)
            {
                if (label != null)
                    label.transform.forward = camTransform.forward;
            }
        }
    }

    // ============ Material Helper ============

    private Material CreateTransparentMaterial(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        Material mat = new Material(shader);
        mat.color = color;

        // 设置半透明渲染模式
        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1); // Transparent
            mat.SetFloat("_Blend", 0);   // Alpha blend
        }
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;

        return mat;
    }

    // ============ Visual Building ============

    /// <summary>
    /// 重建所有区域可视化
    /// </summary>
    [ContextMenu("Rebuild All Zones")]
    public void RebuildAllZones()
    {
        ClearAllVisuals();

        if (worldMapManager == null) return;

        // 用于追踪已处理的基地/据点
        var processedBases = new HashSet<string>();
        var processedNPCOutposts = new HashSet<string>();

        // 收集同类型区域的格子，用于计算中心点放标签
        var zoneGroups = new Dictionary<string, List<Vector2Int>>();

        var allCells = worldMapManager.GetAllCellDataForSave();
        _lastCellCount = allCells.Count;

        foreach (var cellData in allCells)
        {
            CreateZoneVisual(cellData, processedBases, processedNPCOutposts);
            CollectZoneGroup(cellData, zoneGroups);
        }

        // 为每个区域组创建中心标签
        if (showLabels)
            CreateZoneLabels(zoneGroups);

        // 为区域创建边界线
        if (showBorders)
            CreateZoneBorders(allCells);
    }

    /// <summary>
    /// 清除所有可视化
    /// </summary>
    public void ClearAllVisuals()
    {
        foreach (var kvp in _zoneVisuals)
        {
            if (kvp.Value != null)
                Destroy(kvp.Value);
        }
        _zoneVisuals.Clear();

        foreach (var kvp in _areaIconVisuals)
        {
            if (kvp.Value != null)
                Destroy(kvp.Value);
        }
        _areaIconVisuals.Clear();

        foreach (var label in _labelObjects)
        {
            if (label != null) Destroy(label);
        }
        _labelObjects.Clear();

        foreach (var border in _borderObjects)
        {
            if (border != null) Destroy(border);
        }
        _borderObjects.Clear();
    }

    /// <summary>
    /// 收集区域分组信息（用于标签定位）
    /// </summary>
    private void CollectZoneGroup(WorldMapCellData cellData, Dictionary<string, List<Vector2Int>> groups)
    {
        string groupKey = null;

        if (cellData.occupation == WorldMapCellData.OccupationType.Base)
            groupKey = $"base_{cellData.occupationId}";
        else if (cellData.occupation == WorldMapCellData.OccupationType.NPCOutpost)
            groupKey = $"npc_{cellData.occupationId}";
        else if (cellData.zoneState == WorldMapCellData.ZoneState.Threat)
            groupKey = $"threat_{cellData.threatLevel}_{GetRegionKey(cellData.cell, "threat")}";
        else if (cellData.zoneState == WorldMapCellData.ZoneState.Unbuildable)
            groupKey = $"unbuildable_{GetRegionKey(cellData.cell, "unbuildable")}";
        else if (cellData.zoneState == WorldMapCellData.ZoneState.NPCTerritory)
            groupKey = $"npcterritory_{cellData.npcFactionId}";
        else if (cellData.HasResourceZone)
            groupKey = $"resource_{cellData.resourceZoneTypeId}";

        if (groupKey == null) return;

        if (!groups.ContainsKey(groupKey))
            groups[groupKey] = new List<Vector2Int>();
        groups[groupKey].Add(cellData.cell);
    }

    /// <summary>
    /// 简单区域key生成（用格子坐标范围做粗略分组）
    /// </summary>
    private string GetRegionKey(Vector2Int cell, string prefix)
    {
        // 用 10x10 的区域块做粗略分组
        int bx = cell.x / 10;
        int by = cell.y / 10;
        return $"{prefix}_{bx}_{by}";
    }

    /// <summary>
    /// 为单个格子创建可视化
    /// </summary>
    private void CreateZoneVisual(WorldMapCellData cellData, HashSet<string> processedBases, HashSet<string> processedNPCOutposts)
    {
        Color? color = null;
        string label = "";
        GameObject prefab = null;
        bool isAreaType = false;
        string areaKey = null;

        // 1. 最高优先级：占用类型（Base、NPCOutpost）
        if (showBaseZones && cellData.occupation == WorldMapCellData.OccupationType.Base)
        {
            color = baseColor;
            label = "Base";
            prefab = basePrefab;
            isAreaType = true;
            areaKey = $"base_{cellData.occupationId}";
        }
        else if (showNPCOutposts && cellData.occupation == WorldMapCellData.OccupationType.NPCOutpost)
        {
            color = npcOutpostColor;
            label = "NPC";
            prefab = npcOutpostPrefab;
            isAreaType = true;
            areaKey = $"npc_{cellData.occupationId}";
        }
        // 2. 中层：区域状态
        else if (showThreatZones && cellData.zoneState == WorldMapCellData.ZoneState.Threat)
        {
            color = cellData.threatCleared
                ? new Color(threatColor.r, threatColor.g + 0.3f, threatColor.b, threatColor.a * 0.5f)
                : threatColor;
            label = cellData.threatCleared ? "Cleared" : "Threat";
            prefab = threatPrefab;
        }
        else if (showUnbuildableZones && cellData.zoneState == WorldMapCellData.ZoneState.Unbuildable)
        {
            color = unbuildableColor;
            label = "Unbuildable";
            prefab = unbuildablePrefab;
        }
        else if (showNPCTerritory && cellData.zoneState == WorldMapCellData.ZoneState.NPCTerritory)
        {
            color = npcTerritoryColor;
            label = "NPC Territory";
            prefab = npcTerritoryPrefab;
        }
        // 3. 底层：资源区
        else if (showResourceZones && cellData.HasResourceZone)
        {
            var mapping = GetResourceZonePrefabMapping(cellData.resourceZoneTypeId);
            if (mapping != null)
            {
                prefab = mapping.prefab;
                color = mapping.fallbackColor;
            }

            if (color == null)
                color = GetResourceZoneColor(cellData.resourceZoneTypeId);
            label = cellData.resourceZoneTypeId ?? "Resource";
        }

        if (color == null) return;

        // 对于区域类型（基地/据点），3D图标只放一个
        if (isAreaType && prefab != null && areaKey != null)
        {
            var quadVisual = CreateZoneQuad(cellData.cell, color.Value);
            if (quadVisual != null)
                _zoneVisuals[cellData.cell] = quadVisual;

            if (cellData.occupation == WorldMapCellData.OccupationType.Base)
            {
                string baseId = cellData.occupationId ?? "default_base";
                if (!processedBases.Contains(baseId))
                {
                    processedBases.Add(baseId);
                    CreateAreaIcon(cellData.cell, prefab, areaKey);
                }
            }
            else if (cellData.occupation == WorldMapCellData.OccupationType.NPCOutpost)
            {
                string outpostId = cellData.occupationId ?? "default_npc";
                if (!processedNPCOutposts.Contains(outpostId))
                {
                    processedNPCOutposts.Add(outpostId);
                    CreateAreaIcon(cellData.cell, prefab, areaKey);
                }
            }
        }
        else if (prefab != null)
        {
            var visual = CreatePrefabVisual(cellData.cell, prefab, label);
            if (visual != null)
                _zoneVisuals[cellData.cell] = visual;
        }
        else
        {
            var visual = CreateZoneQuad(cellData.cell, color.Value);
            if (visual != null)
                _zoneVisuals[cellData.cell] = visual;
        }
    }

    // ============ Zone Labels ============

    /// <summary>
    /// 为每个区域组在中心创建3D文字标签
    /// </summary>
    private void CreateZoneLabels(Dictionary<string, List<Vector2Int>> zoneGroups)
    {
        foreach (var kvp in zoneGroups)
        {
            string groupKey = kvp.Key;
            var cells = kvp.Value;
            if (cells.Count == 0) continue;

            // 计算区域中心点
            Vector3 center = Vector3.zero;
            foreach (var cell in cells)
            {
                center += worldMapManager.CellToWorldCenter(cell);
            }
            center /= cells.Count;
            center.y = labelHeight;

            // 确定标签文字
            string labelText = GetGroupDisplayName(groupKey);
            if (string.IsNullOrEmpty(labelText)) continue;

            // 确定标签颜色
            Color textColor = GetGroupLabelColor(groupKey);

            // 创建3D文字
            var labelObj = CreateTextMeshLabel(center, labelText, textColor);
            if (labelObj != null)
                _labelObjects.Add(labelObj);
        }
    }

    /// <summary>
    /// 获取分组的显示名称
    /// </summary>
    private string GetGroupDisplayName(string groupKey)
    {
        if (groupKey.StartsWith("resource_"))
        {
            string zoneTypeId = groupKey.Substring("resource_".Length);
            // 从 WorldMapManager 获取 displayName
            if (worldMapManager != null)
            {
                var zoneType = worldMapManager.GetResourceZoneType(zoneTypeId);
                if (zoneType != null)
                    return zoneType.displayName;
            }
            return zoneTypeId;
        }
        if (groupKey.StartsWith("threat_"))
            return "威胁区";
        if (groupKey.StartsWith("unbuildable_"))
            return "不可建造";
        if (groupKey.StartsWith("npcterritory_"))
            return "NPC领地";
        if (groupKey.StartsWith("base_"))
            return "基地";
        if (groupKey.StartsWith("npc_"))
            return "NPC据点";
        return groupKey;
    }

    /// <summary>
    /// 获取分组标签颜色
    /// </summary>
    private Color GetGroupLabelColor(string groupKey)
    {
        if (groupKey.StartsWith("resource_"))
        {
            string zoneTypeId = groupKey.Substring("resource_".Length);
            Color zoneColor = GetResourceZoneColor(zoneTypeId);
            // 标签用更亮的颜色
            return new Color(
                Mathf.Min(1f, zoneColor.r + 0.3f),
                Mathf.Min(1f, zoneColor.g + 0.3f),
                Mathf.Min(1f, zoneColor.b + 0.3f),
                1f
            );
        }
        if (groupKey.StartsWith("threat_"))
            return new Color(1f, 0.3f, 0.3f, 1f);
        if (groupKey.StartsWith("unbuildable_"))
            return new Color(0.6f, 0.6f, 0.6f, 1f);
        if (groupKey.StartsWith("npcterritory_"))
            return new Color(1f, 0.8f, 0.3f, 1f);
        if (groupKey.StartsWith("base_"))
            return new Color(0.3f, 1f, 0.3f, 1f);
        if (groupKey.StartsWith("npc_"))
            return new Color(1f, 0.7f, 0.2f, 1f);
        return labelColor;
    }

    /// <summary>
    /// 创建3D TextMesh标签（Game窗口可见）
    /// </summary>
    private GameObject CreateTextMeshLabel(Vector3 position, string text, Color color)
    {
        var go = new GameObject($"Label_{text}");
        go.transform.SetParent(_labelContainer);
        go.transform.position = position;

        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = (int)labelFontSize;
        tm.characterSize = 0.5f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        tm.fontStyle = FontStyle.Bold;

        // 文字渲染在最前面
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.material.renderQueue = 4000;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        return go;
    }

    // ============ Zone Borders ============

    /// <summary>
    /// 为所有区域创建边界线（使用 LineRenderer）
    /// </summary>
    private void CreateZoneBorders(List<WorldMapCellData> allCells)
    {
        if (worldMapManager == null) return;

        // 按区域类型分组
        var cellSet = new HashSet<Vector2Int>();
        var resourceCells = new Dictionary<string, HashSet<Vector2Int>>();
        var threatCells = new HashSet<Vector2Int>();
        var unbuildableCells = new HashSet<Vector2Int>();

        foreach (var cell in allCells)
        {
            if (cell.HasResourceZone)
            {
                string id = cell.resourceZoneTypeId;
                if (!resourceCells.ContainsKey(id))
                    resourceCells[id] = new HashSet<Vector2Int>();
                resourceCells[id].Add(cell.cell);
            }
            if (cell.zoneState == WorldMapCellData.ZoneState.Threat)
                threatCells.Add(cell.cell);
            if (cell.zoneState == WorldMapCellData.ZoneState.Unbuildable)
                unbuildableCells.Add(cell.cell);
        }

        // 为每种资源区创建边界
        foreach (var kvp in resourceCells)
        {
            Color borderColor = GetResourceZoneColor(kvp.Key);
            borderColor.a = 1f;
            CreateBorderForCellGroup(kvp.Value, borderColor);
        }

        // 威胁区边界
        if (threatCells.Count > 0)
        {
            Color c = threatColor; c.a = 1f;
            CreateBorderForCellGroup(threatCells, c);
        }

        // 不可建造区边界
        if (unbuildableCells.Count > 0)
        {
            Color c = unbuildableColor; c.a = 1f;
            CreateBorderForCellGroup(unbuildableCells, c);
        }
    }

    /// <summary>
    /// 为一组格子创建外边界线
    /// </summary>
    private void CreateBorderForCellGroup(HashSet<Vector2Int> cells, Color color)
    {
        if (cells.Count == 0 || worldMapManager == null) return;

        float cs = worldMapManager.cellSize;
        float half = cs * 0.5f;

        // 收集所有外边界线段
        var segments = new List<(Vector3 a, Vector3 b)>();

        foreach (var cell in cells)
        {
            Vector3 center = worldMapManager.CellToWorldCenter(cell);
            center.y = borderHeight;

            // 检查四个方向，如果邻居不在同一组则画边
            // 上边 (z+)
            if (!cells.Contains(cell + Vector2Int.up))
                segments.Add((center + new Vector3(-half, 0, half), center + new Vector3(half, 0, half)));
            // 下边 (z-)
            if (!cells.Contains(cell + Vector2Int.down))
                segments.Add((center + new Vector3(-half, 0, -half), center + new Vector3(half, 0, -half)));
            // 右边 (x+)
            if (!cells.Contains(cell + Vector2Int.right))
                segments.Add((center + new Vector3(half, 0, -half), center + new Vector3(half, 0, half)));
            // 左边 (x-)
            if (!cells.Contains(cell + Vector2Int.left))
                segments.Add((center + new Vector3(-half, 0, -half), center + new Vector3(-half, 0, half)));
        }

        if (segments.Count == 0) return;

        // 创建 LineRenderer
        var borderGO = new GameObject($"Border_{color.GetHashCode()}");
        borderGO.transform.SetParent(_borderContainer);

        var lr = borderGO.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = false;
        lr.widthMultiplier = cs * 0.06f;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        // 简单材质
        var mat = CreateTransparentMaterial(color);
        mat.renderQueue = 3100;
        lr.material = mat;
        lr.startColor = color;
        lr.endColor = color;

        // 将所有线段的顶点依次放入（每段之间加入断开的连接）
        var positions = new List<Vector3>();
        foreach (var seg in segments)
        {
            positions.Add(seg.a);
            positions.Add(seg.b);
        }

        lr.positionCount = positions.Count;
        lr.SetPositions(positions.ToArray());

        _borderObjects.Add(borderGO);
    }

    // ============ Prefab Mapping ============

    private ResourceZonePrefabMapping GetResourceZonePrefabMapping(string zoneTypeId)
    {
        if (string.IsNullOrEmpty(zoneTypeId)) return null;

        foreach (var mapping in resourceZonePrefabs)
        {
            if (mapping.zoneTypeId == zoneTypeId)
                return mapping;
        }
        return null;
    }

    private Color GetResourceZoneColor(string zoneTypeId)
    {
        if (string.IsNullOrEmpty(zoneTypeId))
            return Color.white * 0.5f;

        var mapping = GetResourceZonePrefabMapping(zoneTypeId);
        if (mapping != null)
            return mapping.fallbackColor;

        if (worldMapManager != null && worldMapManager.resourceZoneTypes != null)
        {
            foreach (var zoneType in worldMapManager.resourceZoneTypes)
            {
                if (zoneType != null && zoneType.zoneId == zoneTypeId)
                    return zoneType.mapColor;
            }
        }

        string lower = zoneTypeId.ToLower();
        if (lower.Contains("mineral") || lower.Contains("ore")) return mineralColor;
        if (lower.Contains("forest") || lower.Contains("wood")) return forestColor;
        if (lower.Contains("fertile") || lower.Contains("farm")) return fertileColor;
        if (lower.Contains("water")) return waterColor;

        return Color.white * 0.5f;
    }

    // ============ Visual Creation ============

    private void CreateAreaIcon(Vector2Int cell, GameObject prefab, string areaKey)
    {
        if (worldMapManager == null || prefab == null) return;

        Vector3 worldPos = worldMapManager.CellToWorldCenter(cell);
        worldPos.y = iconHeight;

        var icon = Instantiate(prefab, worldPos, Quaternion.identity, _iconContainer);
        icon.name = $"Icon_{areaKey}";
        _areaIconVisuals[areaKey] = icon;
    }

    private GameObject CreatePrefabVisual(Vector2Int cell, GameObject prefab, string label)
    {
        if (worldMapManager == null || prefab == null) return null;

        Vector3 worldPos = worldMapManager.CellToWorldCenter(cell);
        worldPos.y = iconHeight;

        var go = Instantiate(prefab, worldPos, Quaternion.identity, _zoneContainer);
        go.name = $"Zone_{cell.x}_{cell.y}_{label}";
        return go;
    }

    private GameObject CreateZoneQuad(Vector2Int cell, Color color)
    {
        if (worldMapManager == null) return null;

        var go = new GameObject($"Zone_{cell.x}_{cell.y}");
        go.transform.SetParent(_zoneContainer);

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.transform.SetParent(go.transform);
        quad.transform.localRotation = Quaternion.Euler(90, 0, 0);
        quad.transform.localPosition = Vector3.zero;

        float cellSize = worldMapManager.cellSize;
        quad.transform.localScale = new Vector3(cellSize * 0.95f, cellSize * 0.95f, 1);

        Vector3 worldPos = worldMapManager.CellToWorldCenter(cell);
        worldPos.y = visualHeight;
        go.transform.position = worldPos;

        var renderer = quad.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = CreateTransparentMaterial(color);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        var collider = quad.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        return go;
    }

    // ============ Public API ============

    public void UpdateCellVisual(Vector2Int cell)
    {
        if (_zoneVisuals.TryGetValue(cell, out var oldVisual))
        {
            if (oldVisual != null) Destroy(oldVisual);
            _zoneVisuals.Remove(cell);
        }

        if (worldMapManager != null)
        {
            var cellData = worldMapManager.GetCellData(cell);
            if (cellData != null)
            {
                var processedBases = new HashSet<string>();
                var processedNPCOutposts = new HashSet<string>();
                CreateZoneVisual(cellData, processedBases, processedNPCOutposts);
            }
        }
    }

    public void SetDisplayOptions(bool resources, bool threats, bool unbuildable)
    {
        showResourceZones = resources;
        showThreatZones = threats;
        showUnbuildableZones = unbuildable;
        RebuildAllZones();
    }

    public void SetAllDisplayOptions(bool resources, bool threats, bool unbuildable, bool bases, bool npcOutposts, bool npcTerritory)
    {
        showResourceZones = resources;
        showThreatZones = threats;
        showUnbuildableZones = unbuildable;
        showBaseZones = bases;
        showNPCOutposts = npcOutposts;
        showNPCTerritory = npcTerritory;
        RebuildAllZones();
    }

    /// <summary>
    /// 强制立即重建（手动调用）
    /// </summary>
    [ContextMenu("Force Rebuild Now")]
    public void ForceRebuild()
    {
        _lastCellCount = -1;
        RebuildAllZones();
    }
}
