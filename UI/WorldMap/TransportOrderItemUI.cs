using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 单个运输订单的 UI 项
/// 显示路线名、进度条、剩余时间、货物概要
/// </summary>
public class TransportOrderItemUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("路线名称")]
    public TextMeshProUGUI routeNameText;

    [Tooltip("进度条")]
    public Slider progressSlider;

    [Tooltip("剩余时间文本")]
    public TextMeshProUGUI timeText;

    [Tooltip("货物概要文本")]
    public TextMeshProUGUI cargoText;

    [Tooltip("进度百分比文本（可选）")]
    public TextMeshProUGUI progressText;

    [Tooltip("进度条填充颜色图片（可选，用于根据进度变色）")]
    public Image progressFillImage;

    [Header("Road Status")]
    [Tooltip("道路状态图标（用于显示道路损坏警告）")]
    public GameObject roadStatusIcon;

    [Tooltip("道路状态文本")]
    public TextMeshProUGUI roadStatusText;

    /// <summary>
    /// 设置订单数据（兼容旧调用方式）
    /// </summary>
    public void Setup(WorldMapTransportOrder order, string routeName)
    {
        Setup(order, null, routeName);
    }

    /// <summary>
    /// 设置订单数据（带路线信息）
    /// </summary>
    public void Setup(WorldMapTransportOrder order, TradeRoute route)
    {
        string routeName = route != null ? route.displayName : "Unknown Route";
        Setup(order, route, routeName);
    }

    /// <summary>
    /// 设置订单数据（完整版）
    /// </summary>
    private void Setup(WorldMapTransportOrder order, TradeRoute route, string routeName)
    {
        if (order == null) return;

        // 路线名
        if (routeNameText != null)
            routeNameText.text = routeName;

        // 进度条
        if (progressSlider != null)
        {
            progressSlider.minValue = 0f;
            progressSlider.maxValue = 1f;
            progressSlider.value = order.Progress;
        }

        // 进度百分比
        if (progressText != null)
            progressText.text = $"{order.Progress:P0}";

        // 进度条颜色（接近完成时变绿）
        if (progressFillImage != null)
        {
            progressFillImage.color = Color.Lerp(
                new Color(0.2f, 0.6f, 1f), // 蓝色（起始）
                new Color(0.2f, 0.9f, 0.3f), // 绿色（完成）
                order.Progress
            );
        }

        // 剩余时间
        if (timeText != null)
            timeText.text = order.FormattedRemainingTime;

        // 货物概要
        if (cargoText != null)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var cargo in order.cargoItems)
            {
                string arrow = cargo.direction == TradeDirection.Export ? "\u2192" : "\u2190";
                if (sb.Length > 0) sb.Append("  ");
                sb.Append($"{arrow} {cargo.amount}x {cargo.resourceId}");
            }
            cargoText.text = sb.ToString();
        }

        // 道路状态警告
        UpdateRoadStatus(route);
    }

    /// <summary>
    /// 更新道路状态显示
    /// </summary>
    private void UpdateRoadStatus(TradeRoute route)
    {
        if (roadStatusIcon == null && roadStatusText == null) return;

        bool hasDamage = false;
        bool hasSevereDamage = false;

        if (route != null && route.roadPath != null && RoadNetwork.Instance != null)
        {
            foreach (var cell in route.roadPath)
            {
                var segment = RoadNetwork.Instance.GetRoadAt(cell);
                if (segment != null)
                {
                    var damageLevel = segment.GetDamageLevel();
                    if (damageLevel != RoadSegment.DamageLevel.Normal)
                    {
                        hasDamage = true;
                        if (damageLevel == RoadSegment.DamageLevel.Severe)
                        {
                            hasSevereDamage = true;
                            break;
                        }
                    }
                }
            }
        }

        if (roadStatusIcon != null)
        {
            roadStatusIcon.SetActive(hasDamage);
        }

        if (roadStatusText != null)
        {
            if (hasSevereDamage)
            {
                roadStatusText.text = "<color=orange>道路严重损坏，减速+货损</color>";
                roadStatusText.gameObject.SetActive(true);
            }
            else if (hasDamage)
            {
                roadStatusText.text = "<color=yellow>道路受损，运输减速</color>";
                roadStatusText.gameObject.SetActive(true);
            }
            else
            {
                roadStatusText.gameObject.SetActive(false);
            }
        }
    }
}
