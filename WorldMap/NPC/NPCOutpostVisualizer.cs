using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPC据点可视化器 - 在Game视图中显示NPC据点
/// </summary>
public class NPCOutpostVisualizer : MonoBehaviour
{
    [Header("References")]
    public NPCManager npcManager;
    public WorldMapManager worldMapManager;

    [Header("Visual Settings")]
    [Tooltip("据点标记的高度")]
    public float markerHeight = 1f;

    [Tooltip("标记的缩放")]
    public float markerScale = 3f;

    [Tooltip("是否在运行时动态更新")]
    public bool dynamicUpdate = true;

    [Tooltip("更新间隔（秒）")]
    public float updateInterval = 1f;

    [Header("Display Options")]
    [Tooltip("是否只显示已发现的据点")]
    public bool onlyShowDiscovered = true;

    [Tooltip("是否显示势力颜色")]
    public bool showFactionColors = true;

    [Header("Default Colors")]
    public Color friendlyColor = new Color(0f, 0.8f, 0f, 1f);
    public Color neutralColor = new Color(0.8f, 0.8f, 0f, 1f);
    public Color hostileColor = new Color(0.8f, 0f, 0f, 1f);
    public Color merchantColor = new Color(0f, 0.6f, 0.8f, 1f);
    public Color undiscoveredColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    [Header("Marker Prefab (Optional)")]
    public GameObject outpostMarkerPrefab;

    // Runtime
    private Dictionary<string, GameObject> _outpostVisuals = new();
    private Transform _outpostContainer;
    private float _lastUpdateTime;

    // ============ Lifecycle ============

    private void Awake()
    {
        _outpostContainer = new GameObject("OutpostVisualsContainer").transform;
        _outpostContainer.SetParent(transform);

        if (npcManager == null)
            npcManager = FindObjectOfType<NPCManager>();

        if (worldMapManager == null)
            worldMapManager = FindObjectOfType<WorldMapManager>();
    }

    private void Start()
    {
        RebuildAllOutposts();
    }

    private void Update()
    {
        if (!dynamicUpdate) return;

        if (Time.time - _lastUpdateTime >= updateInterval)
        {
            _lastUpdateTime = Time.time;
            RebuildAllOutposts();
        }
    }

    // ============ Visual Building ============

    /// <summary>
    /// 重建所有据点可视化
    /// </summary>
    [ContextMenu("Rebuild All Outposts")]
    public void RebuildAllOutposts()
    {
        ClearAllVisuals();

        if (npcManager == null) return;

        List<NPCOutpost> outposts;
        if (onlyShowDiscovered)
        {
            outposts = npcManager.GetDiscoveredOutposts();
        }
        else
        {
            outposts = npcManager.GetAllOutposts();
        }

        foreach (var outpost in outposts)
        {
            CreateOutpostVisual(outpost);
        }
    }

    /// <summary>
    /// 清除所有可视化
    /// </summary>
    public void ClearAllVisuals()
    {
        foreach (var kvp in _outpostVisuals)
        {
            if (kvp.Value != null)
                Destroy(kvp.Value);
        }
        _outpostVisuals.Clear();
    }

    /// <summary>
    /// 创建单个据点的可视化
    /// </summary>
    private void CreateOutpostVisual(NPCOutpost outpost)
    {
        if (outpost == null || worldMapManager == null) return;

        // 获取颜色
        Color color = GetOutpostColor(outpost);

        GameObject visual;
        if (outpostMarkerPrefab != null)
        {
            visual = Instantiate(outpostMarkerPrefab, _outpostContainer);
        }
        else
        {
            visual = CreateDefaultMarker(outpost, color);
        }

        if (visual == null) return;

        // 设置位置
        Vector3 worldPos = worldMapManager.CellToWorldCenter(outpost.cell);
        worldPos.y = markerHeight;
        visual.transform.position = worldPos;

        // 设置颜色（如果使用预制体，尝试设置其材质颜色）
        if (outpostMarkerPrefab != null)
        {
            var renderers = visual.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                foreach (var mat in r.materials)
                {
                    if (mat.HasProperty("_BaseColor"))
                        mat.SetColor("_BaseColor", color);
                    else if (mat.HasProperty("_Color"))
                        mat.color = color;
                }
            }
        }

