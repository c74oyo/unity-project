using System;
using UnityEngine;

/// <summary>
/// 任务类型
/// </summary>
public enum QuestType
{
    Delivery,       // 运送指定资源到NPC据点
    ThreatClear,    // 清除世界地图威胁区域
    CombatDefend,   // 防御据点（预留）
    CombatRaid,     // 突袭指定地点（预留）
    Exploration     // 发现新据点（预留）
}

/// <summary>
/// 任务难度
/// </summary>
public enum QuestDifficulty
{
    Easy,
    Normal,
    Hard,
    Elite
}

/// <summary>
/// 任务需求类型
/// </summary>
public enum QuestRequirementType
{
    DeliverResource,    // 运送资源
    ClearThreatZone,    // 清除威胁区
    DefendOutpost,      // 防御据点（预留）
    ReachLocation,      // 到达指定位置（预留）
    DefeatEnemy         // 击败敌人（预留）
}

/// <summary>
/// 任务定义 - 描述一个任务的完整信息
/// </summary>
[Serializable]
public class QuestDefinition
{
    [Header("基本信息")]
    public string questId;
    public string displayName;

    [TextArea(2, 4)]
    public string description;

    [Header("关联")]
    [Tooltip("发布方势力ID")]
    public string factionId;

    [Tooltip("关联据点ID")]
    public string outpostId;

    [Header("类型")]
    public QuestType questType;
    public QuestDifficulty difficulty;

    [Header("需求")]
    public QuestRequirement[] requirements;

    [Header("奖励")]
    public QuestReward reward;

    [Header("条件")]
    [Tooltip("接受任务所需最低声望")]
    public int minReputationToAccept;

    [Tooltip("任务时限（秒），0=无限")]
    public float timeLimitSeconds;

    [Tooltip("是否可重复完成")]
    public bool isRepeatable;

    public QuestDefinition()
    {
        questId = Guid.NewGuid().ToString();
    }
}

/// <summary>
/// 单个任务需求条目
/// </summary>
[Serializable]
public class QuestRequirement
{
    public QuestRequirementType type;

    [Header("运输任务参数")]
    [Tooltip("需要运输的资源ID")]
    public string resourceId;

    [Tooltip("需要运输的数量")]
    public int amount;

    [Header("战斗任务参数（预留）")]
    [Tooltip("目标位置")]
    public Vector2Int targetCell;

    [Tooltip("最低威胁等级")]
    public int threatLevelMin;

    public QuestRequirement() { }

    public QuestRequirement(QuestRequirementType type, string resourceId, int amount)
    {
        this.type = type;
        this.resourceId = resourceId;
        this.amount = amount;
    }
}

/// <summary>
/// 任务奖励
/// </summary>
[Serializable]
public class QuestReward
{
    [Tooltip("金钱奖励")]
    public float moneyReward;

    [Tooltip("声望奖励")]
    public int reputationReward;

    [Tooltip("资源奖励列表")]
    public ResourceReward[] resourceRewards;
}

/// <summary>
/// 资源奖励条目
/// </summary>
[Serializable]
public class ResourceReward
{
    public string resourceId;
    public int amount;

    public ResourceReward() { }

    public ResourceReward(string resourceId, int amount)
    {
        this.resourceId = resourceId;
        this.amount = amount;
    }
}
