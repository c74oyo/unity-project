using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 养成系统底部工具栏 — 提供背包/角色/商店入口按钮
/// 挂在 World Map 场景 Canvas 下的 Toolbar GameObject 上
///
/// Hierarchy 搭建示例:
/// ProgressionToolbar (BottomCenter, HorizontalLayoutGroup)
///   ├── BagButton    (Button + Image + Text "背包")
///   ├── CharButton   (Button + Image + Text "角色")
///   └── ShopButton   (Button + Image + Text "商店")
/// </summary>
public class ProgressionToolbar : MonoBehaviour
{
    public static ProgressionToolbar Instance { get; private set; }

    // ============ Buttons ============

    [Header("Toolbar Buttons")]
    [Tooltip("背包按钮")]
    public Button bagButton;

    [Tooltip("角色按钮")]
    public Button characterButton;

    [Tooltip("商店按钮")]
    public Button shopButton;

    // ============ Panel References ============

    [Header("Panel References (拖拽对应面板)")]
    [Tooltip("背包面板")]
    public InventoryPanel inventoryPanel;

    [Tooltip("角色列表面板")]
    public CharacterRosterPanel characterRosterPanel;

    [Tooltip("商店面板")]
    public ItemShopPanel itemShopPanel;

    // ============ Optional: Keyboard Shortcuts ============

    [Header("Keyboard Shortcuts")]
    [Tooltip("背包快捷键")]
    public KeyCode bagKey = KeyCode.B;

    [Tooltip("角色快捷键")]
    public KeyCode characterKey = KeyCode.C;

    [Tooltip("商店快捷键")]
    public KeyCode shopKey = KeyCode.P;

    // ============ Lifecycle ============

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (bagButton != null)
            bagButton.onClick.AddListener(ToggleBag);

        if (characterButton != null)
            characterButton.onClick.AddListener(ToggleCharacter);

        if (shopButton != null)
            shopButton.onClick.AddListener(ToggleShop);
    }

    private void Update()
    {
        // 快捷键支持（仅在没有输入框聚焦时）
        if (Input.GetKeyDown(bagKey))
            ToggleBag();
        if (Input.GetKeyDown(characterKey))
            ToggleCharacter();
        if (Input.GetKeyDown(shopKey))
            ToggleShop();
    }

    // ============ Toggle Methods ============

    public void ToggleBag()
    {
        // 关闭其他面板
        CloseAllExcept(inventoryPanel?.panel);

        if (inventoryPanel != null)
        {
            if (inventoryPanel.panel != null && inventoryPanel.panel.activeSelf)
                inventoryPanel.Close();
            else
                inventoryPanel.Open();
        }
    }

    public void ToggleCharacter()
    {
        CloseAllExcept(characterRosterPanel?.panel);

        if (characterRosterPanel != null)
        {
            if (characterRosterPanel.panel != null && characterRosterPanel.panel.activeSelf)
                characterRosterPanel.Close();
            else
                characterRosterPanel.Open();
        }
    }

    public void ToggleShop()
    {
        CloseAllExcept(itemShopPanel?.panel);

        if (itemShopPanel != null)
        {
            if (itemShopPanel.panel != null && itemShopPanel.panel.activeSelf)
                itemShopPanel.Close();
            else
                itemShopPanel.Open();
        }
    }

    // ============ Helper ============

    /// <summary>
    /// 关闭除了指定面板以外的所有养成面板
    /// 保证同一时间只打开一个主面板
    /// </summary>
    private void CloseAllExcept(GameObject except)
    {
        if (inventoryPanel != null && inventoryPanel.panel != except)
            inventoryPanel.Close();

        if (characterRosterPanel != null && characterRosterPanel.panel != except)
            characterRosterPanel.Close();

        if (itemShopPanel != null && itemShopPanel.panel != except)
            itemShopPanel.Close();

        // 也关闭角色详情面板（它是从角色列表打开的子面板）
        if (CharacterDetailPanel.Instance != null)
            CharacterDetailPanel.Instance.Close();
    }

    /// <summary>
    /// 关闭所有养成面板
    /// </summary>
    public void CloseAll()
    {
        CloseAllExcept(null);
    }
}