        // 添加点击检测
        var click = visual.AddComponent<NPCOutpostMarkerClick>();
        click.outpostId = outpost.outpostId;
        var col = visual.AddComponent<BoxCollider>();
        col.size = new Vector3(markerScale, markerScale * 1.5f, markerScale);
        col.center = new Vector3(0, markerScale * 0.5f, 0);

        _outpostVisuals[outpost.outpostId] = visual;
    }

    /// <summary>
    /// 创建默认标记
    /// </summary>
    private GameObject CreateDefaultMarker(NPCOutpost outpost, Color color)
    {
        var go = new GameObject($"Outpost_{outpost.outpostId}");
        go.transform.SetParent(_outpostContainer);

        // 创建底座（圆柱体）
        var baseObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseObj.transform.SetParent(go.transform);
        baseObj.transform.localPosition = Vector3.zero;
        baseObj.transform.localScale = new Vector3(markerScale, 0.2f, markerScale);
        SetupMarkerRenderer(baseObj, color);

        // 创建主体（立方体或球体表示势力类型）
        var mainObj = CreateMainMarker(outpost);
        mainObj.transform.SetParent(go.transform);
        mainObj.transform.localPosition = new Vector3(0, markerScale * 0.5f, 0);
        mainObj.transform.localScale = Vector3.one * markerScale * 0.6f;
        SetupMarkerRenderer(mainObj, color);

        // 创建标签（使用3D文字或简单的标识）
        CreateOutpostLabel(go, outpost);

        return go;
    }

    /// <summary>
    /// 根据势力类型创建不同形状的标记
    /// </summary>
    private GameObject CreateMainMarker(NPCOutpost outpost)
    {
        // 获取势力信息
        NPCFaction faction = null;
        if (npcManager != null)
        {
            faction = npcManager.GetFactionById(outpost.factionId);
        }

        if (faction != null)
        {
            switch (faction.factionType)
            {
                case NPCFaction.FactionType.Merchant:
                    // 商人用立方体
                    return GameObject.CreatePrimitive(PrimitiveType.Cube);
                case NPCFaction.FactionType.Hostile:
                    // 敌对用尖锐的形状（这里用旋转的立方体模拟）
                    var hostile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    hostile.transform.localRotation = Quaternion.Euler(45, 45, 0);
                    return hostile;
                case NPCFaction.FactionType.Friendly:
                    // 友好用球体
                    return GameObject.CreatePrimitive(PrimitiveType.Sphere);
                default:
                    // 中立用胶囊
                    return GameObject.CreatePrimitive(PrimitiveType.Capsule);
            }
        }

        return GameObject.CreatePrimitive(PrimitiveType.Sphere);
    }

    /// <summary>
    /// 设置标记渲染器（URP兼容）
    /// </summary>
    private void SetupMarkerRenderer(GameObject obj, Color color)
    {
        var renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            // 优先使用URP兼容的shader
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            Material mat = new Material(shader);
            mat.color = color;
            renderer.material = mat;
        }

        // 移除碰撞体
        var collider = obj.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);
    }

    /// <summary>
    /// 创建据点标签
    /// </summary>
    private void CreateOutpostLabel(GameObject parent, NPCOutpost outpost)
    {
        // 创建一个简单的标识牌
        var labelHolder = new GameObject("Label");
        labelHolder.transform.SetParent(parent.transform);
        labelHolder.transform.localPosition = new Vector3(0, markerScale * 1.2f, 0);

        // 使用TextMesh显示名称（如果可用）
        var textMesh = labelHolder.AddComponent<TextMesh>();
        textMesh.text = outpost.displayName;
        textMesh.fontSize = 24;
        textMesh.characterSize = 0.3f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = Color.white;

        // 添加背景
        var bgQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bgQuad.transform.SetParent(labelHolder.transform);
        bgQuad.transform.localPosition = new Vector3(0, 0, 0.1f);
        bgQuad.transform.localScale = new Vector3(textMesh.text.Length * 0.25f, 0.8f, 1);

        var bgRenderer = bgQuad.GetComponent<Renderer>();
        if (bgRenderer != null)
        {
            // URP兼容的半透明材质
            var bgShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (bgShader == null)
                bgShader = Shader.Find("Unlit/Color");
            if (bgShader == null)
                bgShader = Shader.Find("Sprites/Default");

            Material bgMat = new Material(bgShader);
            bgMat.color = new Color(0, 0, 0, 0.7f);
            bgRenderer.material = bgMat;
        }

        var bgCollider = bgQuad.GetComponent<Collider>();
        if (bgCollider != null)
            Destroy(bgCollider);

        // 让标签始终面向摄像机
        var billboard = labelHolder.AddComponent<BillboardLabel>();
    }

    /// <summary>
    /// 获取据点颜色
    /// </summary>
    private Color GetOutpostColor(NPCOutpost outpost)
    {
        if (!outpost.isDiscovered)
            return undiscoveredColor;

        if (!showFactionColors)
            return neutralColor;

        // 根据势力获取颜色
        NPCFaction faction = null;
        if (npcManager != null)
        {
            faction = npcManager.GetFactionById(outpost.factionId);
        }

        if (faction != null)
        {
            // 优先使用势力定义的颜色
            if (faction.factionColor != default)
                return faction.factionColor;

            // 否则根据类型
            switch (faction.factionType)
            {
                case NPCFaction.FactionType.Friendly:
                    return friendlyColor;
                case NPCFaction.FactionType.Neutral:
                    return neutralColor;
                case NPCFaction.FactionType.Hostile:
                    return hostileColor;
                case NPCFaction.FactionType.Merchant:
                    return merchantColor;
            }
        }

        return neutralColor;
    }

    // ============ Public API ============

    /// <summary>
    /// 更新单个据点的可视化
    /// </summary>
    public void UpdateOutpostVisual(string outpostId)
    {
        // 移除旧的
        if (_outpostVisuals.TryGetValue(outpostId, out var oldVisual))
        {
            if (oldVisual != null)
                Destroy(oldVisual);
            _outpostVisuals.Remove(outpostId);
        }

        // 创建新的
        if (npcManager != null)
        {
            var outpost = npcManager.GetOutpost(outpostId);
            if (outpost != null && (!onlyShowDiscovered || outpost.isDiscovered))
            {
                CreateOutpostVisual(outpost);
            }
        }
    }

    /// <summary>
    /// 当据点被发现时调用
    /// </summary>
    public void OnOutpostDiscovered(string outpostId)
    {
        UpdateOutpostVisual(outpostId);
    }
}

/// <summary>
/// 简单的Billboard组件，让对象始终面向摄像机
/// </summary>
public class BillboardLabel : MonoBehaviour
{
    private Camera _mainCamera;

    private void Start()
    {
        _mainCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null) return;
        }

        transform.LookAt(transform.position + _mainCamera.transform.rotation * Vector3.forward,
                        _mainCamera.transform.rotation * Vector3.up);
    }
}

/// <summary>
/// NPC据点点击检测 - 附加到据点标记上，点击后打开据点交互面板
/// 模式参考: BaseMarker3D.OnMouseDown
/// </summary>
public class NPCOutpostMarkerClick : MonoBehaviour
{
    [HideInInspector]
    public string outpostId;

    private void OnMouseDown()
    {
        if (string.IsNullOrEmpty(outpostId)) return;

        Debug.Log($"[NPCOutpostMarkerClick] Clicked outpost: {outpostId}");

        if (NPCOutpostPopup.Instance != null)
            NPCOutpostPopup.Instance.Toggle(outpostId);
        else
            Debug.LogWarning("[NPCOutpostMarkerClick] NPCOutpostPopup.Instance is null. " +
                             "Make sure the popup exists in the scene Canvas.");
    }
}
