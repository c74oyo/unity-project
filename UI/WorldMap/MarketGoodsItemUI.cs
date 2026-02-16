using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Market goods list item UI - displays a single tradeable resource.
/// All layout is handled in the prefab via Unity Editor (LayoutElement, etc.).
/// </summary>
public class MarketGoodsItemUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Resource name")]
    public TextMeshProUGUI resourceNameText;

    [Tooltip("Amount")]
    public TextMeshProUGUI amountText;

    [Tooltip("Unit price")]
    public TextMeshProUGUI priceText;

    [Tooltip("Total value")]
    public TextMeshProUGUI totalValueText;

    [Tooltip("Trade direction label (Sell/Buy)")]
    public TextMeshProUGUI directionLabel;

    [Tooltip("Special goods badge")]
    public GameObject specialBadge;

    [Tooltip("Background image (for alternating colors)")]
    public Image backgroundImage;

    [Header("Actions")]
    [Tooltip("Trade button - opens quantity selector")]
    public Button tradeBtn;

    // Callback & cached data
    private Action<ResourceStock, bool> _onTrade;
    private ResourceStock _stock;
    private bool _isSell;

    // ============ Setup ============

    /// <summary>
    /// Set up item data and optional trade callback.
    /// If onTrade is null, the trade button is hidden.
    /// </summary>
    public void Setup(ResourceStock stock, bool isSell, bool isSpecial = false,
                      Action<ResourceStock, bool> onTrade = null)
    {
        if (stock == null) return;

        _stock = stock;
        _isSell = isSell;
        _onTrade = onTrade;

        if (resourceNameText != null)
            resourceNameText.text = FormatResourceName(stock.resourceId);

        if (amountText != null)
            amountText.text = $"x{stock.amount}";

        if (priceText != null)
            priceText.text = $"${stock.pricePerUnit:F1}";

        if (totalValueText != null)
            totalValueText.text = $"${stock.TotalValue:F0}";

        if (directionLabel != null)
        {
            directionLabel.text = isSell ? "SELL" : "BUY";
            directionLabel.color = isSell ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.8f, 0.6f, 0.2f);
        }

        if (specialBadge != null)
            specialBadge.SetActive(isSpecial);

        // Trade button
        if (tradeBtn != null)
        {
            tradeBtn.gameObject.SetActive(onTrade != null);
            tradeBtn.onClick.RemoveAllListeners();
            if (onTrade != null)
                tradeBtn.onClick.AddListener(() => _onTrade?.Invoke(_stock, _isSell));
        }
    }

    public void SetAlternateBackground(bool isOdd)
    {
        if (backgroundImage != null)
        {
            backgroundImage.color = isOdd
                ? new Color(0.15f, 0.15f, 0.15f, 0.5f)
                : new Color(0.1f, 0.1f, 0.1f, 0.3f);
        }
    }

    // ============ Utility ============

    public static string FormatResourceName(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId)) return "???";

        string name = resourceId.Replace("_", " ");
        if (name.Length > 0)
            name = char.ToUpper(name[0]) + name.Substring(1);
        return name;
    }
}
