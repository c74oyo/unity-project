using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 基地详情弹出面板 - 点击基地后弹出，显示建筑和资源流动信息
/// </summary>
public class BaseDetailPopup : MonoBehaviour
{
    public static BaseDetailPopup Instance { get; private set; }

    [Header("Panel")]
    public GameObject popupPanel;
    public Button closeButton;
    public Button enterBaseButton;

    [Header("Header Info")]
    public TextMeshProUGUI baseNameText;
    public TextMeshProUGUI positionText;
    public TextMeshProUGUI moneyText;

    [Header("Resource Zone Display")]
    public GameObject resourceZonePanel;  // 资源区面板（可隐藏）
    public Image resourceZoneIcon;
    public TextMeshProUGUI resourceZoneNameText;
    public TextMeshProUGUI efficiencyBonusText;

    [Header("Resource Section")]
    public Transform resourceListParent;
    public GameObject resourceItemPrefab;

    [Header("Building Section")]
    public Transform buildingListParent;
    public GameObject buildingItemPrefab;

    [Header("Flow Section")]
    public Transform flowListParent;
    public GameObject flowItemPrefab;

    [Header("Refresh")]
    [Tooltip("弹窗打开时的自动刷新间隔（秒）")]
    public float refreshInterval = 1f;

    // Runtime
    private string _currentBaseId;
    private List<GameObject> _spawnedItems = new List<GameObject>();
    private float _refreshTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 设置按钮事件
        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);

        if (enterBaseButton != null)
            enterBaseButton.onClick.AddListener(OnEnterBaseClicked);

        // 初始隐藏
        if (popupPanel != null)
            popupPanel.SetActive(false);
    }

    private void Update()
    {
        // 弹窗打开时定时刷新数据
        if (popupPanel != null && popupPanel.activeSelf && !string.IsNullOrEmpty(_currentBaseId))
        {
            _refreshTimer += Time.deltaTime;
            if (_refreshTimer >= refreshInterval)
            {
                _refreshTimer = 0f;
                RefreshCurrentBase();
            }
        }
    }

    /// <summary>
    /// 刷新当前显示的基地数据（不重新打开面板）
    /// </summary>
    private void RefreshCurrentBase()
    {
        if (string.IsNullOrEmpty(_currentBaseId)) return;

        BaseInfoData baseInfo = GetBaseInfo(_currentBaseId);
        if (baseInfo == null) return;

        // 只刷新变化频繁的部分（Header 中的资源数量、资源列表）
        PopulateHeader(baseInfo);

        // 清空并重建资源列表
        ClearAllItems();
        PopulateResources(baseInfo.resources);
        PopulateBuildings(baseInfo.buildings);
        PopulateFlows(baseInfo.resourceFlows);
    }

    /// <summary>
    /// 显示基地详情
    /// </summary>
    public void Show(string baseId)
    {
        if (string.IsNullOrEmpty(baseId)) return;

        _currentBaseId = baseId;

        // 获取基地信息
        BaseInfoData baseInfo = GetBaseInfo(baseId);
        if (baseInfo == null)
        {
            Debug.LogWarning($"[BaseDetailPopup] Cannot find base info for: {baseId}");
            return;
        }

        // 清空旧内容
        ClearAllItems();

        // 填充数据
        PopulateHeader(baseInfo);
        PopulateResources(baseInfo.resources);
        PopulateBuildings(baseInfo.buildings);
        PopulateFlows(baseInfo.resourceFlows);

        // 显示面板
        if (popupPanel != null)
            popupPanel.SetActive(true);
    }

    /// <summary>
    /// 隐藏面板
    /// </summary>
    public void Hide()
    {
        if (popupPanel != null)
            popupPanel.SetActive(false);

        _currentBaseId = null;
    }

    /// <summary>
    /// 切换显示/隐藏
    /// </summary>
    public void Toggle(string baseId)
    {
        if (popupPanel != null && popupPanel.activeSelf && _currentBaseId == baseId)
        {
            Hide();
        }
        else
        {
            Show(baseId);
        }
    }

    // ============ Private Methods ============

    private void ClearAllItems()
    {
        foreach (var item in _spawnedItems)
        {
            if (item != null)
                Destroy(item);
        }
        _spawnedItems.Clear();
    }

    private void PopulateHeader(BaseInfoData baseInfo)
    {
        if (baseNameText != null)
            baseNameText.text = baseInfo.baseName;

        if (positionText != null)
            positionText.text = $"Position: ({baseInfo.worldPosition.x:F0}, {baseInfo.worldPosition.z:F0})";

        if (moneyText != null)
            moneyText.text = $"${baseInfo.money:F0}";

        // 显示资源区信息
        if (resourceZonePanel != null)
        {
            if (baseInfo.zoneInfo != null)
            {
                resourceZonePanel.SetActive(true);

                if (resourceZoneNameText != null)
                    resourceZoneNameText.text = $"资源区: {baseInfo.zoneInfo.displayName}";

                if (efficiencyBonusText != null)
                {
                    float bonus = baseInfo.zoneInfo.efficiencyBonus;
                    efficiencyBonusText.text = $"生产效率: x{bonus:0.##}";
                    efficiencyBonusText.color = bonus > 1f ? Color.green : Color.white;
                }

                if (resourceZoneIcon != null && baseInfo.zoneInfo.icon != null)
                    resourceZoneIcon.sprite = baseInfo.zoneInfo.icon;
            }
            else
            {
                resourceZonePanel.SetActive(false);
            }
        }
    }

    private void PopulateResources(List<BaseInfoData.ResourceStockInfo> resources)
    {
        if (resourceListParent == null || resourceItemPrefab == null) return;

        // 获取当前基地信息（用于获取资源区加成）
        BaseInfoData baseInfo = GetBaseInfo(_currentBaseId);

        foreach (var res in resources)
        {
            CreateResourceItem(res.resource, res.current, res.capacity, baseInfo);
        }
    }

    private void CreateResourceItem(ResourceDefinition resource, float current, int capacity, BaseInfoData baseInfo)
    {
        if (resource == null) return;

        var item = Instantiate(resourceItemPrefab, resourceListParent);
        _spawnedItems.Add(item);

        // 使用ResourceStockItemUI的Setup方法来正确显示所有UI元素
        var stockUI = item.GetComponent<ResourceStockItemUI>();
        if (stockUI != null)
        {
            // 使用构造函数创建ResourceStockInfo（构造函数会自动计算percentage）
            var stockInfo = new BaseInfoData.ResourceStockInfo(resource, current, capacity);
            stockUI.Setup(stockInfo);

            // 新增：如果基地在资源区，并且该资源与资源区匹配，则将资源名称染成绿色
            if (baseInfo != null && baseInfo.zoneInfo != null && stockUI.resourceNameText != null)
            {
                var zoneType = FindResourceZoneType(baseInfo.resourceZoneTypeId);
                if (zoneType != null && zoneType.normalResource == resource)
                {
                    stockUI.resourceNameText.color = Color.green;
                }
            }
        }
        else
        {
            // 回退方案：使用旧的方式（如果prefab没有ResourceStockItemUI组件）
            Debug.LogWarning("[BaseDetailPopup] ResourceStockItemUI component not found on prefab!");

            var text = item.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                float percentage = capacity > 0 ? current / capacity * 100f : 0f;

                // 根据数量设置颜色
                if (current <= 0)
                    text.color = Color.red;
                else if (percentage < 20f)
                    text.color = Color.yellow;
                else
                    text.color = Color.white;

                text.text = $"{resource.displayName}: {current:F0} / {capacity}";
            }

            var slider = item.GetComponentInChildren<Slider>();
            if (slider != null)
            {
                slider.value = capacity > 0 ? Mathf.Clamp01(current / capacity) : 0f;
            }
        }
    }

    private void PopulateBuildings(List<BaseInfoData.BuildingInfo> buildings)
    {
        if (buildingListParent == null || buildingItemPrefab == null) return;

        foreach (var building in buildings)
        {
            var item = Instantiate(buildingItemPrefab, buildingListParent);
            _spawnedItems.Add(item);

            var text = item.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = $"{building.GetTypeIcon()} {building.GetDisplayText()}";
            }
        }
    }

    private void PopulateFlows(List<BaseInfoData.ResourceFlowInfo> flows)
    {
        if (flowListParent == null || flowItemPrefab == null) return;

        foreach (var flow in flows)
        {
            var item = Instantiate(flowItemPrefab, flowListParent);
            _spawnedItems.Add(item);

            // 尝试使用 ResourceFlowItemUI
            var flowUI = item.GetComponent<ResourceFlowItemUI>();
            if (flowUI != null)
            {
                flowUI.Setup(flow);
            }
            else
            {
                // 回退到简单文本
                var text = item.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null)
                {
                    string netSign = flow.net >= 0 ? "+" : "";
                    Color netColor = flow.net > 0 ? Color.green : (flow.net < 0 ? Color.red : Color.gray);
                    text.text = $"{flow.resource.displayName}: {netSign}{flow.net:F1}/min";
                    text.color = netColor;
                }
            }
        }
    }

    private BaseInfoData GetBaseInfo(string baseId)
    {
        if (BaseManager.Instance == null) return null;

        var saveData = BaseManager.Instance.GetBaseSaveData(baseId);
        if (saveData != null)
        {
            return BaseInfoData.FromBaseSaveData(saveData);
        }

        return null;
    }

    private ResourceZoneType FindResourceZoneType(string zoneId)
    {
        if (string.IsNullOrEmpty(zoneId)) return null;

        if (BaseManager.Instance != null)
            return BaseManager.Instance.FindResourceZoneType(zoneId);

        // 备选：直接从Resources查找
        var allZoneTypes = Resources.FindObjectsOfTypeAll<ResourceZoneType>();
        foreach (var zt in allZoneTypes)
        {
            if (zt.zoneId == zoneId)
                return zt;
        }
        return null;
    }

    private void OnEnterBaseClicked()
    {
        if (string.IsNullOrEmpty(_currentBaseId)) return;

        // 先保存 baseId，因为 Hide() 会清空 _currentBaseId
        string baseIdToEnter = _currentBaseId;

        Hide();

        // 优先使用 SceneTransitionManager（与 WorldMapTester 保持一致）
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.EnterBase(baseIdToEnter);
        }
        else if (BaseSceneManager.Instance != null)
        {
            BaseSceneManager.Instance.EnterBase(baseIdToEnter);
        }
        else if (BaseManager.Instance != null)
        {
            BaseManager.Instance.SetActiveBase(baseIdToEnter);
            Debug.LogWarning("[BaseDetailPopup] No scene manager found, only set active base");
        }
    }

    // ============ Static Helper ============

    /// <summary>
    /// 快速显示基地详情（静态方法）
    /// </summary>
    public static void ShowBaseDetail(string baseId)
    {
        if (Instance != null)
        {
            Instance.Show(baseId);
        }
        else
        {
            Debug.LogWarning("[BaseDetailPopup] Instance not found!");
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
