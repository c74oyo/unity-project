using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Quest board list item UI - displays a single quest's info.
/// Used for both available quests and active (in-progress) quests.
/// All layout is handled in the prefab via Unity Editor.
/// </summary>
public class QuestBoardItemUI : MonoBehaviour
{
    [Header("Basic Info")]
    [Tooltip("Quest name")]
    public TextMeshProUGUI questNameText;

    [Tooltip("Quest type label")]
    public TextMeshProUGUI typeText;

    [Tooltip("Quest description")]
    public TextMeshProUGUI descriptionText;

    [Tooltip("Difficulty label")]
    public TextMeshProUGUI difficultyText;

    [Header("Progress")]
    [Tooltip("Progress bar (active quests only)")]
    public Slider progressSlider;

    [Tooltip("Progress percentage text")]
    public TextMeshProUGUI progressText;

    [Tooltip("Requirement detail text (e.g. ore 50/100)")]
    public TextMeshProUGUI requirementText;

    [Header("Reward")]
    [Tooltip("Reward summary text")]
    public TextMeshProUGUI rewardText;

    [Header("Time")]
    [Tooltip("Remaining time / status text")]
    public TextMeshProUGUI timeText;

    [Header("Buttons")]
    [Tooltip("Accept quest button")]
    public Button acceptBtn;

    [Tooltip("Submit quest button")]
    public Button submitBtn;

    [Header("Visuals")]
    [Tooltip("Background image")]
    public Image backgroundImage;

    [Tooltip("Status indicator bar (left side)")]
    public Image statusBar;

    private Action<string> _onAccept;
    private Action<string> _onSubmit;
    private string _questInstanceId;

    // ============ Setup ============

    public void Setup(QuestInstance quest, bool isActive,
                      Action<string> onAccept = null, Action<string> onSubmit = null)
    {
        if (quest == null) return;

        _questInstanceId = quest.instanceId;
        _onAccept = onAccept;
        _onSubmit = onSubmit;

        if (questNameText != null)
            questNameText.text = quest.displayName;

        if (typeText != null)
        {
            typeText.text = GetQuestTypeLabel(quest.questType);
            typeText.color = GetQuestTypeColor(quest.questType);
        }

        if (descriptionText != null)
            descriptionText.text = quest.description;

        if (difficultyText != null)
        {
            difficultyText.text = GetDifficultyLabel(quest.difficulty);
            difficultyText.color = GetDifficultyColor(quest.difficulty);
        }

        bool showProgress = isActive && quest.state != QuestState.Available;
        if (progressSlider != null)
        {
            progressSlider.gameObject.SetActive(showProgress);
            if (showProgress)
                progressSlider.value = quest.TotalProgress;
        }

        if (progressText != null)
        {
            progressText.gameObject.SetActive(showProgress);
            if (showProgress)
                progressText.text = $"{quest.TotalProgress:P0}";
        }

        if (requirementText != null)
            requirementText.text = FormatRequirements(quest);

        if (rewardText != null)
            rewardText.text = FormatReward(quest.reward);

        if (timeText != null)
        {
            if (quest.state == QuestState.ReadyToSubmit)
                timeText.text = "Ready";
            else if (isActive)
                timeText.text = quest.FormattedRemainingTime;
            else
                timeText.text = "";
        }

        SetupButtons(quest, isActive);

        if (statusBar != null)
            statusBar.color = GetStateColor(quest.state);
    }

    private void SetupButtons(QuestInstance quest, bool isActive)
    {
        if (acceptBtn != null)
        {
            bool showAccept = !isActive && quest.state == QuestState.Available;
            acceptBtn.gameObject.SetActive(showAccept);
            acceptBtn.onClick.RemoveAllListeners();
            if (showAccept && _onAccept != null)
            {
                acceptBtn.onClick.AddListener(() => _onAccept?.Invoke(_questInstanceId));
            }
        }

        if (submitBtn != null)
        {
            bool showSubmit = quest.state == QuestState.ReadyToSubmit;
            submitBtn.gameObject.SetActive(showSubmit);
            submitBtn.onClick.RemoveAllListeners();
            if (showSubmit && _onSubmit != null)
            {
                submitBtn.onClick.AddListener(() => _onSubmit?.Invoke(_questInstanceId));
            }
        }
    }

