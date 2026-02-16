using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BaseSceneLoader - 基地场景加载器
/// 在进入基地场景时，从SaveData重建基地的建筑和资源
/// 此组件应放在 BaseScene 场景中
/// </summary>
public class BaseSceneLoader : MonoBehaviour
{
    [Header("References")]
    public PlacementManager placementManager;
    public BuildCatalog buildCatalog;

    [Header("Resource Zones")]
    [Tooltip("可用的资源区类型（留空则自动查找所有 ResourceZoneType 资产）")]
    public List<ResourceZoneType> resourceZoneTypes = new();

    [Header("UI References (optional)")]
    [Tooltip("如果场景中有GlobalInventoryHUD，会自动设置为当前基地的库存")]
    public GlobalInventoryHUD inventoryHUD;

    [Tooltip("如果场景中有RuntimeGridOverlayGL，会自动设置grid引用")]
    public RuntimeGridOverlayGL gridOverlay;

    [Header("Runtime")]
    [SerializeField] private BaseInstance _currentBase;
    [SerializeField] private string _loadedBaseId;

    private Dictionary<string, BuildableDefinition> _buildingDefCache;

    // ============ Lifecycle ============
    private void Awake()
    {
        // Auto-find references if not set
        if (placementManager == null)
            placementManager = FindObjectOfType<PlacementManager>();

        if (inventoryHUD == null)
            inventoryHUD = FindObjectOfType<GlobalInventoryHUD>();

        if (gridOverlay == null)
            gridOverlay = FindObjectOfType<RuntimeGridOverlayGL>();

        // Cache building definitions for faster lookup
        CacheBuildingDefinitions();
    }

    private void Start()
    {
        // 延迟执行，确保所有组件（BaseManager、ProducerBuilding等）都已完成初始化
        Invoke(nameof(TryAutoRestoreResourceZone), 0.1f);
    }

    /// <summary>
    /// 自动检测并恢复资源区（不依赖 LoadBase 被调用）
    /// 适用于：直接在编辑器里Play建筑场景、LoadBase已被调用但资源区未设置等情况
    /// </summary>
    private void TryAutoRestoreResourceZone()
    {
        var baseInst = FindObjectOfType<BaseInstance>();
        if (baseInst == null || baseInst.ResourceZone != null) return;

        // 尝试从 BaseManager 的存档数据中获取资源区信息
        if (BaseManager.Instance != null)
        {
            // 优先用已加载的 baseId，否则用 BaseInstance 上的
            string baseId = !string.IsNullOrEmpty(_loadedBaseId) ? _loadedBaseId : baseInst.baseId;
            var saveData = BaseManager.Instance.GetBaseSaveData(baseId);

            if (saveData != null && !string.IsNullOrEmpty(saveData.resourceZoneTypeId))
            {
                var zoneType = FindResourceZoneType(saveData.resourceZoneTypeId);
                if (zoneType != null)
                {
                    baseInst.SetResourceZone(zoneType);
                    Debug.Log($"[BaseSceneLoader] 自动恢复基地 '{baseInst.baseName}' 的资源区: {zoneType.displayName}");

                    // 刷新所有 ProducerBuilding
                    var producers = FindObjectsOfType<ProducerBuilding>();
                    foreach (var p in producers)
                        p.RefreshResourceZone();
                }
            }
        }
    }

    // ============ Loading ============
    public void LoadBase(string baseId)
    {
        if (string.IsNullOrEmpty(baseId)) return;
        if (BaseManager.Instance == null) return;

        BaseSaveData saveData = BaseManager.Instance.GetBaseSaveData(baseId);
        if (saveData == null) return;

        CreateBaseFromSaveData(saveData);
        _loadedBaseId = baseId;
    }

