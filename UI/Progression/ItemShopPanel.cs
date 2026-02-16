using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 道具商店面板 — 使用金钱购买养成道具和装备
/// 独立于NPC贸易系统的即买即得商店
/// </summary>
public class ItemShopPanel : MonoBehaviour
{
    public static ItemShopPanel Instance { get; private set; }

    // ============ UI References ============

    [Header("Panel")]
    public GameObject panel;
    public Button closeButton;

    [Header("Money Display")]
    public TextMeshProUGUI moneyText;

    [Header("Tabs")]
    public Button itemsTabButton;
    public Button equipTabButton;
    public TextMeshProUGUI itemsTabText;
    public TextMeshProUGUI equipTabText;

    [Header("Items View")]
    public GameObject itemsSection;
    public Transform itemListParent;
    public GameObject shopItemRowPrefab;

    [Header("Equipment View")]
    public GameObject equipSection;
    public Transform equipListParent;
    public GameObject shopEquipRowPrefab;

    [Header("Purchase Confirmation")]
    [Tooltip("购买确认弹窗")]
    public GameObject confirmPopup;
    public TextMeshProUGUI confirmText;
    public Button confirmYesButton;
    public Button confirmNoButton;

    [Header("Quantity Selector (for items)")]
    public GameObject quantityPopup;
    public TextMeshProUGUI quantityItemNameText;
    public TextMeshProUGUI quantityTotalCostText;
    public Slider quantitySlider;
    public TextMeshProUGUI quantityValueText;
    public Button quantityConfirmButton;
    public Button quantityCancelButton;

    [Tooltip("+/- 按钮（Slider 在 ScrollView 内拖拽冲突时的备选操作）")]
    public Button quantityPlusButton;
    public Button quantityMinusButton;

    // ============ Runtime ============

    private readonly List<GameObject> _spawnedRows = new();
    private bool _showingItems = true;
    private string _pendingBaseId;

    // 数量选择状态
    private string _pendingItemId;
    private float _pendingItemPrice;
    private int _pendingMaxQuantity;

    // 装备购买状态
    private string _pendingEquipDefId;
    private float _pendingEquipPrice;

