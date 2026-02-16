using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Persistent right-side panel showing active Transport Jobs and Quests in tabs.
/// Always visible on the world map scene. Replaces TransportStatusUI.
///
/// Transport tab: uses VehicleTransportManager.GetActiveJobs() (MultiTripTransportJob)
/// Quest tab:     uses QuestManager.GetActiveQuests() (QuestInstance)
///
/// Refresh pattern: timer-based (same as old TransportStatusUI), only refreshes visible tab.
/// </summary>
public class ActiveTaskPanelUI : MonoBehaviour
{
    // ============ Tab Buttons ============

    [Header("Tab Buttons")]
    [Tooltip("Transport tab button")]
    public Button transportTabBtn;

    [Tooltip("Quest tab button")]
    public Button questTabBtn;

    [Header("Tab Visuals")]
    [Tooltip("Transport tab button text")]
    public TextMeshProUGUI transportTabText;

    [Tooltip("Quest tab button text")]
    public TextMeshProUGUI questTabText;

    [Tooltip("Transport tab highlight/underline (toggled active)")]
    public GameObject transportTabHighlight;

    [Tooltip("Quest tab highlight/underline (toggled active)")]
    public GameObject questTabHighlight;

    // ============ Transport Section ============

    [Header("Transport Section")]
    [Tooltip("Root GO for transport content (toggled by tab)")]
    public GameObject transportSection;

    [Tooltip("Transport job list item prefab (TransportJobItemUI)")]
    public GameObject transportJobPrefab;

    [Tooltip("Content parent for transport items (ScrollView Content with VLG)")]
    public Transform transportListParent;

    [Tooltip("Title text showing count (e.g. 'Transport (3)')")]
    public TextMeshProUGUI transportTitleText;

    [Tooltip("Summary text (e.g. 'Transporting 500 units, earliest ~1:23')")]
    public TextMeshProUGUI transportSummaryText;

    [Tooltip("Empty hint shown when no transport jobs")]
    public GameObject transportEmptyHint;

    [Header("Road Warning (Transport)")]
    [Tooltip("Road damage warning panel")]
    public GameObject roadWarningPanel;

    [Tooltip("Road damage warning text")]
    public TextMeshProUGUI roadWarningText;

    // ============ Quest Section ============

    [Header("Quest Section")]
    [Tooltip("Root GO for quest content (toggled by tab)")]
    public GameObject questSection;

    [Tooltip("Active quest list item prefab (ActiveQuestItemUI)")]
    public GameObject questItemPrefab;

    [Tooltip("Content parent for quest items (ScrollView Content with VLG)")]
    public Transform questListParent;

    [Tooltip("Title text showing count (e.g. 'Quests (2)')")]
    public TextMeshProUGUI questTitleText;

    [Tooltip("Empty hint shown when no active quests")]
    public GameObject questEmptyHint;

    // ============ Settings ============

    [Header("Settings")]
    [Tooltip("Refresh interval (seconds)")]
    public float refreshInterval = 0.5f;

    // ============ Runtime ============

    private enum ActiveTab { Transport, Quest }

    private ActiveTab _currentTab = ActiveTab.Transport;
    private float _refreshTimer;
    private readonly List<GameObject> _spawnedTransportItems = new();
    private readonly List<GameObject> _spawnedQuestItems = new();

    // ============ Lifecycle ============

    private void Start()
    {
        // Wire tab buttons
        if (transportTabBtn != null)
            transportTabBtn.onClick.AddListener(() => SwitchTab(ActiveTab.Transport));
        if (questTabBtn != null)
            questTabBtn.onClick.AddListener(() => SwitchTab(ActiveTab.Quest));

        // Default to Transport tab
        SwitchTab(ActiveTab.Transport);
    }

    private void Update()
    {
        _refreshTimer += Time.deltaTime;
        if (_refreshTimer >= refreshInterval)
        {
            _refreshTimer = 0f;
            Refresh();
        }
    }

    private void OnDisable()
    {
        ClearList(_spawnedTransportItems);
        ClearList(_spawnedQuestItems);
    }

    // ============ Tab Switching ============

    private void SwitchTab(ActiveTab tab)
    {
        _currentTab = tab;

        // Toggle section visibility
        if (transportSection != null)
            transportSection.SetActive(tab == ActiveTab.Transport);
        if (questSection != null)
            questSection.SetActive(tab == ActiveTab.Quest);

        // Tab highlight
        if (transportTabHighlight != null)
            transportTabHighlight.SetActive(tab == ActiveTab.Transport);
        if (questTabHighlight != null)
            questTabHighlight.SetActive(tab == ActiveTab.Quest);

        // Tab text styling: bold = active, normal = inactive
        if (transportTabText != null)
            transportTabText.fontStyle = tab == ActiveTab.Transport
                ? FontStyles.Bold : FontStyles.Normal;
        if (questTabText != null)
            questTabText.fontStyle = tab == ActiveTab.Quest
                ? FontStyles.Bold : FontStyles.Normal;

        // Force immediate refresh on tab switch
        _refreshTimer = refreshInterval;
    }

    // ============ Refresh ============

    private void Refresh()
    {
        // Only refresh the active tab for performance
        if (_currentTab == ActiveTab.Transport)
            RefreshTransport();
        else
            RefreshQuests();
    }

    // ============ Transport Tab ============

