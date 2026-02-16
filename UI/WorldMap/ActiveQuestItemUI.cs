using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Individual quest display item for ActiveTaskPanelUI.
/// Simplified version of QuestBoardItemUI — no accept/submit buttons,
/// focused on progress tracking for the persistent side panel.
/// </summary>
public class ActiveQuestItemUI : MonoBehaviour
{
    [Header("Basic Info")]
    [Tooltip("Quest name")]
    public TextMeshProUGUI questNameText;

    [Tooltip("Quest type label (e.g. 'Delivery')")]
    public TextMeshProUGUI typeText;

    [Header("Progress")]
    [Tooltip("Progress bar (0..1)")]
    public Slider progressSlider;

    [Tooltip("Progress details text (e.g. 'Deliver iron_ore: 50/100')")]
    public TextMeshProUGUI progressDetailText;

    [Tooltip("Progress percentage text")]
    public TextMeshProUGUI progressPercentText;

    [Tooltip("Fill image for progress bar color")]
    public Image progressFillImage;

    [Header("Time")]
    [Tooltip("Remaining time text")]
    public TextMeshProUGUI timeText;

    [Header("State Indicators")]
    [Tooltip("State indicator bar (left side color strip)")]
    public Image stateBar;

    [Tooltip("'Ready to Submit' hint — shown when all requirements met")]
    public GameObject readyToSubmitHint;

    [Tooltip("Background image (for alternate row coloring)")]
    public Image backgroundImage;

    // ============ Setup ============

    /// <summary>
    /// Populate this item with a QuestInstance's data.
    /// </summary>
    public void Setup(QuestInstance quest)
    {
        if (quest == null) return;

        // ---- Quest name ----
        if (questNameText != null)
            questNameText.text = quest.displayName;

        // ---- Quest type ----
        if (typeText != null)
        {
            typeText.text = GetQuestTypeLabel(quest.questType);
            typeText.color = GetQuestTypeColor(quest.questType);
        }

        // ---- Progress bar ----
        if (progressSlider != null)
        {
            progressSlider.minValue = 0f;
            progressSlider.maxValue = 1f;
            progressSlider.value = quest.TotalProgress;
        }

        // ---- Progress percentage ----
        if (progressPercentText != null)
            progressPercentText.text = $"{quest.TotalProgress:P0}";

        // ---- Progress bar color (blue → green gradient) ----
        if (progressFillImage != null)
        {
            progressFillImage.color = Color.Lerp(
                new Color(0.2f, 0.6f, 1f),   // blue (start)
                new Color(0.2f, 0.9f, 0.3f),  // green (done)
                quest.TotalProgress
            );
        }

        // ---- Progress detail lines ----
        if (progressDetailText != null)
            progressDetailText.text = FormatProgressDetails(quest);

        // ---- Remaining time ----
        if (timeText != null)
        {
            if (quest.state == QuestState.ReadyToSubmit)
                timeText.text = "<color=green>Ready!</color>";
            else
                timeText.text = quest.FormattedRemainingTime;
        }

        // ---- State bar color ----
        if (stateBar != null)
            stateBar.color = GetStateColor(quest.state);

        // ---- Ready to submit hint ----
        if (readyToSubmitHint != null)
            readyToSubmitHint.SetActive(quest.state == QuestState.ReadyToSubmit);
    }

    /// <summary>
    /// Set alternate row background color.
    /// </summary>
    public void SetAlternateBackground(bool alternate)
    {
        if (backgroundImage != null)
        {
            backgroundImage.color = alternate
                ? new Color(1f, 1f, 1f, 0.03f)
                : new Color(0f, 0f, 0f, 0f);
        }
    }

    // ============ Formatting ============

    /// <summary>
    /// Format all quest progress entries into a multi-line string.
    /// </summary>
    private static string FormatProgressDetails(QuestInstance quest)
    {
        if (quest.progress == null || quest.progress.Count == 0)
            return "";

        var sb = new StringBuilder();
        foreach (var entry in quest.progress)
        {
            if (sb.Length > 0) sb.Append("\n");

            string typeName = entry.type switch
            {
                QuestRequirementType.DeliverResource => "Deliver",
                QuestRequirementType.ClearThreatZone => "Clear",
                QuestRequirementType.DefendOutpost   => "Defend",
                QuestRequirementType.ReachLocation    => "Reach",
                QuestRequirementType.DefeatEnemy      => "Defeat",
                _ => "Complete"
            };

            string resName = string.IsNullOrEmpty(entry.resourceId)
                ? ""
                : $" {MarketGoodsItemUI.FormatResourceName(entry.resourceId)}";

            string checkmark = entry.isComplete ? " \u2714" : "";
            sb.Append($"{typeName}{resName}: {entry.currentAmount}/{entry.requiredAmount}{checkmark}");
        }
        return sb.ToString();
    }

    // ============ Label / Color Helpers ============
    // Consistent with QuestBoardItemUI styling

    private static string GetQuestTypeLabel(QuestType type)
    {
        return type switch
        {
            QuestType.Delivery      => "Delivery",
            QuestType.ThreatClear   => "Clear Threat",
            QuestType.CombatDefend  => "Defend",
            QuestType.CombatRaid    => "Raid",
            QuestType.Exploration   => "Explore",
            _ => "Unknown"
        };
    }

    private static Color GetQuestTypeColor(QuestType type)
    {
        return type switch
        {
            QuestType.Delivery      => new Color(0.4f, 0.8f, 1f),
            QuestType.ThreatClear   => new Color(1f, 0.5f, 0.3f),
            QuestType.CombatDefend  => new Color(1f, 0.8f, 0.2f),
            QuestType.CombatRaid    => new Color(1f, 0.3f, 0.3f),
            QuestType.Exploration   => new Color(0.5f, 1f, 0.5f),
            _ => Color.white
        };
    }

    private static Color GetStateColor(QuestState state)
    {
        return state switch
        {
            QuestState.Available     => new Color(0.5f, 0.5f, 0.5f),
            QuestState.Accepted      => new Color(0.3f, 0.6f, 1f),
            QuestState.InProgress    => new Color(1f, 0.8f, 0.2f),
            QuestState.ReadyToSubmit => new Color(0.2f, 1f, 0.2f),
            QuestState.Completed     => new Color(0.2f, 0.8f, 0.2f),
            QuestState.Failed        => new Color(1f, 0.2f, 0.2f),
            QuestState.Expired       => new Color(0.4f, 0.4f, 0.4f),
            _ => Color.gray
        };
    }
}
