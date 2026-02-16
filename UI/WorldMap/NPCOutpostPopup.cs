using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// NPC Outpost info card — shows faction name, reputation, and two tab buttons.
/// Clicking Market/Quest tab hides this card and opens the corresponding sub-panel
/// in the same screen space. Sub-panels call ShowInfoCard() to return here.
///
/// Hierarchy (all under one parent GO, e.g. "NPCOutpostPopupPanel"):
///   - InfoCard  (this script's references)
///   - MarketSubPanel (separate GO with MarketSubPanel.cs)
///   - QuestSubPanel  (separate GO with QuestSubPanel.cs)
/// </summary>
public class NPCOutpostPopup : MonoBehaviour
{
    public static NPCOutpostPopup Instance { get; private set; }

    // ============ Info Card UI ============

    [Header("Info Card Root")]
    [Tooltip("The root GO that contains header, reputation, and tab bar")]
    public GameObject infoCardRoot;

    [Header("Header")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI factionNameText;
    public Button closeButton;

    [Header("Reputation")]
    public TextMeshProUGUI reputationLabel;
    public Slider reputationSlider;
    public TextMeshProUGUI reputationTierText;

    [Header("Tab Buttons")]
    public Button marketTabBtn;
    public Button questTabBtn;

    // ============ Sub-Panels ============

    [Header("Sub-Panels")]
    public MarketSubPanel marketSubPanel;
    public QuestSubPanel questSubPanel;

    // ============ Runtime ============

    private string _currentOutpostId;
    private NPCOutpost _currentOutpost;
    private NPCFaction _currentFaction;

    // Expose for sub-panels
    public string CurrentOutpostId => _currentOutpostId;
    public NPCOutpost CurrentOutpost => _currentOutpost;
    public NPCFaction CurrentFaction => _currentFaction;

    // ============ Lifecycle ============

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);

        if (marketTabBtn != null)
            marketTabBtn.onClick.AddListener(OpenMarket);

        if (questTabBtn != null)
            questTabBtn.onClick.AddListener(OpenQuest);
    }

    // ============ Show / Hide ============

    public void Show(string outpostId)
    {
        if (string.IsNullOrEmpty(outpostId)) return;
        if (NPCManager.Instance == null)
        {
            Debug.LogWarning("[NPCOutpostPopup] NPCManager not found");
            return;
        }

        _currentOutpostId = outpostId;
        _currentOutpost = NPCManager.Instance.GetOutpost(outpostId);
        if (_currentOutpost == null)
        {
            Debug.LogWarning($"[NPCOutpostPopup] Outpost not found: {outpostId}");
            return;
        }

        _currentFaction = NPCManager.Instance.GetFaction(_currentOutpost.factionId);

        gameObject.SetActive(true);
        ShowInfoCard();

        Debug.Log($"[NPCOutpostPopup] Show outpost: {_currentOutpost.displayName}");
    }

    public void Toggle(string outpostId)
    {
        if (gameObject.activeSelf && _currentOutpostId == outpostId)
            Hide();
        else
            Show(outpostId);
    }

    public void Hide()
    {
        // Hide everything
        if (infoCardRoot != null) infoCardRoot.SetActive(false);
        if (marketSubPanel != null) marketSubPanel.Close();
        if (questSubPanel != null) questSubPanel.Close();

        gameObject.SetActive(false);
        _currentOutpostId = null;
        _currentOutpost = null;
        _currentFaction = null;
    }

    // ============ Info Card ============

    /// <summary>
    /// Show the info card and hide all sub-panels.
    /// Called on initial open and when sub-panels click "Back".
    /// </summary>
    public void ShowInfoCard()
    {
        // Re-fetch latest data
        if (!string.IsNullOrEmpty(_currentOutpostId) && NPCManager.Instance != null)
        {
            _currentOutpost = NPCManager.Instance.GetOutpost(_currentOutpostId);
            _currentFaction = _currentOutpost != null
                ? NPCManager.Instance.GetFaction(_currentOutpost.factionId)
                : null;
        }

        // Hide sub-panels
        if (marketSubPanel != null) marketSubPanel.Close();
        if (questSubPanel != null) questSubPanel.Close();

        // Show info card
        if (infoCardRoot != null) infoCardRoot.SetActive(true);

        RefreshInfoCard();
    }

    private void RefreshInfoCard()
    {
        if (_currentOutpost == null) return;

        // Header
        if (titleText != null)
            titleText.text = _currentOutpost.displayName;

        if (factionNameText != null)
        {
            string factionName = _currentFaction != null ? _currentFaction.displayName : _currentOutpost.factionId;
            Color factionColor = _currentFaction != null ? _currentFaction.factionColor : Color.white;
            factionNameText.text = $"Faction: {factionName}";
            factionNameText.color = factionColor;
        }

        // Reputation
        string factionId = _currentOutpost.factionId;
        int reputation = NPCManager.Instance.GetReputation(factionId);
        string levelName = NPCFaction.GetReputationLevel(reputation);
        Color levelColor = NPCFaction.GetReputationColor(reputation);

        if (reputationLabel != null)
        {
            string sign = reputation >= 0 ? "+" : "";
            reputationLabel.text = $"Reputation: {levelName} ({sign}{reputation})";
            reputationLabel.color = levelColor;
        }

        if (reputationSlider != null)
        {
            reputationSlider.minValue = -100;
            reputationSlider.maxValue = 100;
            reputationSlider.value = reputation;
        }

        if (reputationTierText != null)
        {
            if (ReputationMarketSystem.Instance != null)
            {
                var tier = ReputationMarketSystem.Instance.GetCurrentTier(factionId);
                if (tier != null)
                {
                    string tradeInfo = tier.canTrade
                        ? $"Tradeable | Price x{tier.priceMultiplier:F1} | Limit {tier.maxQuantityPerResource}"
                        : "Trade Forbidden";
                    string specialInfo = tier.unlockSpecialGoods ? " | Special Goods Unlocked" : "";
                    reputationTierText.text = $"[{tier.tierName}] {tradeInfo}{specialInfo}";
                }
                else
                {
                    reputationTierText.text = "No market config";
                }
            }
            else
            {
                reputationTierText.text = "";
            }
        }
    }

    // ============ Tab Actions ============

    private void OpenMarket()
    {
        if (infoCardRoot != null) infoCardRoot.SetActive(false);
        if (questSubPanel != null) questSubPanel.Close();

        if (marketSubPanel != null)
            marketSubPanel.Open(_currentOutpostId, _currentOutpost, _currentFaction);
    }

    private void OpenQuest()
    {
        if (infoCardRoot != null) infoCardRoot.SetActive(false);
        if (marketSubPanel != null) marketSubPanel.Close();

        if (questSubPanel != null)
            questSubPanel.Open(_currentOutpostId, _currentOutpost, _currentFaction);
    }

    // ============ Utility (for sub-panels) ============

    public string GetPlayerBaseId()
    {
        if (BaseManager.Instance == null) return null;

        string activeId = BaseManager.Instance.ActiveBaseId;
        if (!string.IsNullOrEmpty(activeId))
            return activeId;

        var allBases = BaseManager.Instance.AllBaseSaveData;
        if (allBases != null && allBases.Count > 0)
            return allBases[0].baseId;

        return null;
    }
}
