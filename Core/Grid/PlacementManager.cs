using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEngine.EventSystems;

public class PlacementManager : MonoBehaviour
{
    public enum InteractionMode { Normal, Build, Demolish }

    [Header("Refs")]
    public Camera cam;
    public GridSystem grid;
    public BuildCatalog catalog;
    public LayerMask groundMask;   // Ground/Walkway/Highway 等（落点射线）

    [Header("Economy")]
    [Tooltip("可以是 GlobalInventory 或 BaseInventory")]
    public MonoBehaviour globalInventory;

    private IInventorySystem _inventory;
    public IInventorySystem Inventory
    {
        get
        {
            if (_inventory == null && globalInventory != null)
                _inventory = globalInventory as IInventorySystem;
            return _inventory;
        }
    }

    [Header("Layers")]
    public string buildingLayerName = "Building";
    public string walkwayLayerName  = "Walkway";

    [Header("Overlay / Visuals (optional)")]
    public GameObject gridOverlayRoot;          // 你的网格显示物体（可不填）
    public PowerHeatmapOverlay heatmapOverlay;  // 你的热力图脚本对象（可不填）

    [Header("Demolish")]
    public KeyCode toggleDemolishKey = KeyCode.X;
    public LayerMask buildingMask; // 拆除射线，只勾 Building（以及你想允许拆的层）

    [Header("NavMesh Refs")]
    public NavMeshSurface workerSurface;   // Walkway NavMeshSurface（用于 DockYard 校验）
    public NavMeshSurface vehicleSurface;  // Highway NavMeshSurface（用于 DockYard 校验）
    public Transform highwayEntryPoint;
    public float navSampleRadius = 2f;

    [Header("Ghost Materials")]
    public Material ghostMatOk;
    public Material ghostMatBad;

    [Header("Hotkeys")]
    public KeyCode toggleBuildModeKey = KeyCode.B;
    public KeyCode rotateKey = KeyCode.R;
    public KeyCode exitModeKey = KeyCode.Escape;

    [Header("Debug")]
    public bool enableDebugLogs = false;

    // ===== Public state =====
    [SerializeField] private InteractionMode _mode = InteractionMode.Normal;
    public InteractionMode Mode => _mode;

    // 保持旧接口（如果你别的脚本还在用）
    public bool BuildMode { get; private set; }
    public bool DemolishMode { get; private set; }

    public event Action<InteractionMode> OnModeChanged;

    // ===== Build runtime =====
    private BuildableDefinition current;
    private GameObject ghost;
    private int rot90; // 0..3
    private Vector2Int lastAnchor;
    private bool lastCanPlace;

    // 公开 ghost 以便 PowerHeatmapOverlay 可以在预览模式下显示热力图
    public GameObject Ghost => ghost;

    private int _buildingLayer = -1;
    private int _walkwayLayer = -1;

    private void Awake()
    {
        if (globalInventory == null)
        {
            globalInventory = GlobalInventory.Instance;
            if (globalInventory == null)
                Debug.LogError("[PlacementManager] GlobalInventory.Instance is null. Ensure GlobalInventory exists in scene.", this);
        }

        _buildingLayer = LayerMask.NameToLayer(buildingLayerName);
        _walkwayLayer  = LayerMask.NameToLayer(walkwayLayerName);

        ApplyMode(_mode, clearSelection: true);
    }

