using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 资源流动显示项 - 显示资源的消耗、生产和净值
/// 格式: "资源名: -消耗 + 生产 = 净值 /min"
/// </summary>
public class ResourceFlowItemUI : MonoBehaviour
{
    [Header("UI Elements")]
    public Image resourceIcon;
    public TextMeshProUGUI resourceNameText;
    public TextMeshProUGUI consumptionText;
    public TextMeshProUGUI productionText;
    public TextMeshProUGUI netText;

    [Header("Colors")]
    public Color positiveColor = Color.green;
    public Color negativeColor = Color.red;
    public Color neutralColor = Color.gray;

    [Header("Formatting")]
    public bool showDetailedView = true;  // true: 显示详细信息，false: 只显示净值

    public void Setup(BaseInfoData.ResourceFlowInfo flowInfo)
    {
        if (flowInfo == null)
        {
            Debug.LogWarning("[ResourceFlowItemUI] flowInfo is null!");
            return;
        }
        if (flowInfo.resource == null)
        {
            Debug.LogWarning("[ResourceFlowItemUI] flowInfo.resource is null!");
            return;
        }

        Debug.Log($"[ResourceFlowItemUI] Setup: {flowInfo.resource.displayName}, consume={flowInfo.consumption}, produce={flowInfo.production}, net={flowInfo.net}");

        // 设置资源名称
        if (resourceNameText != null)
            resourceNameText.text = flowInfo.resource.displayName;

        if (showDetailedView)
        {
            // 详细视图：显示消耗、生产、净值
            if (consumptionText != null)
            {
                consumptionText.text = $"-{flowInfo.consumption:F1}";
                consumptionText.color = negativeColor;
            }

            if (productionText != null)
            {
                productionText.text = $"+{flowInfo.production:F1}";
                productionText.color = positiveColor;
            }

            if (netText != null)
            {
                string netSign = flowInfo.net >= 0 ? "+" : "";
                netText.text = $"{netSign}{flowInfo.net:F1}/min";

                // 设置净值颜色
                if (flowInfo.net > 0)
                    netText.color = positiveColor;
                else if (flowInfo.net < 0)
                    netText.color = negativeColor;
                else
                    netText.color = neutralColor;
            }
        }
        else
        {
            // 简单视图：只显示净值
            if (netText != null)
            {
                string netSign = flowInfo.net >= 0 ? "+" : "";
                netText.text = $"{flowInfo.resource.displayName}: {netSign}{flowInfo.net:F1}/min";

                // 设置净值颜色
                if (flowInfo.net > 0)
                    netText.color = positiveColor;
                else if (flowInfo.net < 0)
                    netText.color = negativeColor;
                else
                    netText.color = neutralColor;
            }

            // 隐藏其他文本
            if (consumptionText != null) consumptionText.gameObject.SetActive(false);
            if (productionText != null) productionText.gameObject.SetActive(false);
            if (resourceNameText != null) resourceNameText.gameObject.SetActive(false);
        }

        // 设置资源图标（如果有）
        if (resourceIcon != null && flowInfo.resource.icon != null)
        {
            resourceIcon.sprite = flowInfo.resource.icon;
            resourceIcon.enabled = true;
        }
        else if (resourceIcon != null)
        {
            resourceIcon.enabled = false;
        }
    }

    /// <summary>
    /// 设置为详细视图或简单视图
    /// </summary>
    public void SetDetailedView(bool detailed)
    {
        showDetailedView = detailed;
    }
}