    // ============ Formatting ============

    private string FormatRequirements(QuestInstance quest)
    {
        if (quest.progress == null || quest.progress.Count == 0)
            return "";

        var sb = new System.Text.StringBuilder();
        foreach (var entry in quest.progress)
        {
            if (sb.Length > 0) sb.Append("\n");

            string typeName = entry.type switch
            {
                QuestRequirementType.DeliverResource => "Deliver",
                QuestRequirementType.ClearThreatZone => "Clear Threat",
                QuestRequirementType.DefendOutpost => "Defend",
                QuestRequirementType.ReachLocation => "Reach",
                QuestRequirementType.DefeatEnemy => "Defeat",
                _ => "Complete"
            };

            string resName = string.IsNullOrEmpty(entry.resourceId)
                ? ""
                : $" {entry.resourceId}";

            sb.Append($"{typeName}{resName} {entry.currentAmount}/{entry.requiredAmount}");
        }
        return sb.ToString();
    }

    private string FormatReward(QuestReward reward)
    {
        if (reward == null) return "No Reward";

        var parts = new System.Collections.Generic.List<string>();

        if (reward.moneyReward > 0)
            parts.Add($"${reward.moneyReward:F0}");

        if (reward.reputationReward != 0)
        {
            string sign = reward.reputationReward > 0 ? "+" : "";
            parts.Add($"Rep {sign}{reward.reputationReward}");
        }

        if (reward.resourceRewards != null)
        {
            foreach (var res in reward.resourceRewards)
            {
                parts.Add($"{res.resourceId}x{res.amount}");
            }
        }

        return parts.Count > 0 ? string.Join(" | ", parts) : "No Reward";
    }

    // ============ Label Helpers ============

    private static string GetQuestTypeLabel(QuestType type)
    {
        return type switch
        {
            QuestType.Delivery => "Delivery",
            QuestType.ThreatClear => "Clear Threat",
            QuestType.CombatDefend => "Defend",
            QuestType.CombatRaid => "Raid",
            QuestType.Exploration => "Explore",
            _ => "Unknown"
        };
    }

    private static Color GetQuestTypeColor(QuestType type)
    {
        return type switch
        {
            QuestType.Delivery => new Color(0.4f, 0.8f, 1f),
            QuestType.ThreatClear => new Color(1f, 0.5f, 0.3f),
            QuestType.CombatDefend => new Color(1f, 0.8f, 0.2f),
            QuestType.CombatRaid => new Color(1f, 0.3f, 0.3f),
            QuestType.Exploration => new Color(0.5f, 1f, 0.5f),
            _ => Color.white
        };
    }

    private static string GetDifficultyLabel(QuestDifficulty difficulty)
    {
        return difficulty switch
        {
            QuestDifficulty.Easy => "Easy",
            QuestDifficulty.Normal => "Normal",
            QuestDifficulty.Hard => "Hard",
            QuestDifficulty.Elite => "Elite",
            _ => ""
        };
    }

    private static Color GetDifficultyColor(QuestDifficulty difficulty)
    {
        return difficulty switch
        {
            QuestDifficulty.Easy => Color.green,
            QuestDifficulty.Normal => Color.white,
            QuestDifficulty.Hard => new Color(1f, 0.6f, 0f),
            QuestDifficulty.Elite => new Color(1f, 0.2f, 0.2f),
            _ => Color.white
        };
    }

    private static Color GetStateColor(QuestState state)
    {
        return state switch
        {
            QuestState.Available => new Color(0.5f, 0.5f, 0.5f),
            QuestState.Accepted => new Color(0.3f, 0.6f, 1f),
            QuestState.InProgress => new Color(1f, 0.8f, 0.2f),
            QuestState.ReadyToSubmit => new Color(0.2f, 1f, 0.2f),
            QuestState.Completed => new Color(0.2f, 0.8f, 0.2f),
            QuestState.Failed => new Color(1f, 0.2f, 0.2f),
            QuestState.Expired => new Color(0.4f, 0.4f, 0.4f),
            _ => Color.gray
        };
    }
}
