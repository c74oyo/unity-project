using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Popup for selecting trade quantity before creating a TradeRoute.
/// Singleton pattern. Follows ConfirmReplacePopupUI pattern (root GO, TMPs, buttons, Open/Close).
///
/// Unity Editor hierarchy:
///   TradeQuantityPopup (this script)
///   ├── Overlay (Image, dark semi-transparent, raycast target to block clicks behind)
///   └── Panel (centered Image)
///       ├── TitleText TMP
///       ├── StockInfoText TMP
///       ├── QuantityRow (HLG)
///       │   ├── DecreaseBtn "-"
///       │   ├── QuantityInput (TMP_InputField)
///       │   ├── IncreaseBtn "+"
///       │   └── MaxBtn "Max"
///       ├── UnitPriceText TMP
///       ├── TotalPriceText TMP
///       ├── ErrorText TMP (red, hidden by default)
///       └── ButtonRow (HLG)
///           ├── ConfirmBtn "Confirm"
///           └── CancelBtn "Cancel"
/// </summary>
public class TradeQuantityPopup : MonoBehaviour
{
    public static TradeQuantityPopup Instance { get; private set; }

    [Header("Root")]
    [Tooltip("The root panel GO - toggled via SetActive")]
    public GameObject root;

    [Header("Info Display")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI unitPriceText;
    public TextMeshProUGUI totalPriceText;
    public TextMeshProUGUI stockInfoText;

    [Header("Quantity Controls")]
    public TMP_InputField quantityInput;
    public Button increaseBtn;
    public Button decreaseBtn;
    public Button maxBtn;

    [Header("Buttons")]
    public Button confirmBtn;
    public Button cancelBtn;

    [Header("Error")]
    public TextMeshProUGUI errorText;

    // Runtime
    private int _quantity = 1;
    private int _maxQuantity = 1;
    private int _stockAmount;
    private float _unitPrice;
    private Action<int> _onConfirm;

    // ============ Lifecycle ============

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (root != null)
            root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        if (increaseBtn != null)
            increaseBtn.onClick.AddListener(() => AdjustQuantity(1));

        if (decreaseBtn != null)
            decreaseBtn.onClick.AddListener(() => AdjustQuantity(-1));

        if (maxBtn != null)
            maxBtn.onClick.AddListener(() => SetQuantity(_maxQuantity));

        if (confirmBtn != null)
            confirmBtn.onClick.AddListener(OnConfirm);

        if (cancelBtn != null)
            cancelBtn.onClick.AddListener(Close);

        if (quantityInput != null)
            quantityInput.onEndEdit.AddListener(OnInputChanged);
    }

    // ============ Open / Close ============

    /// <summary>
    /// Open the quantity selector popup.
    /// </summary>
    /// <param name="resourceName">Display name of the resource</param>
    /// <param name="isSell">True if NPC is selling (player buys/imports)</param>
    /// <param name="unitPrice">Price per unit (with tax/multiplier applied)</param>
    /// <param name="maxQuantity">Max tradeable quantity (min of stock and reputation limit)</param>
    /// <param name="stockAmount">Total stock available at outpost</param>
    /// <param name="onConfirm">Callback with chosen quantity</param>
    public void Open(string resourceName, bool isSell, float unitPrice,
                     int maxQuantity, int stockAmount, Action<int> onConfirm)
    {
        _unitPrice = unitPrice;
        _maxQuantity = Mathf.Max(1, maxQuantity);
        _stockAmount = stockAmount;
        _quantity = 1;
        _onConfirm = onConfirm;

        // Title: from player's perspective
        // isSell = NPC sells = player buys
        if (titleText != null)
        {
            string action = isSell ? "Buy" : "Sell";
            titleText.text = $"{action} {resourceName}";
        }

        // Stock info
        if (stockInfoText != null)
            stockInfoText.text = $"Available: {stockAmount} | Trade limit: {maxQuantity}";

        // Clear error
        if (errorText != null)
            errorText.gameObject.SetActive(false);

        RefreshDisplay();

        if (root != null)
            root.SetActive(true);
    }

    /// <summary>
    /// Open the popup in error-only mode — shows a title + error message + Cancel button only.
    /// All quantity controls and Confirm button are hidden.
    /// </summary>
    public void OpenError(string title, string errorMessage)
    {
        _onConfirm = null;

        if (titleText != null)
            titleText.text = title;

        // Show error
        if (errorText != null)
        {
            errorText.text = errorMessage;
            errorText.gameObject.SetActive(true);
        }

        // Hide everything except title, error, and cancel
        SetQuantityControlsVisible(false);

        if (stockInfoText != null) stockInfoText.gameObject.SetActive(false);
        if (unitPriceText != null) unitPriceText.gameObject.SetActive(false);
        if (totalPriceText != null) totalPriceText.gameObject.SetActive(false);
        if (confirmBtn != null) confirmBtn.gameObject.SetActive(false);

        if (root != null)
            root.SetActive(true);
    }

    public void Close()
    {
        if (root != null)
            root.SetActive(false);

        _onConfirm = null;

        // Restore all elements for next Open()
        SetQuantityControlsVisible(true);
        if (stockInfoText != null) stockInfoText.gameObject.SetActive(true);
        if (unitPriceText != null) unitPriceText.gameObject.SetActive(true);
        if (totalPriceText != null) totalPriceText.gameObject.SetActive(true);
        if (confirmBtn != null) confirmBtn.gameObject.SetActive(true);
        if (errorText != null) errorText.gameObject.SetActive(false);
    }

    private void SetQuantityControlsVisible(bool visible)
    {
        if (quantityInput != null) quantityInput.gameObject.SetActive(visible);
        if (increaseBtn != null) increaseBtn.gameObject.SetActive(visible);
        if (decreaseBtn != null) decreaseBtn.gameObject.SetActive(visible);
        if (maxBtn != null) maxBtn.gameObject.SetActive(visible);
    }

    // ============ Quantity Controls ============

    private void AdjustQuantity(int delta)
    {
        SetQuantity(_quantity + delta);
    }

    private void SetQuantity(int value)
    {
        _quantity = Mathf.Clamp(value, 1, _maxQuantity);
        RefreshDisplay();
    }

    private void OnInputChanged(string text)
    {
        if (int.TryParse(text, out int value))
            SetQuantity(value);
        else
            RefreshDisplay(); // Reset display to current valid quantity
    }

    private void RefreshDisplay()
    {
        _quantity = Mathf.Clamp(_quantity, 1, _maxQuantity);

        if (quantityInput != null)
            quantityInput.text = _quantity.ToString();

        if (unitPriceText != null)
            unitPriceText.text = $"${_unitPrice:F1} / unit";

        if (totalPriceText != null)
            totalPriceText.text = $"Total: ${_quantity * _unitPrice:F0}";

        // Enable/disable buttons at bounds
        if (decreaseBtn != null)
            decreaseBtn.interactable = _quantity > 1;

        if (increaseBtn != null)
            increaseBtn.interactable = _quantity < _maxQuantity;

        if (maxBtn != null)
            maxBtn.interactable = _quantity < _maxQuantity;

        if (confirmBtn != null)
            confirmBtn.interactable = _quantity > 0 && _maxQuantity > 0;
    }

    // ============ Actions ============

    private void OnConfirm()
    {
        var callback = _onConfirm;
        int qty = _quantity;
        Close();
        callback?.Invoke(qty);
    }
}
