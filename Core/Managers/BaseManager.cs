using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BaseManager - 全局基地管理器（持久化）
/// 管理所有基地的元数据和存档，支持场景切换
/// </summary>
public class BaseManager : MonoBehaviour
{
    public static BaseManager Instance { get; private set; }

    [Header("Prefabs")]
    public GameObject baseInstancePrefab;
    [Tooltip("大地图上基地3D标记的预制体（需包含 BaseMarker3D 组件）")]
    public GameObject baseMarkerPrefab;

    [Header("Default Settings")]
    public int defaultGridWidth = 100;
    public int defaultGridHeight = 100;
    public float defaultCellSize = 1f;
    public int defaultBaseCapacity = 100;
    public float defaultStartingMoney = 1000f;

    [Header("Starting Resources")]
    [Tooltip("新基地的初始资源（留空则无初始资源）")]
    public List<StartingResource> startingResources = new();

    [Header("Resource Zone Types")]
    [Tooltip("所有资源区类型（拖入 ResourceZoneType 资产，跨场景自动可用）")]
    public List<ResourceZoneType> resourceZoneTypes = new();

    [Header("Save/Load")]
    public string saveDirectory = "Saves";
    public string saveFileName = "GameSave.json";

    [Header("World Map Grid")]
    public WorldMapGrid worldGrid;
    public Vector2Int baseGridSize = new Vector2Int(3, 3);  // 基地在大地图上占用的格子数

    [Header("Runtime - Save Data")]
    [SerializeField] private List<BaseSaveData> _baseDataList = new();
    [SerializeField] private string _activeBaseId;

    [Header("Runtime - Scene Instance (temporary)")]
    [SerializeField] private List<BaseInstance> _sceneInstances = new();

    // Events
    public event Action<string> OnBaseCreated;  // baseId
    public event Action<string> OnBaseDestroyed;
    public event Action<string> OnActiveBaseChanged;

    // ============ Public Properties ============
    public string ActiveBaseId => _activeBaseId;
    public int BaseCount => _baseDataList.Count;
    public List<BaseSaveData> AllBaseSaveData => new List<BaseSaveData>(_baseDataList);

