using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 全局资源/金钱统计UI - 显示所有基地的资源和金钱汇总
/// 挂在大地图 Canvas 上，提供全局经济概览
/// </summary>
public class GlobalResourcesUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("主面板（可折叠）")]
    public GameObject mainPanel;

    [Tooltip("折叠/展开按钮")]
    public Button toggleButton;

    [Tooltip("折叠时显示的简要信息")]
    public TextMeshProUGUI collapsedSummaryText;

    [Header("Money Display")]
    [Tooltip("总金钱文本")]
    public TextMeshProUGUI totalMoneyText;

    [Tooltip("金钱变化指示（可选，显示收支）")]
    public TextMeshProUGUI moneyChangeText;

    [Header("Resource List")]
    [Tooltip("资源列表项预制体")]
    public GameObject resourceItemPrefab;

    [Tooltip("资源列表的父容器")]
    public Transform resourceListParent;

    [Header("Base Summary")]
    [Tooltip("基地数量文本")]
    public TextMeshProUGUI baseCountText;

    [Tooltip("基地列表项预制体（可选，显示每个基地的简要信息）")]
    public GameObject baseItemPrefab;

    [Tooltip("基地列表的父容器")]
    public Transform baseListParent;

    [Header("Road Summary")]
    [Tooltip("道路统计文本（可选）")]
    public TextMeshProUGUI roadSummaryText;

    [Header("Trade Summary")]
    [Tooltip("贸易统计文本（可选）")]
    public TextMeshProUGUI tradeSummaryText;

    [Header("Settings")]
    [Tooltip("刷新间隔（秒）")]
    public float refreshInterval = 1f;

    [Tooltip("是否默认折叠")]
    public bool startCollapsed = false;

    [Tooltip("金钱格式化（例如 '$#,##0' 或 '{0:N0}'）")]
    public string moneyFormat = "${0:N0}";

    // Runtime
    private float _refreshTimer;
    private bool _isExpanded = true;
    private List<GameObject> _spawnedResourceItems = new();
    private List<GameObject> _spawnedBaseItems = new();

    // 缓存的数据（用于检测变化）
    private float _lastTotalMoney;
    private Dictionary<string, float> _lastResourceTotals = new();

    // ============ Lifecycle ============

    private void Start()
    {
        _isExpanded = !startCollapsed;
        UpdatePanelState();

        if (toggleButton != null)
        {
            toggleButton.onClick.AddListener(TogglePanel);
        }

        // 初始刷新
        Refresh();
    }

    private void Update()
    {
        _refreshTimer += Time.deltaTime;
        if (_refreshTimer >= refreshInterval)
        {
            _refreshTimer = 0f;
            Refresh();
        }
    }

    // ============ Panel Toggle ============

    public void TogglePanel()
    {
        _isExpanded = !_isExpanded;
        UpdatePanelState();
    }

    private void UpdatePanelState()
    {
        if (mainPanel != null)
        {
            mainPanel.SetActive(_isExpanded);
        }

        if (collapsedSummaryText != null)
        {
            collapsedSummaryText.gameObject.SetActive(!_isExpanded);
        }
    }

    // ============ Refresh ============

    /// <summary>
    /// 刷新所有统计数据
    /// </summary>
    public void Refresh()
    {
        if (BaseManager.Instance == null) return;

        var allBases = BaseManager.Instance.AllBaseSaveData;

        // 计算汇总数据
        float totalMoney = 0f;
        Dictionary<string, float> resourceTotals = new();

        foreach (var baseData in allBases)
        {
            totalMoney += baseData.money;

            if (baseData.resources != null)
            {
                foreach (var res in baseData.resources)
                {
                    if (!resourceTotals.ContainsKey(res.resourceName))
                        resourceTotals[res.resourceName] = 0f;
                    resourceTotals[res.resourceName] += res.amount;
                }
            }
        }

        // 更新金钱显示
        UpdateMoneyDisplay(totalMoney);

        // 更新资源列表
        UpdateResourceList(resourceTotals);

        // 更新基地信息
        UpdateBaseInfo(allBases);

        // 更新道路统计
        UpdateRoadSummary();

        // 更新贸易统计
        UpdateTradeSummary();

        // 更新折叠时的简要信息
        UpdateCollapsedSummary(totalMoney, allBases.Count);

        // 保存缓存
        _lastTotalMoney = totalMoney;
        _lastResourceTotals = new Dictionary<string, float>(resourceTotals);
    }

    // ============ Money Display ============

    private void UpdateMoneyDisplay(float totalMoney)
    {
        if (totalMoneyText != null)
        {
            totalMoneyText.text = string.Format(moneyFormat, totalMoney);
        }

        // 金钱变化指示
        if (moneyChangeText != null)
        {
            float change = totalMoney - _lastTotalMoney;
            if (Mathf.Abs(change) > 0.01f && _lastTotalMoney > 0f)
            {
                string sign = change > 0 ? "+" : "";
                Color color = change > 0 ? Color.green : Color.red;
                moneyChangeText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{sign}{change:N0}</color>";
                moneyChangeText.gameObject.SetActive(true);
            }
            else
            {
                moneyChangeText.gameObject.SetActive(false);
            }
        }
    }

    // ============ Resource List ============

    private void UpdateResourceList(Dictionary<string, float> resourceTotals)
    {
        // 清除旧项
        foreach (var item in _spawnedResourceItems)
        {
            if (item != null) Destroy(item);
        }
        _spawnedResourceItems.Clear();

        if (resourceItemPrefab == null || resourceListParent == null) return;

        // 按数量排序
        var sortedResources = new List<KeyValuePair<string, float>>(resourceTotals);
        sortedResources.Sort((a, b) => b.Value.CompareTo(a.Value));

        foreach (var kvp in sortedResources)
        {
            if (kvp.Value <= 0f) continue; // 跳过数量为0的资源

            var item = Instantiate(resourceItemPrefab, resourceListParent);
            _spawnedResourceItems.Add(item);

            // 设置文本
            var text = item.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                string displayName = GetResourceDisplayName(kvp.Key);
                text.text = $"{displayName}: {kvp.Value:N0}";

                // 检测变化
                if (_lastResourceTotals.TryGetValue(kvp.Key, out float lastAmount))
                {
                    float change = kvp.Value - lastAmount;
                    if (Mathf.Abs(change) > 0.01f)
                    {
                        string sign = change > 0 ? "+" : "";
                        Color color = change > 0 ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.5f, 0.5f);
                        text.text += $" <color=#{ColorUtility.ToHtmlStringRGB(color)}>({sign}{change:N0})</color>";
                    }
                }
            }

            // 设置图标（如果有）
            var icon = item.transform.Find("Icon")?.GetComponent<Image>();
            if (icon != null)
            {
                var resourceDef = GetResourceDefinition(kvp.Key);
                if (resourceDef != null && resourceDef.icon != null)
                {
                    icon.sprite = resourceDef.icon;
                    icon.gameObject.SetActive(true);
                }
                else
                {
                    icon.gameObject.SetActive(false);
                }
            }
        }
    }

    // ============ Base Info ============

    private void UpdateBaseInfo(List<BaseSaveData> allBases)
    {
        // 基地数量
        if (baseCountText != null)
        {
            baseCountText.text = $"基地: {allBases.Count}";
        }

        // 基地列表（可选）
        if (baseItemPrefab != null && baseListParent != null)
        {
            // 清除旧项
            foreach (var item in _spawnedBaseItems)
            {
                if (item != null) Destroy(item);
            }
            _spawnedBaseItems.Clear();

            foreach (var baseData in allBases)
            {
                var item = Instantiate(baseItemPrefab, baseListParent);
                _spawnedBaseItems.Add(item);

                var text = item.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null)
                {
                    bool isActive = baseData.baseId == BaseManager.Instance.ActiveBaseId;
                    string activeMarker = isActive ? "<color=yellow>*</color>" : "";
                    text.text = $"{activeMarker}{baseData.baseName}: {string.Format(moneyFormat, baseData.money)}";
                }
            }
        }
    }

    // ============ Road Summary ============

    private void UpdateRoadSummary()
    {
        if (roadSummaryText == null) return;

        var roadNetwork = RoadNetwork.Instance;
        if (roadNetwork == null)
        {
            roadSummaryText.text = "";
            return;
        }

        var allRoads = roadNetwork.GetAllRoads();
        int totalRoads = allRoads.Count;
        int damagedCount = 0;
        int brokenCount = 0;

        foreach (var segment in allRoads)
        {
            if (segment == null) continue;

            var level = segment.GetDamageLevel();
            if (level == RoadSegment.DamageLevel.Broken)
                brokenCount++;
            else if (level != RoadSegment.DamageLevel.Normal)
                damagedCount++;
        }

        StringBuilder sb = new StringBuilder();
        sb.Append($"道路: {totalRoads} 段");

        if (brokenCount > 0)
        {
            sb.Append($" | <color=red>断裂: {brokenCount}</color>");
        }
        if (damagedCount > 0)
        {
            sb.Append($" | <color=yellow>损坏: {damagedCount}</color>");
        }

        roadSummaryText.text = sb.ToString();
    }

    // ============ Trade Summary ============

    private void UpdateTradeSummary()
    {
        if (tradeSummaryText == null) return;

        if (TradeManager.Instance == null)
        {
            tradeSummaryText.text = "";
            return;
        }

        var routes = TradeManager.Instance.GetAllTradeRoutes();
        var orders = TradeManager.Instance.GetActiveOrders();

        int activeRoutes = 0;
        int blockedRoutes = 0;

        foreach (var route in routes)
        {
            if (route.isActive)
            {
                activeRoutes++;
                if (!route.isValid)
                    blockedRoutes++;
            }
        }

        StringBuilder sb = new StringBuilder();
        sb.Append($"路线: {activeRoutes}");

        if (blockedRoutes > 0)
        {
            sb.Append($" (<color=red>{blockedRoutes} 中断</color>)");
        }

        sb.Append($" | 运输中: {orders.Count}");

        tradeSummaryText.text = sb.ToString();
    }

    // ============ Collapsed Summary ============

    private void UpdateCollapsedSummary(float totalMoney, int baseCount)
    {
        if (collapsedSummaryText == null) return;

        collapsedSummaryText.text = $"{string.Format(moneyFormat, totalMoney)} | {baseCount} 基地";
    }

    // ============ Helpers ============

    private string GetResourceDisplayName(string resourceName)
    {
        // 直接返回资源名称（如有ResourceRegistry可以在此扩展）
        return resourceName;
    }

    private ResourceDefinition GetResourceDefinition(string resourceName)
    {
        // 如果有 ResourceRegistry，可以在此查找
        // 目前直接返回 null
        return null;
    }

    // ============ Cleanup ============

    private void OnDisable()
    {
        foreach (var item in _spawnedResourceItems)
        {
            if (item != null) Destroy(item);
        }
        _spawnedResourceItems.Clear();

        foreach (var item in _spawnedBaseItems)
        {
            if (item != null) Destroy(item);
        }
        _spawnedBaseItems.Clear();
    }
}
