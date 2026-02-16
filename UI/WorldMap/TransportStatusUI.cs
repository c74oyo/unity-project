using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;

/// <summary>
/// 运输状态面板 - 在大地图上显示所有活跃的运输订单
/// 挂在大地图 Canvas 上，自动刷新显示进度
/// </summary>
public class TransportStatusUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("订单列表项预制体（需要挂 TransportOrderItemUI）")]
    public GameObject orderItemPrefab;

    [Tooltip("订单列表的父容器（VerticalLayoutGroup）")]
    public Transform orderListParent;

    [Tooltip("标题文本（显示 '运输中 (3)' 等）")]
    public TextMeshProUGUI titleText;

    [Tooltip("摘要文本（显示 '最快 2:30 后到达' 等）")]
    public TextMeshProUGUI summaryText;

    [Tooltip("无运输时显示的提示")]
    public GameObject emptyHint;

    [Header("Road Status Warning")]
    [Tooltip("道路损坏警告面板")]
    public GameObject roadWarningPanel;
    [Tooltip("道路损坏警告文本")]
    public TextMeshProUGUI roadWarningText;

    [Header("Settings")]
    [Tooltip("刷新间隔（秒）")]
    public float refreshInterval = 0.5f;

    // Runtime
    private float _refreshTimer;
    private List<GameObject> _spawnedItems = new List<GameObject>();

    private void Update()
    {
        if (TradeManager.Instance == null) return;

        _refreshTimer += Time.deltaTime;
        if (_refreshTimer >= refreshInterval)
        {
            _refreshTimer = 0f;
            Refresh();
        }
    }

    /// <summary>
    /// 刷新运输状态显示
    /// </summary>
    private void Refresh()
    {
        // 清除旧的 UI 项
        ClearItems();

        var orders = TradeManager.Instance.GetActiveOrders();

        // 更新标题
        if (titleText != null)
        {
            titleText.text = orders.Count > 0
                ? $"运输中 ({orders.Count})"
                : "运输中";
        }

        // 更新摘要
        UpdateSummary(orders);

        // 检查道路状态警告
        UpdateRoadWarning();

        // 空提示
        if (emptyHint != null)
        {
            emptyHint.SetActive(orders.Count == 0);
        }

        // 生成订单项
        if (orderItemPrefab == null || orderListParent == null) return;

        foreach (var order in orders)
        {
            if (!order.IsActive) continue;

            var item = Instantiate(orderItemPrefab, orderListParent);
            _spawnedItems.Add(item);

            var itemUI = item.GetComponent<TransportOrderItemUI>();
            if (itemUI != null)
            {
                // 获取路线
                var route = TradeManager.Instance.GetTradeRoute(order.routeId);
                itemUI.Setup(order, route);
            }
            else
            {
                // 回退：使用简单文本
                var text = item.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null)
                {
                    text.text = $"{order.orderId.Substring(0, 8)} | " +
                                $"{order.Progress:P0} | " +
                                $"ETA {order.FormattedRemainingTime}";
                }
            }
        }
    }

    /// <summary>
    /// 更新摘要信息
    /// </summary>
    private void UpdateSummary(List<WorldMapTransportOrder> orders)
    {
        if (summaryText == null) return;

        if (orders.Count == 0)
        {
            summaryText.text = "";
            return;
        }

        // 找到最快到达的订单
        float minTime = float.MaxValue;
        int totalCargo = 0;

        foreach (var order in orders)
        {
            if (!order.IsActive) continue;
            if (order.RemainingTime < minTime)
                minTime = order.RemainingTime;

            foreach (var cargo in order.cargoItems)
                totalCargo += cargo.amount;
        }

        if (minTime < float.MaxValue)
        {
            string timeStr = FormatTime(minTime);
            summaryText.text = $"运输 {totalCargo} 单位货物，最快 {timeStr} 后到达";
        }
        else
        {
            summaryText.text = $"运输 {totalCargo} 单位货物";
        }
    }

    /// <summary>
    /// 更新道路损坏警告
    /// </summary>
    private void UpdateRoadWarning()
    {
        if (roadWarningPanel == null) return;

        // 检查是否有路线因道路损坏而无效
        var routes = TradeManager.Instance.GetAllTradeRoutes();
        int blockedCount = 0;

        foreach (var route in routes)
        {
            if (route.isActive && !route.isValid)
            {
                // 检查是否因道路损坏
                if (RoadNetwork.Instance != null && route.roadPath != null)
                {
                    if (!RoadNetwork.Instance.IsPathPassable(route.roadPath))
                    {
                        blockedCount++;
                    }
                }
            }
        }

        if (blockedCount > 0)
        {
            roadWarningPanel.SetActive(true);
            if (roadWarningText != null)
            {
                roadWarningText.text = $"<color=red>警告: {blockedCount} 条路线因道路损坏中断!</color>";
            }
        }
        else
        {
            roadWarningPanel.SetActive(false);
        }
    }

    /// <summary>
    /// 格式化时间为 M:SS 格式
    /// </summary>
    private string FormatTime(float seconds)
    {
        int min = Mathf.FloorToInt(seconds / 60f);
        int sec = Mathf.FloorToInt(seconds % 60f);
        return $"{min}:{sec:D2}";
    }

    private void ClearItems()
    {
        foreach (var item in _spawnedItems)
        {
            if (item != null)
                Destroy(item);
        }
        _spawnedItems.Clear();
    }

    private void OnDisable()
    {
        ClearItems();
    }
}