    private void CreateBaseFromSaveData(BaseSaveData saveData)
    {
        // Find or create base instance
        _currentBase = FindObjectOfType<BaseInstance>();

        if (_currentBase == null)
        {
            GameObject baseGO = new GameObject("BaseInstance");
            _currentBase = baseGO.AddComponent<BaseInstance>();
        }

        // Setup base properties
        _currentBase.baseId = saveData.baseId;
        _currentBase.baseName = saveData.baseName;

        // 恢复资源区信息（从大地图带入基地场景）
        if (!string.IsNullOrEmpty(saveData.resourceZoneTypeId))
        {
            var zoneType = FindResourceZoneType(saveData.resourceZoneTypeId);
            if (zoneType != null)
            {
                _currentBase.SetResourceZone(zoneType);
                Debug.Log($"[BaseSceneLoader] 基地 '{saveData.baseName}' 恢复资源区: {zoneType.displayName}");
            }
        }

        // Setup inventory
        if (_currentBase.inventory == null)
        {
            var invGO = new GameObject("Inventory");
            invGO.transform.SetParent(_currentBase.transform);
            _currentBase.inventory = invGO.AddComponent<BaseInventory>();
        }

        _currentBase.inventory.ownerBase = _currentBase;
        _currentBase.inventory.money = saveData.money;
        _currentBase.inventory.baseCapacity = saveData.baseCapacity;

        // Load resources
        LoadResources(saveData, _currentBase.inventory);

        // Setup grid
        if (_currentBase.grid == null)
        {
            var gridGO = new GameObject("GridSystem");
            gridGO.transform.SetParent(_currentBase.transform);
            _currentBase.grid = gridGO.AddComponent<GridSystem>();
        }

        _currentBase.grid.origin = saveData.gridOrigin;
        _currentBase.grid.width = saveData.gridWidth > 0 ? saveData.gridWidth : 100;
        _currentBase.grid.height = saveData.gridHeight > 0 ? saveData.gridHeight : 100;
        _currentBase.grid.cellSize = saveData.gridCellSize > 0f ? saveData.gridCellSize : 1f;

        Debug.Log($"[BaseSceneLoader] Grid setup: origin={_currentBase.grid.origin}, " +
                  $"size={_currentBase.grid.width}x{_currentBase.grid.height}, cellSize={_currentBase.grid.cellSize}");

        // Update PlacementManager reference
        if (placementManager != null)
        {
            placementManager.grid = _currentBase.grid;
            placementManager.globalInventory = _currentBase.inventory;
        }

        // Update GridOverlay reference
        if (gridOverlay != null)
        {
            gridOverlay.grid = _currentBase.grid;
        }

        // Update UI references
        if (inventoryHUD != null)
        {
            inventoryHUD.inventoryComponent = _currentBase.inventory;
        }

        // Load buildings
        LoadBuildings(saveData, _currentBase);

        // 统一更新所有建筑的库存引用为当前基地的 BaseInventory
        // （建筑的 Awake 在 Instantiate 时运行，会绑定到 GlobalInventory，需要修正为 BaseInventory）
        RebindAllBuildingInventories(_currentBase.inventory);

        // 刷新所有 ProducerBuilding 的资源区检查
        // （因为建筑的 Awake/Update 可能在 SetResourceZone 之前已执行过 CheckResourceZone）
        if (!string.IsNullOrEmpty(saveData.resourceZoneTypeId))
        {
            var allProducers = FindObjectsOfType<ProducerBuilding>();
            foreach (var p in allProducers)
                p.RefreshResourceZone();
            Debug.Log($"[BaseSceneLoader] 刷新了 {allProducers.Length} 个 ProducerBuilding 的资源区检查");
        }
    }

    private void LoadResources(BaseSaveData saveData, BaseInventory inventory)
    {
        if (saveData.resources == null || saveData.resources.Count == 0) return;

        foreach (var resData in saveData.resources)
        {
            ResourceDefinition resDef = FindResourceDefinition(resData.resourceName);
            if (resDef != null)
            {
                inventory.Add(resDef, resData.amount);
            }
        }
    }

    private void LoadBuildings(BaseSaveData saveData, BaseInstance baseInstance)
    {
        if (saveData.buildings == null || saveData.buildings.Count == 0)
            return;

        foreach (var buildingData in saveData.buildings)
        {
            BuildableDefinition def = GetBuildingDefinition(buildingData.buildingDefName);
            if (def == null) continue;

            // Instantiate building at saved position
            // 使用与 PlacementManager.GetFootprintCenterWorld 相同的计算方式
            Vector3 worldPos = GetFootprintCenterWorld(baseInstance.grid, buildingData.anchor, def.size, buildingData.rotation);
            worldPos.y = buildingData.worldY;  // 恢复保存的 Y 坐标
            Quaternion rotation = Quaternion.Euler(0f, buildingData.rotation * 90f, 0f);

            Debug.Log($"[BaseSceneLoader] Loading building '{def.displayName}' anchor={buildingData.anchor} " +
                      $"rot={buildingData.rotation} → worldPos={worldPos}");

            GameObject buildingGO = Instantiate(def.prefab, worldPos, rotation);
            buildingGO.name = def.displayName;

            // 设置 Layer（与 PlacementManager 保持一致：Building 层用于射线检测）
            bool isWalkway = buildingGO.GetComponent<WalkwayTile>() != null;
            int targetLayer = LayerMask.NameToLayer(isWalkway ? "Walkway" : "Building");
            if (targetLayer >= 0)
                SetLayerRecursively(buildingGO, targetLayer);

            // Setup BuildableInstance
            var buildableInstance = buildingGO.GetComponent<BuildableInstance>();
            if (buildableInstance == null)
                buildableInstance = buildingGO.AddComponent<BuildableInstance>();

            buildableInstance.def = def;
            buildableInstance.anchor = buildingData.anchor;
            buildableInstance.rot90 = buildingData.rotation;

            // Occupy grid
            baseInstance.grid.Occupy(buildingData.anchor, def.size, buildingData.rotation, buildingGO);

            // Add to base
            baseInstance.AddBuilding(buildingGO);

            // Restore worker data if available
            if (buildingData.assignedWorkers > 0 || buildingData.desiredWorkers > 0)
            {
                var worksite = buildingGO.GetComponent<Worksite>();
                if (worksite != null)
                {
                    worksite.desiredWorkers = buildingData.desiredWorkers;
                }
            }
        }
    }

