using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 背包面板 — 显示所有持有的道具和装备
/// 两个标签页：道具 / 装备
/// </summary>
public class InventoryPanel : MonoBehaviour
{
    public static InventoryPanel Instance { get; private set; }

    // ============ UI References ============

    [Header("Panel")]
    public GameObject panel;
    public Button closeButton;

    [Header("Tabs")]
    public Button itemsTabButton;
    public Button equipTabButton;
    public TextMeshProUGUI itemsTabText;
    public TextMeshProUGUI equipTabText;

    [Header("Items View")]
    public GameObject itemsSection;
    public Transform itemListParent;
    public GameObject itemRowPrefab;

    [Header("Equipment View")]
    public GameObject equipSection;
    public Transform equipListParent;
    public GameObject equipRowPrefab;

    // ============ Runtime ============

    private readonly List<GameObject> _spawnedRows = new();
    private bool _showingItems = true;

    // ============ Lifecycle ============

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (panel != null) panel.SetActive(false);
    }

    private void Start()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (itemsTabButton != null) itemsTabButton.onClick.AddListener(() => SwitchTab(true));
        if (equipTabButton != null) equipTabButton.onClick.AddListener(() => SwitchTab(false));
    }

    // ============ Public API ============

    public void Open()
    {
        if (panel != null) panel.SetActive(true);
        SwitchTab(true);
    }

    public void Close()
    {
        ClearRows();
        if (panel != null) panel.SetActive(false);
    }

    // ============ Tabs ============

    private void SwitchTab(bool showItems)
    {
        _showingItems = showItems;

        if (itemsSection != null) itemsSection.SetActive(showItems);
        if (equipSection != null) equipSection.SetActive(!showItems);

        // Tab 高亮
        if (itemsTabText != null) itemsTabText.color = showItems ? Color.white : Color.gray;
        if (equipTabText != null) equipTabText.color = !showItems ? Color.white : Color.gray;

        Refresh();
    }

    // ============ Refresh ============

    private void Refresh()
    {
        ClearRows();
        var inv = PlayerInventoryManager.Instance;
        if (inv == null) return;

        if (_showingItems)
            RefreshItems(inv);
        else
            RefreshEquipment(inv);
    }

    private void RefreshItems(PlayerInventoryManager inv)
    {
        var items = inv.GetAllItems();
        foreach (var kvp in items)
        {
            if (itemRowPrefab == null || itemListParent == null) break;

            var row = Instantiate(itemRowPrefab, itemListParent);
            _spawnedRows.Add(row);

            var itemDef = inv.GetItemDef(kvp.Key);
            string displayName = itemDef != null ? itemDef.displayName : kvp.Key;
            Color rarityColor = itemDef != null ? itemDef.GetRarityColor() : Color.white;

            // 配置行内容（假设 prefab 有 NameText, CountText, Icon 子物体）
            var nameText = row.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            var countText = row.transform.Find("CountText")?.GetComponent<TextMeshProUGUI>();
            var iconImg = row.transform.Find("Icon")?.GetComponent<Image>();

            if (nameText != null)
            {
                nameText.text = displayName;
                nameText.color = rarityColor;
            }
            if (countText != null)
                countText.text = $"x{kvp.Value}";
            if (iconImg != null && itemDef != null && itemDef.icon != null)
                iconImg.sprite = itemDef.icon;
        }
    }

    private void RefreshEquipment(PlayerInventoryManager inv)
    {
        var equips = inv.GetAllEquipment();
        foreach (var equip in equips)
        {
            if (equipRowPrefab == null || equipListParent == null) break;

            var row = Instantiate(equipRowPrefab, equipListParent);
            _spawnedRows.Add(row);

            var equipDef = inv.GetEquipDef(equip.equipDefId);
            string displayName = equipDef != null ? equipDef.displayName : equip.equipDefId;
            string levelStr = equip.level > 0 ? $" +{equip.level}" : "";
            string equippedStr = !string.IsNullOrEmpty(equip.equippedToUnitId)
                ? $" [{equip.equippedToUnitId}]" : "";

            var nameText = row.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            var detailText = row.transform.Find("DetailText")?.GetComponent<TextMeshProUGUI>();
            var iconImg = row.transform.Find("Icon")?.GetComponent<Image>();

            if (nameText != null)
                nameText.text = $"{displayName}{levelStr}";
            if (detailText != null)
            {
                if (equipDef != null)
                {
                    var bonus = equipDef.GetBonusAtLevel(equip.level);
                    detailText.text = $"{equipDef.slot} | {bonus}{equippedStr}";
                }
                else
                {
                    detailText.text = equippedStr;
                }
            }
            if (iconImg != null && equipDef != null && equipDef.icon != null)
                iconImg.sprite = equipDef.icon;
        }
    }

    private void ClearRows()
    {
        foreach (var go in _spawnedRows)
            if (go != null) Destroy(go);
        _spawnedRows.Clear();
    }
}
