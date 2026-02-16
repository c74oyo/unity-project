using UnityEngine;

/// <summary>
/// WorldMapInitializer - 大地图场景初始化器
/// 负责在大地图场景加载时初始化所有基地标记和NPC据点
/// </summary>
public class WorldMapInitializer : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("是否在场景启动时自动加载所有基地标记")]
    public bool autoLoadMarkers = true;

    [Tooltip("是否在场景启动时自动初始化NPC据点")]
    public bool autoInitNPCOutposts = true;

    [Tooltip("延迟加载的时间（秒），以确保各Manager已初始化")]
    public float loadDelay = 0.1f;

    private void Start()
    {
        if (autoLoadMarkers || autoInitNPCOutposts)
        {
            // 延迟加载，确保所有 Manager 已经初始化
            Invoke(nameof(InitializeWorldMap), loadDelay);
        }
    }

    /// <summary>
    /// 统一初始化大地图（存档恢复 + 基地标记 + NPC据点）
    /// </summary>
    private void InitializeWorldMap()
    {
        // 优先从存档恢复大地图数据
        RestoreWorldMapFromSave();

        if (autoLoadMarkers)
        {
            LoadBaseMarkers();
        }

        if (autoInitNPCOutposts)
        {
            InitializeNPCOutposts();
        }
    }

    /// <summary>
    /// 从存档恢复大地图数据（道路、NPC据点、格子状态）
    /// </summary>
    private void RestoreWorldMapFromSave()
    {
        if (BaseManager.Instance != null && BaseManager.Instance.HasPendingWorldMapData)
        {
            Debug.Log("[WorldMapInitializer] Restoring world map data from save...");
            BaseManager.Instance.FlushPendingWorldMapData();
        }
    }

    /// <summary>
    /// 加载所有基地标记到大地图上
    /// </summary>
    private void LoadBaseMarkers()
    {
        if (BaseManager.Instance == null)
        {
            Debug.LogWarning("[WorldMapInitializer] BaseManager not found, retrying in 0.5 seconds...");
            Invoke(nameof(LoadBaseMarkers), 0.5f);
            return;
        }

        Debug.Log("[WorldMapInitializer] Loading base markers on world map...");
        BaseManager.Instance.LoadAllBaseMarkers();
    }

    /// <summary>
    /// 初始化NPC据点
    /// </summary>
    private void InitializeNPCOutposts()
    {
        var npcManager = NPCManager.Instance;
        if (npcManager == null)
        {
            npcManager = FindObjectOfType<NPCManager>();
        }

        if (npcManager == null)
        {
            Debug.LogWarning("[WorldMapInitializer] NPCManager not found, skipping NPC outpost initialization.");
            return;
        }

        Debug.Log("[WorldMapInitializer] Initializing NPC outposts...");
        npcManager.InitializeDefaultOutposts();

        // Populate outpost stocks based on current reputation tiers
        if (ReputationMarketSystem.Instance != null)
        {
            ReputationMarketSystem.Instance.RefreshAllOutpostStocks();
            Debug.Log("[WorldMapInitializer] Outpost stocks populated from reputation tiers.");
        }

        // Populate quest boards for all discovered outposts
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.RefreshAllQuestBoards();
            Debug.Log("[WorldMapInitializer] Quest boards populated.");
        }

        // 通知 Visualizer 刷新
        var visualizer = FindObjectOfType<NPCOutpostVisualizer>();
        if (visualizer != null)
        {
            visualizer.RebuildAllOutposts();
            Debug.Log("[WorldMapInitializer] NPCOutpostVisualizer refreshed.");
        }
        else
        {
            Debug.LogWarning("[WorldMapInitializer] NPCOutpostVisualizer not found in scene. " +
                "Add one to see NPC outposts on the map.");
        }
    }

    /// <summary>
    /// 手动触发重新加载标记（可通过按钮或其他方式调用）
    /// </summary>
    public void ReloadBaseMarkers()
    {
        if (BaseManager.Instance != null)
        {
            Debug.Log("[WorldMapInitializer] Manually reloading base markers...");
            BaseManager.Instance.LoadAllBaseMarkers();
        }
        else
        {
            Debug.LogError("[WorldMapInitializer] Cannot reload markers: BaseManager not found");
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Reload Base Markers")]
    private void EditorReloadMarkers()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[WorldMapInitializer] This function only works in Play Mode");
            return;
        }
        ReloadBaseMarkers();
    }

    [ContextMenu("Reinitialize NPC Outposts")]
    private void EditorReinitNPC()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[WorldMapInitializer] This function only works in Play Mode");
            return;
        }
        InitializeNPCOutposts();
    }
#endif
}