    /// <summary>
    /// 将场景中所有建筑的库存引用统一绑定到指定的 BaseInventory
    /// 解决：建筑 Awake 时绑定到 GlobalInventory，但应该使用基地独立的 BaseInventory
    /// </summary>
    private void RebindAllBuildingInventories(BaseInventory baseInventory)
    {
        int rebound = 0;

        // 更新所有 ProducerBuilding
        var producers = FindObjectsOfType<ProducerBuilding>();
        foreach (var p in producers)
        {
            p.SetInventory(baseInventory);
            rebound++;
        }

        // 更新所有 DockYard
        var docks = FindObjectsOfType<DockYard>();
        foreach (var d in docks)
        {
            d.SetInventory(baseInventory);
            rebound++;
        }

        // 更新 GlobalInventoryHUD（确保 inventoryComponent 设置后重新订阅事件）
        if (inventoryHUD != null)
        {
            // 先取消旧订阅
            inventoryHUD.enabled = false;
            inventoryHUD.inventoryComponent = baseInventory;
            // 重新启用触发 OnEnable 重新订阅
            inventoryHUD.enabled = true;
        }

        if (rebound > 0)
            Debug.Log($"[BaseSceneLoader] 重新绑定了 {rebound} 个建筑的库存引用到 BaseInventory");
    }

    // ============ Saving ============
    public void SaveCurrentBase()
    {
        if (_currentBase == null)
        {
            // Try to find BaseInstance manually
            var allBases = FindObjectsOfType<BaseInstance>();
            if (allBases.Length > 0)
            {
                _currentBase = allBases[0];
            }
            else
            {
                return;
            }
        }

        BaseSaveData saveData = CreateSaveDataFromBase(_currentBase);

        if (BaseManager.Instance != null)
        {
            BaseManager.Instance.UpdateBaseSaveData(saveData);
        }
    }

