using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 任务状态
/// </summary>
public enum QuestState
{
    Available,      // NPC公告板可见，可接受
    Accepted,       // 玩家已接受
    InProgress,     // 进行中（有关联的运输等）
    ReadyToSubmit,  // 条件已满足，等待交付
    Completed,      // 已完成并领取奖励
    Failed,         // 超时或放弃
    Expired         // 过期（未接受就过期）
}

/// <summary>
/// 任务实例 - 一个已生成的具体任务
/// </summary>
[Serializable]
public class QuestInstance
{
    [Header("Identity")]
    public string instanceId;
    public string questId;

    [Header("关联")]
    public string factionId;
    public string outpostId;
    public string acceptedByBaseId;

    [Header("内容")]
    public string displayName;
    [TextArea(2, 4)]
    public string description;
    public QuestType questType;
    public QuestDifficulty difficulty;

    [Header("需求与进度")]
    public List<QuestProgressEntry> progress = new();

    [Header("奖励")]
    public QuestReward reward;

    [Header("状态")]
    public QuestState state = QuestState.Available;
    public float generatedTime;
    public float acceptedTime;
    public float deadlineTime;     // 0 = 无限

    [Header("重复")]
    public bool isRepeatable;
    public int minReputationToAccept;

    // ============ Constructor ============

    public QuestInstance()
    {
        instanceId = Guid.NewGuid().ToString();
        generatedTime = Time.time;
    }

    // ============ Properties ============

    /// <summary>
    /// 任务是否已过期（超过截止时间）
    /// </summary>
    public bool IsExpired => deadlineTime > 0 && Time.time > deadlineTime;

    /// <summary>
    /// 所有需求条件是否已满足
    /// </summary>
    public bool IsAllRequirementsMet
    {
        get
        {
            if (progress == null || progress.Count == 0) return false;
            foreach (var entry in progress)
            {
                if (!entry.isComplete) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// 任务是否处于活跃状态（可推进进度）
    /// </summary>
    public bool IsActive => state == QuestState.Accepted
                         || state == QuestState.InProgress;

    /// <summary>
    /// 任务是否已结束
    /// </summary>
    public bool IsFinished => state == QuestState.Completed
                           || state == QuestState.Failed
                           || state == QuestState.Expired;

    /// <summary>
    /// 总体完成进度 (0~1)
    /// </summary>
    public float TotalProgress
    {
        get
        {
            if (progress == null || progress.Count == 0) return 0f;
            float total = 0f;
            foreach (var entry in progress)
            {
                total += entry.Progress;
            }
            return total / progress.Count;
        }
    }

    /// <summary>
    /// 剩余时间（秒），-1 表示无限
    /// </summary>
    public float RemainingTime
    {
        get
        {
            if (deadlineTime <= 0) return -1f;
            return Mathf.Max(0f, deadlineTime - Time.time);
        }
    }

    /// <summary>
    /// 格式化的剩余时间
    /// </summary>
    public string FormattedRemainingTime
    {
        get
        {
            float t = RemainingTime;
            if (t < 0) return "No Limit";
            int min = Mathf.FloorToInt(t / 60f);
            int sec = Mathf.FloorToInt(t % 60f);
            return $"{min}:{sec:D2}";
        }
    }

    // ============ Progress Update ============

    /// <summary>
    /// 更新运送任务进度
    /// </summary>
    public void UpdateDeliveryProgress(string resourceId, int deliveredAmount)
    {
        if (!IsActive) return;

        foreach (var entry in progress)
        {
            if (entry.type == QuestRequirementType.DeliverResource
                && entry.resourceId == resourceId
                && !entry.isComplete)
            {
                entry.currentAmount += deliveredAmount;
                if (entry.currentAmount > entry.requiredAmount)
                    entry.currentAmount = entry.requiredAmount;
                break;
            }
        }

        // 检查是否所有条件都满足
        if (IsAllRequirementsMet)
        {
            state = QuestState.ReadyToSubmit;
        }
        else if (state == QuestState.Accepted)
        {
            state = QuestState.InProgress;
        }
    }

    /// <summary>
    /// 更新战斗任务进度（预留接口）
    /// </summary>
    public void UpdateCombatProgress(Vector2Int clearedCell, bool success)
    {
        if (!IsActive) return;

        foreach (var entry in progress)
        {
            if (entry.type == QuestRequirementType.ClearThreatZone
                && entry.targetCell == clearedCell
                && !entry.isComplete)
            {
                if (success)
                    entry.currentAmount = entry.requiredAmount;
                break;
            }
        }

        if (IsAllRequirementsMet)
            state = QuestState.ReadyToSubmit;
        else if (state == QuestState.Accepted)
            state = QuestState.InProgress;
    }

    // ============ Clone ============

    public QuestInstance Clone()
    {
        var clone = new QuestInstance
        {
            instanceId = this.instanceId,
            questId = this.questId,
            factionId = this.factionId,
            outpostId = this.outpostId,
            acceptedByBaseId = this.acceptedByBaseId,
            displayName = this.displayName,
            description = this.description,
            questType = this.questType,
            difficulty = this.difficulty,
            state = this.state,
            generatedTime = this.generatedTime,
            acceptedTime = this.acceptedTime,
            deadlineTime = this.deadlineTime,
            isRepeatable = this.isRepeatable,
            minReputationToAccept = this.minReputationToAccept
        };

        if (this.reward != null)
            clone.reward = this.reward;

        foreach (var p in this.progress)
            clone.progress.Add(p.Clone());

        return clone;
    }
}

/// <summary>
/// 任务进度条目 - 追踪单个需求的完成情况
/// </summary>
[Serializable]
public class QuestProgressEntry
{
    public QuestRequirementType type;
    public string resourceId;
    public int requiredAmount;
    public int currentAmount;
    public Vector2Int targetCell; // 用于战斗/位置类任务

    /// <summary>
    /// 是否已完成
    /// </summary>
    public bool isComplete => currentAmount >= requiredAmount;

    /// <summary>
    /// 完成进度 (0~1)
    /// </summary>
    public float Progress => requiredAmount > 0
        ? Mathf.Clamp01((float)currentAmount / requiredAmount)
        : 0f;

    public QuestProgressEntry() { }

    public QuestProgressEntry(QuestRequirementType type, string resourceId, int requiredAmount)
    {
        this.type = type;
        this.resourceId = resourceId;
        this.requiredAmount = requiredAmount;
        this.currentAmount = 0;
    }

    public QuestProgressEntry Clone()
    {
        return new QuestProgressEntry
        {
            type = this.type,
            resourceId = this.resourceId,
            requiredAmount = this.requiredAmount,
            currentAmount = this.currentAmount,
            targetCell = this.targetCell
        };
    }
}
