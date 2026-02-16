using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 基地地图UI - 管理侧边栏基地列表
/// 详情显示由 BaseDetailPopup 负责
/// </summary>
public class BaseMapUI : MonoBehaviour
{
    public static BaseMapUI Instance { get; private set; }

    [Header("UI Panels")]
    public GameObject sidebarPanel;           // 侧边栏面板
    public RectTransform baseListContainer;   // 基地列表容器

    [Header("Prefabs")]
    public GameObject baseListItemPrefab;     // 基地列表项预制体

    [Header("Settings")]
    public bool showSidebarOnStart = true;
    public bool autoRefreshInterval = true;
    public float refreshInterval = 2f;  // 自动刷新间隔

    // Runtime
    private string _selectedBaseId;
    private List<GameObject> _listItems = new();
    private float _refreshTimer = 0f;

    // ============ Lifecycle ============

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 初始化显示
        if (sidebarPanel != null)
            sidebarPanel.SetActive(showSidebarOnStart);
    }

    private void Start()
    {
        RefreshBaseList();

        // 订阅 BaseManager 事件，当创建或删除基地时自动刷新列表
        if (BaseManager.Instance != null)
        {
            BaseManager.Instance.OnBaseCreated += OnBaseCreatedHandler;
            BaseManager.Instance.OnBaseDestroyed += OnBaseDestroyedHandler;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        // 取消订阅事件
        if (BaseManager.Instance != null)
        {
            BaseManager.Instance.OnBaseCreated -= OnBaseCreatedHandler;
            BaseManager.Instance.OnBaseDestroyed -= OnBaseDestroyedHandler;
        }
    }

    private void OnBaseCreatedHandler(string baseId)
    {
        RefreshBaseList();
    }

    private void OnBaseDestroyedHandler(string baseId)
    {
        RefreshBaseList();
    }

    private void Update()
    {
        if (autoRefreshInterval)
        {
            _refreshTimer += Time.deltaTime;
            if (_refreshTimer >= refreshInterval)
            {
                _refreshTimer = 0f;
                RefreshBaseList();
            }
        }
    }

    // ============ Public Methods ============

    /// <summary>
    /// 刷新基地列表
    /// </summary>
    public void RefreshBaseList()
    {
        if (baseListContainer == null) return;

        // 清空现有列表
        foreach (var item in _listItems)
        {
            if (item != null) Destroy(item);
        }
        _listItems.Clear();

        if (BaseManager.Instance == null) return;

        // 获取所有基地
        var allBases = BaseManager.Instance.AllBaseSaveData;

        foreach (var baseSave in allBases)
        {
            CreateBaseListItem(baseSave);
        }
    }

    /// <summary>
    /// 选中基地（移动摄像机并打开详情弹窗）
    /// </summary>
    public void SelectBase(string baseId)
    {
        if (string.IsNullOrEmpty(baseId)) return;

        _selectedBaseId = baseId;

        // 更新列表项选中状态
        UpdateListItemSelection();

        // 移动摄像机到基地位置
        MoveCameraToBase(baseId);

        // 打开详情弹窗
        if (BaseDetailPopup.Instance != null)
        {
            BaseDetailPopup.Instance.Show(baseId);
        }
    }

    /// <summary>
    /// 取消选中
    /// </summary>
    public void DeselectBase()
    {
        _selectedBaseId = null;
        UpdateListItemSelection();

        // 关闭详情弹窗
        if (BaseDetailPopup.Instance != null)
        {
            BaseDetailPopup.Instance.Hide();
        }
    }

    /// <summary>
    /// 切换侧边栏显示
    /// </summary>
    public void ToggleSidebar()
    {
        if (sidebarPanel != null)
            sidebarPanel.SetActive(!sidebarPanel.activeSelf);
    }

    /// <summary>
    /// 显示侧边栏
    /// </summary>
    public void ShowSidebar()
    {
        if (sidebarPanel != null)
            sidebarPanel.SetActive(true);
    }

    /// <summary>
    /// 隐藏侧边栏
    /// </summary>
    public void HideSidebar()
    {
        if (sidebarPanel != null)
            sidebarPanel.SetActive(false);
    }

    // ============ Private Methods ============

    private void CreateBaseListItem(BaseSaveData baseSave)
    {
        if (baseListItemPrefab == null || baseListContainer == null) return;

        var itemGO = Instantiate(baseListItemPrefab, baseListContainer);
        _listItems.Add(itemGO);

        // 设置基地信息
        var itemUI = itemGO.GetComponent<BaseListItemUI>();
        if (itemUI != null)
        {
            itemUI.Setup(baseSave.baseId, baseSave.baseName, baseSave.worldPosition);
            itemUI.OnItemClicked += OnBaseListItemClicked;
        }
    }

    private void OnBaseListItemClicked(string baseId)
    {
        SelectBase(baseId);
    }

    private void UpdateListItemSelection()
    {
        foreach (var itemGO in _listItems)
        {
            if (itemGO == null) continue;

            var itemUI = itemGO.GetComponent<BaseListItemUI>();
            if (itemUI != null)
            {
                itemUI.SetSelected(itemUI.BaseId == _selectedBaseId);
            }
        }
    }

    private void MoveCameraToBase(string baseId)
    {
        if (BaseManager.Instance == null) return;

        var saveData = BaseManager.Instance.GetBaseSaveData(baseId);
        if (saveData == null) return;

        // 找到摄像机控制器
        var cameraController = FindObjectOfType<BuildCameraController>();
        if (cameraController != null)
        {
            cameraController.MoveTo(saveData.worldPosition);
        }
    }

    // ============ Debug ============
#if UNITY_EDITOR
    [ContextMenu("Debug: Refresh Base List")]
    private void DebugRefreshBaseList()
    {
        RefreshBaseList();
    }
#endif
}