    /// <summary>
    /// 保存当前基地并返回大地图
    /// Called by Return button - saves current base and returns to world map
    /// </summary>
    public void SaveAndReturn()
    {
        SaveCurrentBase();
        CleanupOverlays();

        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.ReturnToWorldMap();
        }
    }

    /// <summary>
    /// Clean up all overlays and visual elements before scene transition
    /// </summary>
    private void CleanupOverlays()
    {
        // Disable grid overlay
        if (gridOverlay != null)
            gridOverlay.enabled = false;

        // Find and disable all RuntimeGridOverlayGL components
        var allGridOverlays = FindObjectsOfType<RuntimeGridOverlayGL>();
        foreach (var overlay in allGridOverlays)
            overlay.enabled = false;

        // Find and disable all PowerHeatmapOverlay components
        var allHeatmaps = FindObjectsOfType<PowerHeatmapOverlay>();
        foreach (var heatmap in allHeatmaps)
        {
            heatmap.SetVisible(false);
            heatmap.enabled = false;
        }

        // Find and disable PlacementManager's overlays
        var placementManager = FindObjectOfType<PlacementManager>();
        if (placementManager != null)
        {
            if (placementManager.gridOverlayRoot != null)
                placementManager.gridOverlayRoot.SetActive(false);

            if (placementManager.heatmapOverlay != null)
            {
                placementManager.heatmapOverlay.SetVisible(false);
                placementManager.heatmapOverlay.enabled = false;
            }
        }
    }

    private BaseSaveData CreateSaveDataFromBase(BaseInstance baseInstance)
    {
        // 获取原有保存数据以保留 worldPosition（基地场景中的位置是局部的，不是世界地图位置）
        Vector3 worldPosition = Vector3.zero;
        if (BaseManager.Instance != null)
        {
            var existingSaveData = BaseManager.Instance.GetBaseSaveData(baseInstance.baseId);
            if (existingSaveData != null)
            {
                worldPosition = existingSaveData.worldPosition;
            }
        }

        BaseSaveData saveData = new BaseSaveData(
            baseInstance.baseId,
            baseInstance.baseName,
            worldPosition  // 使用原有的 worldPosition，不是 baseInstance.Position
        );

        // 保留资源区信息（优先从 BaseInstance 获取，其次从原存档获取）
        if (baseInstance.ResourceZone != null)
        {
            saveData.resourceZoneTypeId = baseInstance.ResourceZone.zoneId;
        }
        else if (BaseManager.Instance != null)
        {
            var existingData = BaseManager.Instance.GetBaseSaveData(baseInstance.baseId);
            if (existingData != null && !string.IsNullOrEmpty(existingData.resourceZoneTypeId))
            {
                saveData.resourceZoneTypeId = existingData.resourceZoneTypeId;
            }
        }

        // Save grid settings
        if (baseInstance.grid != null)
        {
            saveData.gridOrigin = baseInstance.grid.origin;
            saveData.gridWidth = baseInstance.grid.width;
            saveData.gridHeight = baseInstance.grid.height;
            saveData.gridCellSize = baseInstance.grid.cellSize;
        }

        // Save inventory
        if (baseInstance.inventory != null)
        {
            saveData.money = baseInstance.inventory.Money;
            saveData.baseCapacity = baseInstance.inventory.baseCapacity;

            var resources = baseInstance.inventory.GetAllResources();
            foreach (var res in resources)
            {
                // 保存所有资源，包括数量为0的（这样玩家可以看到资源被消耗完了）
                if (res.res != null)
                {
                    saveData.resources.Add(new ResourceSaveData(res.res.name, Mathf.Max(0, res.amount)));
                }
            }
        }

        // Save buildings
        var buildings = baseInstance.GetAllBuildings();
        foreach (var building in buildings)
        {
            var buildableInstance = building.GetComponent<BuildableInstance>();
            if (buildableInstance == null || buildableInstance.def == null)
                continue;

            var buildingData = new BuildingSaveData(
                buildableInstance.def.name,
                buildableInstance.anchor,
                buildableInstance.rot90,
                building.transform.position.y  // 保存建筑实际 Y 坐标
            );

            // Save worker data
            var worksite = building.GetComponent<Worksite>();
            if (worksite != null)
            {
                buildingData.desiredWorkers = worksite.desiredWorkers;
                buildingData.assignedWorkers = worksite.PresentWorkers;
            }

            saveData.buildings.Add(buildingData);
        }

        // 检测是否有装卸码头（大地图运输必需）
        var dockYards = baseInstance.GetBuildingsOfType<DockYard>();
        saveData.hasDockYard = dockYards.Length > 0;

        // 同步装卸码头的实际装卸时间（受卡片加速影响）到存档，供大地图运输使用
        if (dockYards.Length > 0)
        {
            // 取所有码头中最快的装卸时间（多码头取最优）
            float bestLoadingTime = float.MaxValue;
            foreach (var dock in dockYards)
            {
                float effective = dock.EffectiveLoadingSeconds;
                if (effective < bestLoadingTime)
                    bestLoadingTime = effective;
            }
            saveData.dockLoadingSeconds = bestLoadingTime;
        }

        // 保存资源流动数据（用于离线计算）
        SaveResourceFlows(baseInstance, saveData);

        return saveData;
    }

    /// <summary>
    /// 计算并保存资源流动数据
    /// </summary>
    private void SaveResourceFlows(BaseInstance baseInstance, BaseSaveData saveData)
    {
        var flowDict = new Dictionary<string, (float consume, float produce)>();

        // 收集所有 ProducerBuilding 的输入输出
        var producers = baseInstance.GetBuildingsOfType<ProducerBuilding>();
        foreach (var producer in producers)
        {
            if (producer == null) continue;

            // 计算效率
            float efficiency = producer.LastEfficiency > 0 ? producer.LastEfficiency : 1f;

            // 输入（消耗）- 每秒 -> 每分钟
            if (producer.inputsPerSecond != null)
            {
                foreach (var input in producer.inputsPerSecond)
                {
                    if (input.res == null) continue;
                    string resName = input.res.name;

                    if (!flowDict.ContainsKey(resName))
                        flowDict[resName] = (0f, 0f);

                    var current = flowDict[resName];
                    current.consume += input.amount * efficiency * 60f;
                    flowDict[resName] = current;
                }
            }

            // 输出（生产）- 每秒 -> 每分钟（含资源区效率加成）
            if (producer.outputsPerSecond != null)
            {
                float zoneMul = (producer.HasResourceZoneBonus && producer.ActiveResourceZone != null)
                    ? producer.ActiveResourceZone.efficiencyBonus : 1f;

                foreach (var output in producer.outputsPerSecond)
                {
                    if (output.res == null) continue;
                    string resName = output.res.name;

                    if (!flowDict.ContainsKey(resName))
                        flowDict[resName] = (0f, 0f);

                    var current = flowDict[resName];
                    current.produce += output.amount * efficiency * zoneMul * 60f;
                    flowDict[resName] = current;
                }
            }
        }

        // 转换为保存数据
        foreach (var kvp in flowDict)
        {
            saveData.resourceFlows.Add(new ResourceFlowSaveData(
                kvp.Key,
                kvp.Value.consume,
                kvp.Value.produce
            ));
        }
    }

    // ============ Helpers ============

    /// <summary>
    /// 递归设置 Layer（与 PlacementManager.SetLayerRecursively 保持一致）
    /// </summary>
    private static void SetLayerRecursively(GameObject root, int layer)
    {
        if (root == null) return;
        root.layer = layer;
        foreach (Transform t in root.transform)
            if (t != null) SetLayerRecursively(t.gameObject, layer);
    }

    /// <summary>
    /// 计算建筑占地中心的世界坐标（与 PlacementManager.GetFootprintCenterWorld 保持一致）
    /// </summary>
    private Vector3 GetFootprintCenterWorld(GridSystem grid, Vector2Int anchor, Vector2Int size, int rot90)
    {
        bool swap = (rot90 % 2) != 0;
        int w = swap ? size.y : size.x;
        int h = swap ? size.x : size.y;

        Vector3 baseCenter = grid.CellToWorldCenter(anchor);
        Vector3 offset = new Vector3((w - 1) * 0.5f * grid.cellSize, 0f, (h - 1) * 0.5f * grid.cellSize);
        return baseCenter + offset;
    }

    private void CacheBuildingDefinitions()
    {
        _buildingDefCache = new Dictionary<string, BuildableDefinition>();

        if (buildCatalog != null && buildCatalog.buildables != null)
        {
            foreach (var def in buildCatalog.buildables)
            {
                if (def != null)
                    _buildingDefCache[def.name] = def;
            }
        }
    }

    private BuildableDefinition GetBuildingDefinition(string defName)
    {
        if (_buildingDefCache.TryGetValue(defName, out var def))
            return def;

        return null;
    }

    private ResourceDefinition FindResourceDefinition(string resourceName)
    {
        // Try to load from Resources folder
        return Resources.Load<ResourceDefinition>(resourceName);
    }

    /// <summary>
    /// 根据 zoneId 查找 ResourceZoneType 资产
    /// 查找顺序：Inspector列表 → BaseManager（跨场景） → WorldMapManager
    /// 不需要手动拖入，只要 BaseManager 上配置了就行
    /// </summary>
    private ResourceZoneType FindResourceZoneType(string zoneTypeId)
    {
        if (string.IsNullOrEmpty(zoneTypeId)) return null;

        // 1. 从 Inspector 手动拖入的列表查找（可选）
        if (resourceZoneTypes != null)
        {
            foreach (var zt in resourceZoneTypes)
            {
                if (zt != null && zt.zoneId == zoneTypeId)
                    return zt;
            }
        }

        // 2. 从 BaseManager 查找（DontDestroyOnLoad，跨场景可用）
        if (BaseManager.Instance != null)
        {
            var found = BaseManager.Instance.FindResourceZoneType(zoneTypeId);
            if (found != null) return found;
        }

        // 3. 从 WorldMapManager 查找（如果碰巧在同一场景中）
        if (WorldMapManager.Instance != null)
        {
            var found = WorldMapManager.Instance.GetResourceZoneType(zoneTypeId);
            if (found != null) return found;
        }

        Debug.LogWarning($"[BaseSceneLoader] 未找到资源区类型 '{zoneTypeId}'。请在 BaseManager 的 Resource Zone Types 列表中拖入对应资产");
        return null;
    }

    // ============ Debug ============
#if UNITY_EDITOR
    [ContextMenu("Debug: Save Current Base")]
    private void DebugSaveCurrentBase()
    {
        if (!Application.isPlaying) return;
        SaveCurrentBase();
    }
#endif
}