using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 资源库存显示项 - 显示资源名称、数量和容量
/// </summary>
public class ResourceStockItemUI : MonoBehaviour
{
    [Header("UI Elements")]
    public Image resourceIcon;
    public TextMeshProUGUI resourceNameText;
    public TextMeshProUGUI amountText;
    public Slider capacitySlider;
    public Image fillImage;

    [Header("Colors")]
    public Color normalFillColor = Color.green;
    public Color warningFillColor = Color.yellow;
    public Color criticalFillColor = Color.red;
    public float warningThreshold = 0.8f;
    public float criticalThreshold = 0.95f;

    public void Setup(BaseInfoData.ResourceStockInfo stockInfo)
    {
        if (stockInfo == null || stockInfo.resource == null) return;

        // 设置资源名称
        if (resourceNameText != null)
            resourceNameText.text = stockInfo.resource.displayName;

        // 设置数量文本
        if (amountText != null)
            amountText.text = $"{stockInfo.current}/{stockInfo.capacity}";

        // 设置进度条
        if (capacitySlider != null)
        {
            capacitySlider.maxValue = 1f;
            capacitySlider.value = stockInfo.percentage;
        }

        // 设置填充图像
        if (fillImage != null)
        {
            fillImage.fillAmount = stockInfo.percentage;

            // 根据百分比设置颜色
            if (stockInfo.percentage >= criticalThreshold)
                fillImage.color = criticalFillColor;
            else if (stockInfo.percentage >= warningThreshold)
                fillImage.color = warningFillColor;
            else
                fillImage.color = normalFillColor;
        }

        // 设置资源图标（如果有）
        if (resourceIcon != null && stockInfo.resource.icon != null)
        {
            resourceIcon.sprite = stockInfo.resource.icon;
            resourceIcon.enabled = true;
        }
        else if (resourceIcon != null)
        {
            resourceIcon.enabled = false;
        }
    }
}