    private void RefreshTransport()
    {
        ClearList(_spawnedTransportItems);

        // Get active jobs
        var jobs = VehicleTransportManager.Instance != null
            ? VehicleTransportManager.Instance.GetActiveJobs()
            : new List<MultiTripTransportJob>();

        // Filter to active/pending only
        var activeJobs = new List<MultiTripTransportJob>();
        foreach (var job in jobs)
        {
            if (job.state == MultiTripState.Active || job.state == MultiTripState.Pending)
                activeJobs.Add(job);
        }

        // Title
        if (transportTitleText != null)
        {
            transportTitleText.text = activeJobs.Count > 0
                ? $"Transport ({activeJobs.Count})"
                : "Transport";
        }

        // Summary
        UpdateTransportSummary(activeJobs);

        // Road warning
        UpdateRoadWarning();

        // Empty hint
        if (transportEmptyHint != null)
            transportEmptyHint.SetActive(activeJobs.Count == 0);

        // Spawn items
        if (transportJobPrefab == null || transportListParent == null) return;

        for (int i = 0; i < activeJobs.Count; i++)
        {
            var job = activeJobs[i];
            var go = Instantiate(transportJobPrefab, transportListParent);
            go.SetActive(true);
            _spawnedTransportItems.Add(go);

            var itemUI = go.GetComponent<TransportJobItemUI>();
            if (itemUI != null)
            {
                var route = TradeManager.Instance != null
                    ? TradeManager.Instance.GetTradeRoute(job.routeId)
                    : null;
                itemUI.Setup(job, route);
                itemUI.SetAlternateBackground(i % 2 == 1);
            }
        }
    }

    // ============ Quest Tab ============

    private void RefreshQuests()
    {
        ClearList(_spawnedQuestItems);

        // Get active quests
        var quests = QuestManager.Instance != null
            ? QuestManager.Instance.GetActiveQuests()
            : new List<QuestInstance>();

        // Filter: show Accepted, InProgress, ReadyToSubmit
        var visibleQuests = new List<QuestInstance>();
        foreach (var quest in quests)
        {
            if (quest.IsActive || quest.state == QuestState.ReadyToSubmit)
                visibleQuests.Add(quest);
        }

        // Title
        if (questTitleText != null)
        {
            questTitleText.text = visibleQuests.Count > 0
                ? $"Quests ({visibleQuests.Count})"
                : "Quests";
        }

        // Empty hint
        if (questEmptyHint != null)
            questEmptyHint.SetActive(visibleQuests.Count == 0);

        // Spawn items
        if (questItemPrefab == null || questListParent == null) return;

        for (int i = 0; i < visibleQuests.Count; i++)
        {
            var quest = visibleQuests[i];
            var go = Instantiate(questItemPrefab, questListParent);
            go.SetActive(true);
            _spawnedQuestItems.Add(go);

            var itemUI = go.GetComponent<ActiveQuestItemUI>();
            if (itemUI != null)
            {
                itemUI.Setup(quest);
                itemUI.SetAlternateBackground(i % 2 == 1);
            }
        }
    }

    // ============ Transport Summary ============

    private void UpdateTransportSummary(List<MultiTripTransportJob> jobs)
    {
        if (transportSummaryText == null) return;

        if (jobs.Count == 0)
        {
            transportSummaryText.text = "";
            return;
        }

        float minETA = float.MaxValue;
        int totalCargo = 0;

        foreach (var job in jobs)
        {
            totalCargo += job.totalAmount;

            // Estimate this job's remaining time
            if (TradeManager.Instance != null)
            {
                var route = TradeManager.Instance.GetTradeRoute(job.routeId);
                if (route != null)
                {
                    float travelTime = TradeManager.Instance.CalculateTravelTime(route);
                    float returnTime = TradeManager.Instance.CalculateRoadTravelTime(route);
                    if (returnTime >= float.MaxValue) returnTime = travelTime;

                    float fullRoundTrip = travelTime + returnTime;

                    int remaining = job.totalTripsNeeded - job.tripsCompleted;
                    int vehicles = Mathf.Max(1, job.assignedVehicles);
                    int batches = Mathf.CeilToInt((float)remaining / vehicles);
                    float jobETA = batches * fullRoundTrip;

                    if (jobETA > 0f && jobETA < minETA)
                        minETA = jobETA;
                }
            }
        }

        if (minETA < float.MaxValue && minETA > 0f)
        {
            string timeStr = FormatTime(minETA);
            transportSummaryText.text = $"Transporting {totalCargo} units, earliest done ~{timeStr}";
        }
        else
        {
            transportSummaryText.text = $"Transporting {totalCargo} units";
        }
    }

    // ============ Road Warning ============
    // Same logic as old TransportStatusUI.UpdateRoadWarning(), now in English

    private void UpdateRoadWarning()
    {
        if (roadWarningPanel == null) return;

        if (TradeManager.Instance == null)
        {
            roadWarningPanel.SetActive(false);
            return;
        }

        var routes = TradeManager.Instance.GetAllTradeRoutes();
        int blockedCount = 0;

        foreach (var route in routes)
        {
            if (route.isActive && !route.isValid)
            {
                if (RoadNetwork.Instance != null && route.roadPath != null)
                {
                    if (!RoadNetwork.Instance.IsPathPassable(route.roadPath))
                        blockedCount++;
                }
            }
        }

        if (blockedCount > 0)
        {
            roadWarningPanel.SetActive(true);
            if (roadWarningText != null)
                roadWarningText.text = $"<color=red>Warning: {blockedCount} route(s) blocked by road damage!</color>";
        }
        else
        {
            roadWarningPanel.SetActive(false);
        }
    }

    // ============ Utility ============

    private static string FormatTime(float seconds)
    {
        if (seconds <= 0f) return "0:00";
        int min = Mathf.FloorToInt(seconds / 60f);
        int sec = Mathf.FloorToInt(seconds % 60f);
        return $"{min}:{sec:D2}";
    }

    private void ClearList(List<GameObject> list)
    {
        foreach (var item in list)
        {
            if (item != null) Destroy(item);
        }
        list.Clear();
    }
}