    private void Update()
    {
        // 测试：检测任何鼠标点击
        if (enableDebugLogs && Input.GetMouseButtonDown(0))
        {
            Debug.Log($"[PlacementManager] RAW Mouse Click Detected! Mode={_mode}, Frame={Time.frameCount}");
        }

        // ===== Global exit =====
        if (Input.GetKeyDown(exitModeKey))
        {
            SetMode(InteractionMode.Normal, clearSelection: true);
            return;
        }

        // ===== Toggle keys =====
        if (Input.GetKeyDown(toggleBuildModeKey))
        {
            if (_mode == InteractionMode.Build) SetMode(InteractionMode.Normal, true);
            else SetMode(InteractionMode.Build, false);

            if (enableDebugLogs)
                Debug.Log($"[PlacementManager] Build Mode Toggled! New Mode: {_mode}");
            return;
        }

        if (Input.GetKeyDown(toggleDemolishKey))
        {
            if (_mode == InteractionMode.Demolish) SetMode(InteractionMode.Normal, true);
            else SetMode(InteractionMode.Demolish, true);
            return;
        }

        // ===== Demolish =====
        if (_mode == InteractionMode.Demolish)
        {
            if (Input.GetMouseButtonDown(0))
                TryDemolishUnderCursor();
            return;
        }

        // ===== Build =====
        if (_mode != InteractionMode.Build)
        {
            if (enableDebugLogs && Input.GetMouseButtonDown(0))
                Debug.LogWarning($"[PlacementManager] Mouse clicked but not in Build mode! Current mode: {_mode}");
            return;
        }

        if (enableDebugLogs && Time.frameCount % 120 == 0)
            Debug.Log($"[PlacementManager] In Build Mode. Current building: {(current != null ? current.name : "NONE")}");

        // 数字键选建筑
        var picked = catalog != null ? catalog.GetByNumberKey() : null;
        if (picked != null)
        {
            SetCurrent(picked);
            if (enableDebugLogs)
                Debug.Log($"[PlacementManager] Building selected: {picked.name}");
        }

        if (current == null)
        {
            if (enableDebugLogs && Time.frameCount % 60 == 0)
                Debug.LogWarning("[PlacementManager] No building selected! Press number keys or use UI to select a building.");
            return;
        }

        if (Input.GetKeyDown(rotateKey))
            rot90 = (rot90 + 1) % 4;

        // 右键取消当前选择（不退出建造模式）
        if (Input.GetMouseButtonDown(1))
        {
            ClearCurrent();
            return;
        }

        if (!TryGetGroundHit(out var hit))
        {
            if (enableDebugLogs && Time.frameCount % 60 == 0)
                Debug.LogWarning("[PlacementManager] TryGetGroundHit FAILED! Cannot detect ground. Check Ground Mask and Terrain layer.");
            if (ghost != null) ghost.SetActive(false);
            return;
        }

        if (enableDebugLogs && Time.frameCount % 60 == 0)
            Debug.Log($"[PlacementManager] Ground hit detected at: {hit.point}");

        if (ghost == null) CreateGhost();
        ghost.SetActive(true);

        Vector2Int cell = grid.WorldToCell(hit.point);

        // 让鼠标点靠近“占地中心”
        bool swap = (rot90 % 2) != 0;
        int w = swap ? current.size.y : current.size.x;
        int h = swap ? current.size.x : current.size.y;
        Vector2Int anchor = new Vector2Int(cell.x - w / 2, cell.y - h / 2);

        lastAnchor = anchor;

        Vector3 center = GetFootprintCenterWorld(anchor, current.size, rot90);
        center.y = hit.point.y;

        lastCanPlace = grid.CanPlace(anchor, current.size, rot90);
        lastCanPlace &= ValidateDockYardIfNeeded();

        // 经济预检查
        if (lastCanPlace && current.buildCost != null && current.buildCost.Count > 0)
        {
            if (globalInventory == null) globalInventory = GlobalInventory.Instance;
            if (Inventory == null) lastCanPlace = false;
            else if (!Inventory.CanAffordBatch(current.buildCost, 1)) lastCanPlace = false;
        }

        ghost.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, rot90 * 90f, 0f));
        ApplyGhostMaterial(lastCanPlace ? ghostMatOk : ghostMatBad);

        if (Input.GetMouseButtonDown(0))
        {
            // 检查是否点击在 UI 上
            bool isOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            if (enableDebugLogs)
            {
                Debug.Log($"[PlacementManager] Mouse Click Detected!");
                Debug.Log($"  ├─ Is Over UI: {isOverUI}");
                Debug.Log($"  ├─ lastCanPlace: {lastCanPlace}");
                Debug.Log($"  ├─ current: {(current != null ? current.name : "NULL")}");
                Debug.Log($"  ├─ ghost: {(ghost != null ? "EXISTS" : "NULL")}");
                Debug.Log($"  ├─ anchor: {lastAnchor}");
                Debug.Log($"  ├─ grid.CanPlace: {(grid != null ? grid.CanPlace(anchor, current.size, rot90).ToString() : "GRID NULL")}");

                if (current != null && current.buildCost != null && current.buildCost.Count > 0)
                {
                    Debug.Log($"  ├─ Build Cost: {current.buildCost.Count} items");
                    if (Inventory != null)
                        Debug.Log($"  └─ Can Afford: {Inventory.CanAffordBatch(current.buildCost, 1)}");
                    else
                        Debug.Log($"  └─ Inventory: NULL");
                }
            }

            // 如果点击在 UI 上，不处理放置
            if (isOverUI)
            {
                if (enableDebugLogs)
                    Debug.LogWarning("[PlacementManager] Click blocked - mouse is over UI!");
                return;
            }

            if (lastCanPlace)
                Place(anchor, center);
            else if (enableDebugLogs)
                Debug.LogWarning("[PlacementManager] Cannot place building - lastCanPlace is false!");
        }
    }

    // =========================
    // Mode control (single source of truth)
    // =========================
    public void SetMode(InteractionMode newMode, bool clearSelection)
    {
        if (_mode == newMode)
            return;

        ApplyMode(newMode, clearSelection);
    }

    private void ApplyMode(InteractionMode newMode, bool clearSelection)
    {
        InteractionMode oldMode = _mode;
        _mode = newMode;

        BuildMode = (_mode == InteractionMode.Build);
        DemolishMode = (_mode == InteractionMode.Demolish);

        if (enableDebugLogs)
            Debug.Log($"[PlacementManager] *** MODE CHANGED *** {oldMode} → {newMode} (clearSelection={clearSelection})");

        if (clearSelection)
            ClearCurrent();

        // 拆除模式不需要 ghost
        if (_mode != InteractionMode.Build && ghost != null)
        {
            Destroy(ghost);
            ghost = null;
        }

        // 统一控制网格显示
        bool overlaysOn = (_mode != InteractionMode.Normal);

        if (gridOverlayRoot != null)
            gridOverlayRoot.SetActive(overlaysOn);

        OnModeChanged?.Invoke(_mode);
    }

    // ===== UI API =====
    public void UI_ToggleBuild()
    {
        if (_mode == InteractionMode.Build) SetMode(InteractionMode.Normal, true);
        else SetMode(InteractionMode.Build, false);
    }

    public void UI_ToggleDemolish()
    {
        if (_mode == InteractionMode.Demolish) SetMode(InteractionMode.Normal, true);
        else SetMode(InteractionMode.Demolish, true);
    }

    public void UI_Cancel()
    {
        SetMode(InteractionMode.Normal, true);
    }

    public void UI_Rotate()
    {
        if (_mode != InteractionMode.Build || current == null) return;
        rot90 = (rot90 + 1) % 4;
    }

    // =========================
    // Placement core
    // =========================
    private bool TryGetGroundHit(out RaycastHit hit)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null)
        {
            if (enableDebugLogs)
                Debug.LogError("[PlacementManager] Camera is NULL!");
            hit = default;
            return false;
        }

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        bool didHit = Physics.Raycast(ray, out hit, 500f, groundMask);

        if (enableDebugLogs && !didHit && Time.frameCount % 60 == 0)
        {
            Debug.LogWarning($"[PlacementManager] Raycast MISS! Ground Mask: {groundMask.value}");
            Debug.LogWarning($"  Ray Origin: {ray.origin}, Direction: {ray.direction}");
            Debug.LogWarning($"  Mouse Position: {Input.mousePosition}");
        }

        return didHit;
    }

    private Vector3 GetFootprintCenterWorld(Vector2Int anchor, Vector2Int size, int r90)
    {
        bool swap = (r90 % 2) != 0;
        int w = swap ? size.y : size.x;
        int h = swap ? size.x : size.y;

        Vector3 baseCenter = grid.CellToWorldCenter(anchor);
        Vector3 offset = new Vector3((w - 1) * 0.5f * grid.cellSize, 0f, (h - 1) * 0.5f * grid.cellSize);
        return baseCenter + offset;
    }

    private void CreateGhost()
    {
        ghost = Instantiate(current.prefab);
        ghost.name = $"[Ghost]{current.displayName}";

        foreach (var c in ghost.GetComponentsInChildren<Collider>())
            c.enabled = false;

        foreach (var mb in ghost.GetComponentsInChildren<MonoBehaviour>())
            mb.enabled = false;
    }

    private void ApplyGhostMaterial(Material m)
    {
        if (m == null || ghost == null) return;
        foreach (var r in ghost.GetComponentsInChildren<Renderer>())
            r.sharedMaterial = m;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        if (root == null) return;
        root.layer = layer;
        foreach (Transform t in root.transform)
            if (t != null) SetLayerRecursively(t.gameObject, layer);
    }

    private void Place(Vector2Int anchor, Vector3 center)
    {
        // 扣费
        if (current.buildCost != null && current.buildCost.Count > 0)
        {
            if (globalInventory == null) globalInventory = GlobalInventory.Instance;
            if (Inventory == null) return;

            if (!Inventory.TryConsumeBatch(current.buildCost, 1))
            {
                Debug.LogWarning("[Placement] Not enough resources to build.");
                return;
            }
        }

        // 生成
        GameObject obj = Instantiate(current.prefab, center, Quaternion.Euler(0f, rot90 * 90f, 0f));
        obj.name = current.displayName;

        // 层：WalkwayTile -> Walkway层，否则 -> Building层
        bool isWalkway = obj.GetComponentInParent<WalkwayTile>() != null || obj.GetComponent<WalkwayTile>() != null;
        int targetLayer = isWalkway ? _walkwayLayer : _buildingLayer;
        if (targetLayer >= 0) SetLayerRecursively(obj, targetLayer);

        // 占地
        grid.Occupy(anchor, current.size, rot90, obj);

        // BuildableInstance
        var bi = obj.GetComponent<BuildableInstance>();
        if (bi == null) bi = obj.AddComponent<BuildableInstance>();
        bi.def = current;
        bi.anchor = anchor;
        bi.rot90 = rot90;
        bi.size = current.size;

        // Register building with BaseInstance
        if (grid != null)
        {
            var baseInstance = grid.GetComponentInParent<BaseInstance>();
            if (baseInstance != null)
            {
                baseInstance.AddBuilding(obj);
                Debug.Log($"[PlacementManager] Building '{current.displayName}' registered with BaseInstance");
            }
            else
            {
                Debug.LogWarning("[PlacementManager] Could not find BaseInstance to register building!");
            }
        }
    }

    public void SetCurrent(BuildableDefinition def)
    {
        current = def;
        rot90 = 0;

        if (ghost != null) Destroy(ghost);
        ghost = null;

        Debug.Log($"[PlacementManager] SetCurrent: selected building = {(def != null ? def.displayName : "null")}");
    }

    public void ClearCurrent()
    {
        current = null;
        rot90 = 0;

        if (ghost != null) Destroy(ghost);
        ghost = null;
    }

    // =========================
    // DockYard extra check (TEMPORARILY DISABLED)
    // =========================
    private bool ValidateDockYardIfNeeded()
    {
        // TODO: Re-enable walkway/highway adjacency check when ready
        // Original validation required:
        //   1. workerEntrancePoint on Walkway NavMesh
        //   2. dockPoints reachable from highwayEntryPoint via Vehicle NavMesh
        return true;

        /*
        if (ghost == null || current == null) return false;

        var dock = ghost.GetComponent<DockYard>();
        if (dock == null) return true;

        if (workerSurface == null || vehicleSurface == null || highwayEntryPoint == null)
            return false;

        if (dock.workerEntrancePoint == null || dock.dockPoints == null || dock.dockPoints.Length == 0)
            return false;

        var workerFilter = new NavMeshQueryFilter
        {
            agentTypeID = workerSurface.agentTypeID,
            areaMask = NavMesh.AllAreas
        };

        var vehicleFilter = new NavMeshQueryFilter
        {
            agentTypeID = vehicleSurface.agentTypeID,
            areaMask = NavMesh.AllAreas
        };

        bool workerOk = NavMesh.SamplePosition(
            dock.workerEntrancePoint.position,
            out _,
            navSampleRadius,
            workerFilter
        );
        if (!workerOk) return false;

        Vector3 start = highwayEntryPoint.position;
        if (!NavMesh.SamplePosition(start, out var sHit, navSampleRadius, vehicleFilter))
            return false;

        start = sHit.position;

        var path = new NavMeshPath();
        foreach (var dp in dock.dockPoints)
        {
            if (dp == null) continue;

            if (!NavMesh.SamplePosition(dp.position, out var dHit, navSampleRadius, vehicleFilter))
                continue;

            bool hasPath = NavMesh.CalculatePath(start, dHit.position, vehicleFilter, path);
            if (hasPath && path.status == NavMeshPathStatus.PathComplete)
                return true;
        }

        return false;
        */
    }

    // =========================
    // Demolish
    // =========================
    private void TryDemolishUnderCursor()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out var hit, 500f, buildingMask))
            return;

        var bi = hit.collider.GetComponentInParent<BuildableInstance>();

        GameObject root;
        BuildableDefinition def = null;
        Vector2Int anchor = default;
        Vector2Int size = Vector2Int.one;
        int r90 = 0;

        if (bi != null)
        {
            root = bi.gameObject;
            def = bi.def;
            anchor = bi.anchor;
            size = bi.size;
            r90 = bi.rot90;
        }
        else
        {
            root = hit.collider.transform.root.gameObject;
        }

        if (root == null) return;

        // 释放网格（你项目里 GridSystem 已经有 Release/ReleaseByRoot 才能编过）
        if (bi != null) grid.Release(anchor, size, r90, root);
        else grid.ReleaseByRoot(root);

        // 退费
        if (def != null && def.buildCost != null && def.buildCost.Count > 0)
        {
            if (globalInventory == null) globalInventory = GlobalInventory.Instance;
            if (Inventory != null)
            {
                float pct = Mathf.Clamp01(def.refundPercent);
                if (pct > 0f)
                {
                    var refund = new List<ResourceAmount>(def.buildCost.Count);
                    foreach (var ra in def.buildCost)
                    {
                        if (ra.res == null) continue;
                        int give = Mathf.RoundToInt(ra.amount * pct);
                        if (give > 0)
                            refund.Add(new ResourceAmount { res = ra.res, amount = give });
                    }
                    Inventory.AddBatch(refund, 1);
                }
            }
        }

        // Unregister building from BaseInstance
        if (grid != null)
        {
            var baseInstance = grid.GetComponentInParent<BaseInstance>();
            if (baseInstance != null)
            {
                baseInstance.RemoveBuilding(root);
                Debug.Log($"[PlacementManager] Building unregistered from BaseInstance");
            }
        }

        Destroy(root);
    }
}