    // ============ Lifecycle ============

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (panel != null) panel.SetActive(false);
        if (confirmPopup != null) confirmPopup.SetActive(false);
        if (quantityPopup != null) quantityPopup.SetActive(false);
    }

    private void Start()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);

        if (itemsTabButton != null) itemsTabButton.onClick.AddListener(() => SwitchTab(true));
        if (equipTabButton != null) equipTabButton.onClick.AddListener(() => SwitchTab(false));

        // 购买确认弹窗
        if (confirmYesButton != null) confirmYesButton.onClick.AddListener(OnConfirmYes);
        if (confirmNoButton != null) confirmNoButton.onClick.AddListener(OnConfirmNo);

        // 数量选择弹窗
        if (quantityConfirmButton != null) quantityConfirmButton.onClick.AddListener(OnQuantityConfirm);
        if (quantityCancelButton != null) quantityCancelButton.onClick.AddListener(OnQuantityCancel);
        if (quantitySlider != null) quantitySlider.onValueChanged.AddListener(OnQuantitySliderChanged);
        if (quantityPlusButton != null) quantityPlusButton.onClick.AddListener(OnQuantityPlus);
        if (quantityMinusButton != null) quantityMinusButton.onClick.AddListener(OnQuantityMinus);
    }

    // ============ Public API ============

    /// <summary>
    /// 打开商店面板
    /// </summary>
    /// <param name="payingBaseId">付款基地ID（用于扣钱）</param>
    public void Open(string payingBaseId = "")
    {
        _pendingBaseId = payingBaseId;

        // 如果未指定基地，尝试获取当前活跃基地
        if (string.IsNullOrEmpty(_pendingBaseId) && BaseManager.Instance != null)
        {
            var activeSave = BaseManager.Instance.GetActiveBaseSaveData();
            if (activeSave != null)
                _pendingBaseId = activeSave.baseId;
        }

        if (panel != null) panel.SetActive(true);
        SwitchTab(true);
    }

    public void Close()
    {
        ClearRows();
        if (panel != null) panel.SetActive(false);
        if (confirmPopup != null) confirmPopup.SetActive(false);
        if (quantityPopup != null) quantityPopup.SetActive(false);
    }

    // ============ Tabs ============

    private void SwitchTab(bool showItems)
    {
        _showingItems = showItems;

        if (itemsSection != null) itemsSection.SetActive(showItems);
        if (equipSection != null) equipSection.SetActive(!showItems);

        if (itemsTabText != null) itemsTabText.color = showItems ? Color.white : Color.gray;
        if (equipTabText != null) equipTabText.color = !showItems ? Color.white : Color.gray;

        Refresh();
    }

    // ============ Refresh ============

    private void Refresh()
    {
        ClearRows();
        UpdateMoneyDisplay();

        var shop = ItemShopManager.Instance;
        if (shop == null) return;

        if (_showingItems)
            RefreshItemListings(shop);
        else
            RefreshEquipListings(shop);
    }

    private void UpdateMoneyDisplay()
    {
        if (moneyText == null) return;

        float money = 0f;
        if (ItemShopManager.Instance != null)
            money = ItemShopManager.Instance.GetTotalMoney();

        moneyText.text = $"Money: ${money:N0}";
    }

    private void RefreshItemListings(ItemShopManager shop)
    {
        foreach (var listing in shop.itemListings)
        {
            if (listing.itemDef == null) continue;
            if (shopItemRowPrefab == null || itemListParent == null) break;

            var row = Instantiate(shopItemRowPrefab, itemListParent);
            _spawnedRows.Add(row);

            var nameT = row.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            var priceT = row.transform.Find("PriceText")?.GetComponent<TextMeshProUGUI>();
            var descT = row.transform.Find("DescText")?.GetComponent<TextMeshProUGUI>();
            var iconImg = row.transform.Find("Icon")?.GetComponent<Image>();
            var buyBtn = row.transform.Find("BuyButton")?.GetComponent<Button>();

            if (nameT != null)
            {
                nameT.text = listing.itemDef.displayName;
                nameT.color = listing.itemDef.GetRarityColor();
            }
            if (priceT != null)
                priceT.text = $"${listing.price:N0}";
            if (descT != null)
            {
                string typeStr = listing.itemDef.itemType == ItemType.ExpBook ? "EXP" : "Strengthen";
                descT.text = $"{typeStr} +{listing.itemDef.effectValue}";
            }
            if (iconImg != null && listing.itemDef.icon != null)
                iconImg.sprite = listing.itemDef.icon;

            // 购买按钮
            string capturedItemId = listing.itemDef.itemId;
            float capturedPrice = listing.price;
            int capturedMax = listing.maxPerPurchase > 0 ? listing.maxPerPurchase : 99;

            if (buyBtn != null)
            {
                buyBtn.onClick.AddListener(() =>
                {
                    OpenQuantitySelector(capturedItemId, capturedPrice, capturedMax);
                });
            }
        }
    }

    private void RefreshEquipListings(ItemShopManager shop)
    {
        foreach (var listing in shop.equipListings)
        {
            if (listing.equipDef == null) continue;
            if (shopEquipRowPrefab == null || equipListParent == null) break;

            var row = Instantiate(shopEquipRowPrefab, equipListParent);
            _spawnedRows.Add(row);

            var nameT = row.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            var priceT = row.transform.Find("PriceText")?.GetComponent<TextMeshProUGUI>();
            var descT = row.transform.Find("DescText")?.GetComponent<TextMeshProUGUI>();
            var iconImg = row.transform.Find("Icon")?.GetComponent<Image>();
            var buyBtn = row.transform.Find("BuyButton")?.GetComponent<Button>();

            if (nameT != null)
                nameT.text = listing.equipDef.displayName;

            if (priceT != null)
                priceT.text = $"${listing.price:N0}";

            if (descT != null)
            {
                var bonus = listing.equipDef.GetBonusAtLevel(0);
                var parts = new List<string>();
                if (bonus.attack > 0) parts.Add($"ATK+{bonus.attack}");
                if (bonus.defense > 0) parts.Add($"DEF+{bonus.defense}");
                if (bonus.hp > 0) parts.Add($"HP+{bonus.hp}");
                descT.text = $"[{listing.equipDef.slot}] {string.Join(" ", parts)}";
            }

            if (iconImg != null && listing.equipDef.icon != null)
                iconImg.sprite = listing.equipDef.icon;

            // 购买装备（每次只买一件）
            string capturedEquipId = listing.equipDef.equipId;
            float capturedEquipPrice = listing.price;

            if (buyBtn != null)
            {
                buyBtn.onClick.AddListener(() =>
                {
                    _pendingEquipDefId = capturedEquipId;
                    _pendingEquipPrice = capturedEquipPrice;
                    ShowConfirmPopup($"Purchase {listing.equipDef.displayName} for ${capturedEquipPrice:N0}?");
                });
            }
        }
    }

    // ============ Quantity Selector (Items) ============

    private void OpenQuantitySelector(string itemId, float price, int maxQty)
    {
        _pendingItemId = itemId;
        _pendingItemPrice = price;

        // 计算实际可购买的最大数量（基于所有基地金钱总和）
        float money = 0f;
        if (ItemShopManager.Instance != null)
            money = ItemShopManager.Instance.GetTotalMoney();

        int maxByMoney = price > 0 ? Mathf.FloorToInt(money / price) : maxQty;
        _pendingMaxQuantity = Mathf.Min(maxQty, maxByMoney);
        _pendingMaxQuantity = Mathf.Max(1, _pendingMaxQuantity);

        if (quantityPopup != null) quantityPopup.SetActive(true);

        var inv = PlayerInventoryManager.Instance;
        var itemDef = inv != null ? inv.GetItemDef(itemId) : null;

        if (quantityItemNameText != null)
            quantityItemNameText.text = itemDef != null ? itemDef.displayName : itemId;

        if (quantitySlider != null)
        {
            // 当 max == 1 时，设置 maxValue = 2 以使滑条可拖，但 wholeNumbers 确保只有 1 和 2
            // 实际限制在 OnQuantityConfirm 中通过 _pendingMaxQuantity 控制
            quantitySlider.minValue = 1;
            quantitySlider.maxValue = Mathf.Max(2, _pendingMaxQuantity);
            quantitySlider.wholeNumbers = true;
            quantitySlider.value = 1;
        }

        UpdateQuantityDisplay(1);
    }

    private void OnQuantitySliderChanged(float value)
    {
        int qty = Mathf.Clamp(Mathf.RoundToInt(value), 1, _pendingMaxQuantity);

        // 如果滑条超出实际可购买量，强制回拉
        if (quantitySlider != null && Mathf.RoundToInt(quantitySlider.value) != qty)
            quantitySlider.SetValueWithoutNotify(qty);

        UpdateQuantityDisplay(qty);
    }

    private void OnQuantityPlus()
    {
        if (quantitySlider == null) return;
        int current = Mathf.RoundToInt(quantitySlider.value);
        int next = Mathf.Min(current + 1, _pendingMaxQuantity);
        quantitySlider.value = next;
    }

    private void OnQuantityMinus()
    {
        if (quantitySlider == null) return;
        int current = Mathf.RoundToInt(quantitySlider.value);
        int next = Mathf.Max(current - 1, 1);
        quantitySlider.value = next;
    }

    private void UpdateQuantityDisplay(int qty)
    {
        if (quantityValueText != null)
            quantityValueText.text = qty.ToString();
        if (quantityTotalCostText != null)
            quantityTotalCostText.text = $"Total: ${_pendingItemPrice * qty:N0}";
    }

    private void OnQuantityConfirm()
    {
        int qty = quantitySlider != null ? Mathf.RoundToInt(quantitySlider.value) : 1;
        qty = Mathf.Clamp(qty, 1, _pendingMaxQuantity);

        bool success = ItemShopManager.Instance != null &&
                       ItemShopManager.Instance.TryBuyItem(_pendingItemId, qty, _pendingBaseId);

        if (quantityPopup != null) quantityPopup.SetActive(false);

        if (success)
        {
            Debug.Log($"[ShopPanel] Purchased {qty}x {_pendingItemId}");
            Refresh();
        }
        else
        {
            Debug.Log("[ShopPanel] Purchase failed (insufficient funds?)");
        }
    }

    private void OnQuantityCancel()
    {
        if (quantityPopup != null) quantityPopup.SetActive(false);
    }

    // ============ Confirm Popup (Equipment) ============

    private void ShowConfirmPopup(string message)
    {
        if (confirmPopup != null) confirmPopup.SetActive(true);
        if (confirmText != null) confirmText.text = message;
    }

    private void OnConfirmYes()
    {
        if (confirmPopup != null) confirmPopup.SetActive(false);

        if (!string.IsNullOrEmpty(_pendingEquipDefId))
        {
            bool success = ItemShopManager.Instance != null &&
                           ItemShopManager.Instance.TryBuyEquipment(_pendingEquipDefId, _pendingBaseId);

            if (success)
            {
                Debug.Log($"[ShopPanel] Purchased equipment: {_pendingEquipDefId}");
                Refresh();
            }
            else
            {
                Debug.Log("[ShopPanel] Equipment purchase failed");
            }

            _pendingEquipDefId = "";
        }
    }

    private void OnConfirmNo()
    {
        if (confirmPopup != null) confirmPopup.SetActive(false);
        _pendingEquipDefId = "";
    }

    // ============ Cleanup ============

    private void ClearRows()
    {
        foreach (var go in _spawnedRows)
            if (go != null) Destroy(go);
        _spawnedRows.Clear();
    }
}
