using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Standalone Quest sub-panel. Opened by NPCOutpostPopup when the Quest tab is clicked.
/// Fills the same area as the info card.
///
/// Unity Editor setup:
///   1. Create a GO "QuestSubPanel" as sibling of InfoCard, under the popup root.
///   2. RectTransform: stretch-fill the popup area (anchors 0,0 to 1,1, offsets 0).
///   3. Add a dark background Image.
///   4. Children (top to bottom):
///      - TopBar: BackButton (left) + TitleLabel ("Quests") (center)
///      - AvailableScrollView (ScrollRect + Viewport + Content), Content has VLG + CSF
///      - ActiveQuestsHeader TMP (hidden when no active quests)
///      - ActiveScrollView (same structure)
///      - EmptyHint TMP (hidden by default)
///   5. QuestBoardItem prefab: a card with VLG, each quest one card.
///   6. Drag references into this script's Inspector fields.
///   7. Default: SetActive(false) in scene.
/// </summary>
public class QuestSubPanel : MonoBehaviour
{
    [Header("Navigation")]
    public Button backButton;
    public TextMeshProUGUI panelTitle;

    [Header("Available Quests")]
    public Transform questListParent;      // Content of AvailableScrollView

    [Header("Active Quests")]
    public TextMeshProUGUI activeQuestsHeader;
    public Transform activeQuestListParent; // Content of ActiveScrollView

    [Header("Empty State")]
    public TextMeshProUGUI emptyHint;

    [Header("Prefab")]
    public GameObject questItemPrefab;

    // Runtime
    private string _outpostId;
    private NPCOutpost _outpost;
    private NPCFaction _faction;
    private float _refreshTimer;
    private const float RefreshInterval = 1f;

    private readonly List<GameObject> _spawnedQuests = new();
    private readonly List<GameObject> _spawnedActiveQuests = new();

    private void Start()
    {
        if (backButton != null)
            backButton.onClick.AddListener(OnBack);
    }

    private void Update()
    {
        if (!gameObject.activeSelf) return;

        _refreshTimer -= Time.unscaledDeltaTime;
        if (_refreshTimer <= 0f)
        {
            _refreshTimer = RefreshInterval;
            Refresh();
        }
    }

    // ============ Open / Close ============

    public void Open(string outpostId, NPCOutpost outpost, NPCFaction faction)
    {
        _outpostId = outpostId;
        _outpost = outpost;
        _faction = faction;

        gameObject.SetActive(true);

        if (panelTitle != null)
        {
            string name = outpost != null ? outpost.displayName : outpostId;
            panelTitle.text = $"Quests - {name}";
        }

        _refreshTimer = 0f;
    }

    public void Close()
    {
        ClearAll();
        gameObject.SetActive(false);
        _outpost = null;
        _faction = null;
    }

    private void OnBack()
    {
        if (NPCOutpostPopup.Instance != null)
            NPCOutpostPopup.Instance.ShowInfoCard();
    }

    // ============ Refresh ============

    private void Refresh()
    {
        ClearAll();

        if (_outpost == null || QuestManager.Instance == null || NPCManager.Instance == null) return;

        // Re-fetch latest data
        _outpost = NPCManager.Instance.GetOutpost(_outpostId);
        if (_outpost == null) return;

        // === Available quests ===
        var availableQuests = QuestManager.Instance.GetAvailableQuests(_outpostId);

        if (availableQuests.Count > 0)
        {
            foreach (var quest in availableQuests)
            {
                SpawnQuestItem(questListParent, quest, false, _spawnedQuests);
            }
        }

        if (emptyHint != null)
            emptyHint.gameObject.SetActive(availableQuests.Count == 0);

        // === Active quests (for this faction) ===
        var activeQuests = QuestManager.Instance.GetActiveQuestsByFaction(_outpost.factionId);

        if (activeQuestsHeader != null)
            activeQuestsHeader.gameObject.SetActive(activeQuests.Count > 0);

        if (activeQuests.Count > 0)
        {
            foreach (var quest in activeQuests)
            {
                SpawnQuestItem(activeQuestListParent, quest, true, _spawnedActiveQuests);
            }
        }
    }

    // ============ Spawn ============

    private void SpawnQuestItem(Transform parent, QuestInstance quest, bool isActive,
                                 List<GameObject> targetList)
    {
        if (questItemPrefab == null || parent == null) return;

        var go = Instantiate(questItemPrefab, parent);
        go.SetActive(true);
        targetList.Add(go);

        var ui = go.GetComponent<QuestBoardItemUI>();
        if (ui != null)
        {
            ui.Setup(quest, isActive, OnAcceptQuest, OnSubmitQuest);
        }
    }

    // ============ Quest Actions ============

    private void OnAcceptQuest(string questInstanceId)
    {
        if (QuestManager.Instance == null) return;

        string baseId = NPCOutpostPopup.Instance != null
            ? NPCOutpostPopup.Instance.GetPlayerBaseId()
            : null;

        if (string.IsNullOrEmpty(baseId))
        {
            Debug.LogWarning("[QuestSubPanel] No player base found to accept quest");
            return;
        }

        bool success = QuestManager.Instance.AcceptQuest(questInstanceId, baseId);
        if (success)
        {
            Debug.Log($"[QuestSubPanel] Quest accepted: {questInstanceId}");
            _refreshTimer = 0f;
        }
        else
        {
            Debug.LogWarning($"[QuestSubPanel] Failed to accept quest: {questInstanceId}");
        }
    }

    private void OnSubmitQuest(string questInstanceId)
    {
        if (QuestManager.Instance == null) return;

        bool success = QuestManager.Instance.TrySubmitQuest(questInstanceId);
        if (success)
        {
            Debug.Log($"[QuestSubPanel] Quest submitted: {questInstanceId}");
            _refreshTimer = 0f;
        }
        else
        {
            Debug.LogWarning($"[QuestSubPanel] Failed to submit quest: {questInstanceId}");
        }
    }

    // ============ Cleanup ============

    private void ClearAll()
    {
        foreach (var go in _spawnedQuests)
        {
            if (go != null) Destroy(go);
        }
        _spawnedQuests.Clear();

        foreach (var go in _spawnedActiveQuests)
        {
            if (go != null) Destroy(go);
        }
        _spawnedActiveQuests.Clear();
    }
}