    // ============ Lifecycle ============
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // 将新实例的预制体引用传递给已存在的实例（场景切换后引用可能丢失）
            if (baseMarkerPrefab != null)
                Instance.baseMarkerPrefab = baseMarkerPrefab;
            if (baseInstancePrefab != null)
                Instance.baseInstancePrefab = baseInstancePrefab;
            if (worldGrid != null)
                Instance.worldGrid = worldGrid;
            // 传递资源区类型（场景切换后 ScriptableObject 引用仍然有效）
            if (resourceZoneTypes != null && resourceZoneTypes.Count > 0 && Instance.resourceZoneTypes.Count == 0)
                Instance.resourceZoneTypes = resourceZoneTypes;

            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadGameData();
    }

    private void OnApplicationQuit()
    {
        SaveGameData();
    }

    // ============ Scene Instance Registration (temporary) ============
    public void RegisterBase(BaseInstance baseInstance)
    {
        if (baseInstance == null) return;

        if (!_sceneInstances.Contains(baseInstance))
        {
            _sceneInstances.Add(baseInstance);
        }
    }

    public void UnregisterBase(BaseInstance baseInstance)
    {
        _sceneInstances.Remove(baseInstance);
    }

    // ============ Base Data Management ============

    /// <summary>
    /// 创建新基地（仅数据，不实例化）
    /// 在大地图点击位置时调用
    /// </summary>
    public BaseSaveData CreateNewBase(Vector3 worldPosition, string baseName = null)
    {
        // Snap to world grid if available
        Vector3 snappedPosition = worldPosition;
        Vector2Int anchorCell = Vector2Int.zero;
        bool hasGrid = worldGrid != null;

        if (hasGrid)
        {
            snappedPosition = worldGrid.SnapToGrid(worldPosition);
            Vector2Int clickedCell = worldGrid.WorldToCell(snappedPosition);

            // 将点击位置作为基地中心，计算左下角锚点
            anchorCell = clickedCell - baseGridSize / 2;

            // 先检查是否可以放置（不实际占用）
            if (!worldGrid.CanOccupyArea(anchorCell, baseGridSize))
            {
                Debug.LogError($"[BaseManager] Cannot create base at {worldPosition}: Grid cells are occupied or out of bounds");
                return null;
            }

            // 更新 snappedPosition 为锚点的世界坐标（存档中存储的是锚点位置）
            snappedPosition = worldGrid.CellToWorldCenter(anchorCell);
        }

        string baseId = System.Guid.NewGuid().ToString();
        string name = !string.IsNullOrEmpty(baseName) ? baseName : $"Base {_baseDataList.Count + 1}";

        BaseSaveData newBase = new BaseSaveData(baseId, name, snappedPosition);

        // Setup default settings
        newBase.gridOrigin = Vector3.zero;  // 网格系统原点统一设置为(0,0,0)
        newBase.gridWidth = defaultGridWidth;
        newBase.gridHeight = defaultGridHeight;
        newBase.gridCellSize = defaultCellSize;
        newBase.money = defaultStartingMoney;
        newBase.baseCapacity = defaultBaseCapacity;

        // 查询该位置的资源区并保存到 SaveData
        if (WorldMapManager.Instance != null)
        {
            Vector2Int mapCell = WorldMapManager.Instance.WorldToCell(snappedPosition);
            var zoneType = WorldMapManager.Instance.GetCellResourceZone(mapCell);
            if (zoneType != null)
            {
                newBase.resourceZoneTypeId = zoneType.zoneId;
                Debug.Log($"[BaseManager] 新基地 '{name}' 位于资源区 '{zoneType.displayName}'");
            }
        }

        // Add starting resources
        if (startingResources != null && startingResources.Count > 0)
        {
            foreach (var startingRes in startingResources)
            {
                if (startingRes.resource != null && startingRes.amount > 0)
                {
                    newBase.resources.Add(new ResourceSaveData(startingRes.resource.name, startingRes.amount));
                }
            }
        }

        _baseDataList.Add(newBase);

        // Set as active if it's the first base
        if (string.IsNullOrEmpty(_activeBaseId))
            _activeBaseId = baseId;

        // 正式占用大地图网格（带baseId）
        if (hasGrid)
        {
            worldGrid.TryOccupyArea(anchorCell, baseGridSize, WorldMapGrid.CellType.Base, baseId);
        }

        OnBaseCreated?.Invoke(baseId);

        // 自动生成大地图上的3D标记
        CreateBaseMarker(baseId, snappedPosition, name);

        SaveGameData();

        return newBase;
    }

    /// <summary>
    /// 在大地图上创建基地3D标记
    /// </summary>
    private void CreateBaseMarker(string baseId, Vector3 worldPosition, string baseName)
    {
        if (baseMarkerPrefab == null)
        {
            Debug.LogError("[BaseManager] baseMarkerPrefab is not assigned!");
            return;
        }

        // 计算基地区域的中心位置（worldPosition 是左下角锚点）
        Vector3 markerPosition = worldPosition;
        if (worldGrid != null)
        {
            // 偏移到 baseGridSize 区域的中心
            float cellSize = worldGrid.cellSize;
            markerPosition += new Vector3(
                (baseGridSize.x - 1) * 0.5f * cellSize,
                0f,
                (baseGridSize.y - 1) * 0.5f * cellSize
            );
        }

        // 实例化标记预制体
        GameObject markerObj = Instantiate(baseMarkerPrefab, markerPosition, Quaternion.identity);
        markerObj.name = $"BaseMarker_{baseName}";

        // 获取或添加 BaseMarker3D 组件
        BaseMarker3D markerComponent = markerObj.GetComponent<BaseMarker3D>();
        if (markerComponent == null)
        {
            // BaseMarker3D 需要 Collider，先确保有 Collider
            if (markerObj.GetComponent<Collider>() == null)
            {
                markerObj.AddComponent<BoxCollider>();
            }
            markerComponent = markerObj.AddComponent<BaseMarker3D>();
        }

        // 配置标记组件
        markerComponent.baseId = baseId;
        markerComponent.autoFindBaseInstance = false;
    }

    /// <summary>
    /// 为所有已保存的基地生成大地图标记
    /// 应在大地图场景加载时调用
    /// </summary>
    public void LoadAllBaseMarkers()
    {
        // 如果预制体丢失，尝试从 Resources 加载
        if (baseMarkerPrefab == null)
        {
            baseMarkerPrefab = Resources.Load<GameObject>("BaseMarkerPrefab");
        }

        if (baseMarkerPrefab == null)
        {
            Debug.LogError("[BaseManager] baseMarkerPrefab is NULL! Make sure prefab is assigned or placed in Resources folder");
            return;
        }

        // 清除现有标记（防止重复）
        BaseMarker3D[] existingMarkers = FindObjectsOfType<BaseMarker3D>();
        foreach (var marker in existingMarkers)
        {
            if (marker.gameObject.name.StartsWith("BaseMarker_"))
            {
                Destroy(marker.gameObject);
            }
        }

        // 为每个基地创建标记
        foreach (var baseData in _baseDataList)
        {
            CreateBaseMarker(baseData.baseId, baseData.worldPosition, baseData.baseName);
        }
    }

    public void UpdateBaseSaveData(BaseSaveData saveData)
    {
        if (saveData == null || string.IsNullOrEmpty(saveData.baseId))
        {
            Debug.LogError("[BaseManager] Invalid save data");
            return;
        }

        BaseSaveData existing = _baseDataList.Find(b => b.baseId == saveData.baseId);
        if (existing != null)
        {
            _baseDataList.Remove(existing);
        }

        _baseDataList.Add(saveData);

        // Save to disk immediately
        SaveGameData();
    }

    public void DeleteBase(string baseId)
    {
        BaseSaveData toRemove = _baseDataList.Find(b => b.baseId == baseId);
        if (toRemove != null)
        {
            _baseDataList.Remove(toRemove);

            if (_activeBaseId == baseId)
            {
                _activeBaseId = _baseDataList.Count > 0 ? _baseDataList[0].baseId : null;
            }

            OnBaseDestroyed?.Invoke(baseId);

            SaveGameData();
        }
    }

    // ============ Active Base Management ============
    public void SetActiveBase(string baseId)
    {
        if (string.IsNullOrEmpty(baseId))
        {
            Debug.LogWarning("[BaseManager] Cannot set active base: invalid ID");
            return;
        }

        BaseSaveData baseData = _baseDataList.Find(b => b.baseId == baseId);
        if (baseData == null)
        {
            Debug.LogWarning($"[BaseManager] Base not found: {baseId}");
            return;
        }

        _activeBaseId = baseId;
        OnActiveBaseChanged?.Invoke(_activeBaseId);
    }

    // ============ Query Methods ============

    /// <summary>
    /// 根据 zoneId 查找 ResourceZoneType（跨场景可用）
    /// </summary>
    public ResourceZoneType FindResourceZoneType(string zoneTypeId)
    {
        if (string.IsNullOrEmpty(zoneTypeId)) return null;

        foreach (var zt in resourceZoneTypes)
        {
            if (zt != null && zt.zoneId == zoneTypeId)
                return zt;
        }
        return null;
    }

    public BaseSaveData GetBaseSaveData(string baseId)
    {
        return _baseDataList.Find(b => b.baseId == baseId);
    }

    public BaseSaveData GetActiveBaseSaveData()
    {
        if (string.IsNullOrEmpty(_activeBaseId)) return null;
        return GetBaseSaveData(_activeBaseId);
    }

    public BaseSaveData GetNearestBase(Vector3 position)
    {
        if (_baseDataList.Count == 0) return null;

        BaseSaveData nearest = null;
        float minDist = float.MaxValue;

        foreach (var baseData in _baseDataList)
        {
            float dist = Vector3.Distance(position, baseData.worldPosition);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = baseData;
            }
        }

        return nearest;
    }

    // ============ Save/Load ============
    private void SaveGameData()
    {
        // 如果不在大地图场景，先读取已有存档中的大地图数据，避免覆盖
        GameSaveData existingSave = null;
        string filePath = System.IO.Path.Combine(Application.persistentDataPath, saveDirectory, saveFileName);
        bool worldMapManagerAvailable = WorldMapManager.Instance != null;
        bool roadNetworkAvailable = FindObjectOfType<RoadNetwork>() != null;
        bool npcManagerAvailable = NPCManager.Instance != null;

        if (!worldMapManagerAvailable || !roadNetworkAvailable || !npcManagerAvailable)
        {
            // 不在大地图场景（部分Manager不存在），先读取已有存档保留大地图数据
            if (System.IO.File.Exists(filePath))
            {
                existingSave = GameSaveData.LoadFromFile(filePath);
            }
        }

        GameSaveData gameData = new GameSaveData();
        gameData.bases = _baseDataList;
        gameData.currentActiveBaseId = _activeBaseId;

        // 保存贸易路线和运输订单
        if (TradeManager.Instance != null)
        {
            gameData.tradeRoutes = TradeManager.Instance.GetAllRoutesForSave();
            gameData.activeTransportOrders = TradeManager.Instance.GetActiveOrdersForSave();
        }
        else if (existingSave != null)
        {
            gameData.tradeRoutes = existingSave.tradeRoutes;
            gameData.activeTransportOrders = existingSave.activeTransportOrders;
        }

        // 保存道路网络
        if (roadNetworkAvailable)
        {
            gameData.roads = FindObjectOfType<RoadNetwork>().GetAllRoadsForSave();
        }
        else if (existingSave != null)
        {
            // 保留已有存档中的道路数据
            gameData.roads = existingSave.roads;
        }
        else if (_pendingRoads != null)
        {
            // 保留尚未flush的pending数据
            gameData.roads = _pendingRoads;
        }

        // 保存大地图格子数据（资源区、威胁区、NPC领地等）
        if (worldMapManagerAvailable)
        {
            gameData.worldMapCells = WorldMapManager.Instance.GetAllCellDataForSave();
        }
        else if (existingSave != null)
        {
            gameData.worldMapCells = existingSave.worldMapCells;
        }
        else if (_pendingWorldMapCells != null)
        {
            gameData.worldMapCells = _pendingWorldMapCells;
        }

        // 保存NPC据点和声望数据
        if (npcManagerAvailable)
        {
            gameData.npcData = NPCManager.Instance.GetSaveData();
        }
        else if (existingSave != null)
        {
            gameData.npcData = existingSave.npcData;
        }
        else if (_pendingNPCData != null)
        {
            gameData.npcData = _pendingNPCData;
        }

        // 保存任务数据
        if (QuestManager.Instance != null)
        {
            gameData.questData = QuestManager.Instance.GetSaveData();
        }
        else if (existingSave != null)
        {
            gameData.questData = existingSave.questData;
        }
        else if (_pendingQuestData != null)
        {
            gameData.questData = _pendingQuestData;
        }

        // 保存载具运输数据
        if (VehicleTransportManager.Instance != null)
        {
            gameData.vehicleTransportData = VehicleTransportManager.Instance.GetSaveData();
        }
        else if (existingSave != null)
        {
            gameData.vehicleTransportData = existingSave.vehicleTransportData;
        }
        else if (_pendingVehicleTransportData != null)
        {
            gameData.vehicleTransportData = _pendingVehicleTransportData;
        }

        // 保存玩家背包数据
        if (PlayerInventoryManager.Instance != null)
        {
            gameData.playerInventoryData = PlayerInventoryManager.Instance.GetSaveData();
        }
        else if (existingSave != null)
        {
            gameData.playerInventoryData = existingSave.playerInventoryData;
        }
        else if (_pendingInventoryData != null)
        {
            gameData.playerInventoryData = _pendingInventoryData;
        }

        // 保存角色养成数据
        if (CharacterProgressionManager.Instance != null)
        {
            gameData.characterProgressionData = CharacterProgressionManager.Instance.GetSaveData();
        }
        else if (existingSave != null)
        {
            gameData.characterProgressionData = existingSave.characterProgressionData;
        }
        else if (_pendingProgressionData != null)
        {
            gameData.characterProgressionData = _pendingProgressionData;
        }

        string dirPath = System.IO.Path.Combine(Application.persistentDataPath, saveDirectory);
        if (!System.IO.Directory.Exists(dirPath))
            System.IO.Directory.CreateDirectory(dirPath);

        gameData.SaveToFile(filePath);
    }

    private void LoadGameData()
    {
        string filePath = System.IO.Path.Combine(Application.persistentDataPath, saveDirectory, saveFileName);

        if (!System.IO.File.Exists(filePath))
        {
            return;
        }

        GameSaveData gameData = GameSaveData.LoadFromFile(filePath);
        if (gameData != null)
        {
            _baseDataList = gameData.bases ?? new List<BaseSaveData>();
            _activeBaseId = gameData.currentActiveBaseId;

            // 恢复贸易路线和运输订单（TradeManager可能尚未初始化，延迟加载）
            _pendingTradeRoutes = gameData.tradeRoutes;
            _pendingTransportOrders = gameData.activeTransportOrders;

            // 恢复道路、地图格子、NPC数据（这些Manager可能尚未初始化，延迟加载）
            _pendingRoads = gameData.roads;
            _pendingWorldMapCells = gameData.worldMapCells;
            _pendingNPCData = gameData.npcData;

            // 恢复任务和载具运输数据
            _pendingQuestData = gameData.questData;
            _pendingVehicleTransportData = gameData.vehicleTransportData;

            // 恢复养成数据
            _pendingInventoryData = gameData.playerInventoryData;
            _pendingProgressionData = gameData.characterProgressionData;
        }
    }

    // 延迟加载的贸易数据（等待 TradeManager 初始化后加载）
    private List<TradeRoute> _pendingTradeRoutes;
    private List<WorldMapTransportOrder> _pendingTransportOrders;

    // 延迟加载的大地图数据（等待各Manager初始化后加载）
    private List<RoadSegment> _pendingRoads;
    private List<WorldMapCellData> _pendingWorldMapCells;
    private NPCManagerSaveData _pendingNPCData;

    // 延迟加载的任务和载具运输数据
    private QuestManagerSaveData _pendingQuestData;
    private VehicleTransportSaveData _pendingVehicleTransportData;

    // 延迟加载的养成数据
    private PlayerInventorySaveData _pendingInventoryData;
    private CharacterProgressionSaveData _pendingProgressionData;

    /// <summary>
    /// 将待加载的贸易数据推送给 TradeManager（由 TradeManager 或场景初始化时调用）
    /// </summary>
    public void FlushPendingTradeData()
    {
        if (TradeManager.Instance != null && _pendingTradeRoutes != null)
        {
            TradeManager.Instance.LoadFromSaveData(_pendingTradeRoutes, _pendingTransportOrders);
            _pendingTradeRoutes = null;
            _pendingTransportOrders = null;
        }
    }

    /// <summary>
    /// 将待加载的大地图数据推送给各Manager（由 WorldMapInitializer 或场景初始化时调用）
    /// </summary>
    public void FlushPendingWorldMapData()
    {
        // 恢复大地图格子数据
        if (_pendingWorldMapCells != null && WorldMapManager.Instance != null)
        {
            WorldMapManager.Instance.LoadFromSaveData(_pendingWorldMapCells);
            Debug.Log($"[BaseManager] Restored {_pendingWorldMapCells.Count} world map cells from save.");
            _pendingWorldMapCells = null;
        }

        // 恢复道路网络
        if (_pendingRoads != null)
        {
            var roadNetwork = FindObjectOfType<RoadNetwork>();
            if (roadNetwork != null)
            {
                roadNetwork.LoadFromSaveData(_pendingRoads);
                Debug.Log($"[BaseManager] Restored {_pendingRoads.Count} road segments from save.");
                _pendingRoads = null;
            }
        }

        // 恢复NPC据点和声望
        if (_pendingNPCData != null && NPCManager.Instance != null)
        {
            NPCManager.Instance.LoadFromSaveData(_pendingNPCData);
            Debug.Log($"[BaseManager] Restored NPC data ({_pendingNPCData.outposts.Count} outposts, " +
                $"{_pendingNPCData.reputations.Count} reputations) from save.");
            _pendingNPCData = null;
        }

        // 恢复任务数据
        if (_pendingQuestData != null && QuestManager.Instance != null)
        {
            QuestManager.Instance.LoadFromSaveData(_pendingQuestData);
            Debug.Log($"[BaseManager] Restored quest data ({_pendingQuestData.activeQuests.Count} active, " +
                $"{_pendingQuestData.availableQuests.Count} available) from save.");
            _pendingQuestData = null;
        }

        // 恢复载具运输数据
        if (_pendingVehicleTransportData != null && VehicleTransportManager.Instance != null)
        {
            VehicleTransportManager.Instance.LoadFromSaveData(_pendingVehicleTransportData);
            Debug.Log($"[BaseManager] Restored vehicle transport data ({_pendingVehicleTransportData.activeJobs.Count} active jobs) from save.");
            _pendingVehicleTransportData = null;
        }

        // 恢复玩家背包数据
        if (_pendingInventoryData != null && PlayerInventoryManager.Instance != null)
        {
            PlayerInventoryManager.Instance.LoadFromSaveData(_pendingInventoryData);
            Debug.Log($"[BaseManager] Restored player inventory ({_pendingInventoryData.items.Count} items, " +
                $"{_pendingInventoryData.equipment.Count} equipment) from save.");
            _pendingInventoryData = null;
        }

        // 恢复角色养成数据
        if (_pendingProgressionData != null && CharacterProgressionManager.Instance != null)
        {
            CharacterProgressionManager.Instance.LoadFromSaveData(_pendingProgressionData);
            Debug.Log($"[BaseManager] Restored character progression ({_pendingProgressionData.ownedUnitIds.Count} characters) from save.");
            _pendingProgressionData = null;
        }
    }

    /// <summary>
    /// 检查是否有待加载的大地图数据
    /// </summary>
    public bool HasPendingWorldMapData =>
        _pendingWorldMapCells != null || _pendingRoads != null || _pendingNPCData != null
        || _pendingQuestData != null || _pendingVehicleTransportData != null
        || _pendingInventoryData != null || _pendingProgressionData != null;

    /// <summary>
    /// 重新从存档加载并恢复大地图状态（从基地场景返回时调用）
    /// </summary>
    public void ReloadAndRestoreWorldMap()
    {
        // 重新读取存档文件（获取最新的道路/NPC/格子数据）
        LoadGameData();

        // 恢复大地图数据
        FlushPendingWorldMapData();

        // 恢复贸易数据
        FlushPendingTradeData();

        // 重新加载基地标记
        LoadAllBaseMarkers();

        // 刷新道路可视化（LoadFromSaveData 不触发事件，需要手动刷新）
        var roadVisualizer = FindObjectOfType<RoadVisualizer>();
        if (roadVisualizer != null)
        {
            roadVisualizer.RebuildAllRoads();
            Debug.Log("[BaseManager] Road visualization refreshed.");
        }

        // 刷新NPC据点可视化
        var npcVisualizer = FindObjectOfType<NPCOutpostVisualizer>();
        if (npcVisualizer != null)
        {
            npcVisualizer.RebuildAllOutposts();
            Debug.Log("[BaseManager] NPC outpost visualization refreshed.");
        }

        // 刷新资源区可视化
        var rzVisualizer = FindObjectOfType<ResourceZoneVisualizer>();
        if (rzVisualizer != null)
        {
            rzVisualizer.RebuildAllZones();
            Debug.Log("[BaseManager] Resource zone visualization refreshed.");
        }

        Debug.Log("[BaseManager] World map fully restored from save.");
    }

    public void SaveCurrentGame()
    {
        SaveGameData();
    }

    // ============ Debug ============
#if UNITY_EDITOR
    [ContextMenu("Debug: Print All Bases")]
    private void DebugPrintAllBases()
    {
        Debug.Log($"[BaseManager] Total Bases: {BaseCount}");

        BaseSaveData activeBase = GetActiveBaseSaveData();
        Debug.Log($"  Active Base: {(activeBase != null ? activeBase.baseName : "None")}");

        foreach (var baseData in _baseDataList)
        {
            Debug.Log($"  - {baseData.baseName} (ID: {baseData.baseId})");
            Debug.Log($"    Position: {baseData.worldPosition}");
            Debug.Log($"    Buildings: {baseData.buildings.Count}");
            Debug.Log($"    Money: {baseData.money:F2}");
            Debug.Log($"    Resources: {baseData.resources.Count}");
        }
    }

    [ContextMenu("Debug: Create Test Base")]
    private void DebugCreateTestBase()
    {
        if (!Application.isPlaying) return;
        CreateNewBase(Vector3.zero + Vector3.right * _baseDataList.Count * 50f, $"Test Base {_baseDataList.Count + 1}");
    }

    [ContextMenu("Debug: Save Game")]
    private void DebugSaveGame()
    {
        if (!Application.isPlaying) return;
        SaveGameData();
    }

    [ContextMenu("Debug: Load Game")]
    private void DebugLoadGame()
    {
        if (!Application.isPlaying) return;
        LoadGameData();
    }

    [ContextMenu("Debug: Open Save Folder")]
    private void DebugOpenSaveFolder()
    {
        string dirPath = System.IO.Path.Combine(Application.persistentDataPath, saveDirectory);

        if (!System.IO.Directory.Exists(dirPath))
        {
            System.IO.Directory.CreateDirectory(dirPath);
            Debug.Log($"[BaseManager] Created save folder: {dirPath}");
        }

        // Open folder in file explorer
        #if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            System.Diagnostics.Process.Start("explorer.exe", dirPath.Replace('/', '\\'));
        #elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            System.Diagnostics.Process.Start("open", dirPath);
        #elif UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
            System.Diagnostics.Process.Start("xdg-open", dirPath);
        #endif

        Debug.Log($"[BaseManager] Save folder path: {dirPath}");
    }

    [ContextMenu("Debug: Delete All Save Data")]
    private void DebugDeleteAllSaveData()
    {
        string filePath = System.IO.Path.Combine(Application.persistentDataPath, saveDirectory, saveFileName);

        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
            Debug.Log($"[BaseManager] Deleted save file: {filePath}");
        }
        else
        {
            Debug.LogWarning($"[BaseManager] No save file found at: {filePath}");
        }

        if (Application.isPlaying)
        {
            _baseDataList.Clear();
            _activeBaseId = null;
            Debug.Log("[BaseManager] Cleared all bases from memory. Restart Play mode to start fresh.");
        }
    }
#endif
}

/// <summary>
/// 新基地的初始资源配置
/// </summary>
[System.Serializable]
public class StartingResource
{
    public ResourceDefinition resource;
    public int amount;
}