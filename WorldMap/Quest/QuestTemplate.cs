using System;
using UnityEngine;

/// <summary>
/// 任务模板 - ScriptableObject，用于动态生成任务
/// 配置在 NPCFaction 上，系统根据模板随机生成具体任务实例
/// </summary>
[CreateAssetMenu(menuName = "Game/World Map/Quest Template", fileName = "QuestTemplate_")]
public class QuestTemplate : ScriptableObject
{
    [Header("基本信息")]
    public string templateId;

    [Tooltip("任务名称格式，支持占位符：{0}=资源名, {1}=据点名, {2}=数量")]
    public string displayNameFormat = "运送{0}到{1}";

    [Tooltip("任务描述格式")]
    [TextArea(2, 4)]
    public string descriptionFormat = "{1}需要{2}单位的{0}，请尽快运送。";

    [Header("类型")]
    public QuestType questType = QuestType.Delivery;
    public QuestDifficulty difficulty = QuestDifficulty.Normal;

    [Header("运输任务参数")]
    [Tooltip("可能的资源ID列表（随机选择一个）")]
    public string[] possibleResourceIds;

    [Tooltip("最小数量")]
    public int minAmount = 50;

    [Tooltip("最大数量")]
    public int maxAmount = 200;

    [Header("战斗任务参数（预留）")]
    [Tooltip("最低威胁等级")]
    public int minThreatLevel = 1;

    [Tooltip("最高威胁等级")]
    public int maxThreatLevel = 5;

    [Header("奖励")]
    [Tooltip("基础金钱奖励")]
    public float baseMoneyReward = 100f;

    [Tooltip("基础声望奖励")]
    public int baseReputationReward = 5;

    [Tooltip("奖励随数量缩放系数（每单位额外奖励）")]
    public float rewardScaleByAmount = 0.5f;

    [Tooltip("额外资源奖励（可选）")]
    public ResourceReward[] bonusResourceRewards;

    [Header("条件")]
    [Tooltip("该模板出现所需的最低声望")]
    public int minReputationToAppear = -100;

    [Tooltip("是否可重复完成")]
    public bool isRepeatable = true;

    [Tooltip("基础时限（秒），0=无限")]
    public float baseDurationSeconds = 600f;

    // ============ Generation ============

    /// <summary>
    /// 根据模板生成具体的任务实例
    /// </summary>
    public QuestInstance GenerateQuest(string factionId, string outpostId, string outpostName)
    {
        var quest = new QuestInstance
        {
            questId = templateId,
            factionId = factionId,
            outpostId = outpostId,
            questType = questType,
            difficulty = difficulty,
            isRepeatable = isRepeatable,
            minReputationToAccept = minReputationToAppear
        };

        // 根据任务类型生成需求
        switch (questType)
        {
            case QuestType.Delivery:
                GenerateDeliveryQuest(quest, outpostName);
                break;
            case QuestType.ThreatClear:
                GenerateThreatClearQuest(quest, outpostName);
                break;
            default:
                // 预留类型 - 生成占位数据
                GeneratePlaceholderQuest(quest, outpostName);
                break;
        }

        // 设置时限
        if (baseDurationSeconds > 0)
            quest.deadlineTime = Time.time + baseDurationSeconds;

        return quest;
    }

    private void GenerateDeliveryQuest(QuestInstance quest, string outpostName)
    {
        if (possibleResourceIds == null || possibleResourceIds.Length == 0)
        {
            Debug.LogWarning($"[QuestTemplate] Template '{templateId}' has no resource IDs configured");
            return;
        }

        // 随机选择资源
        string resourceId = possibleResourceIds[UnityEngine.Random.Range(0, possibleResourceIds.Length)];
        int amount = UnityEngine.Random.Range(minAmount, maxAmount + 1);

        // 格式化名称和描述
        quest.displayName = string.Format(displayNameFormat, resourceId, outpostName, amount);
        quest.description = string.Format(descriptionFormat, resourceId, outpostName, amount);

        // 创建进度条目
        quest.progress.Add(new QuestProgressEntry(
            QuestRequirementType.DeliverResource, resourceId, amount
        ));

        // 计算奖励
        quest.reward = new QuestReward
        {
            moneyReward = baseMoneyReward + amount * rewardScaleByAmount,
            reputationReward = baseReputationReward,
            resourceRewards = bonusResourceRewards
        };
    }

    private void GenerateThreatClearQuest(QuestInstance quest, string outpostName)
    {
        int threatLevel = UnityEngine.Random.Range(minThreatLevel, maxThreatLevel + 1);

        quest.displayName = string.Format(displayNameFormat, threatLevel, outpostName);
        quest.description = string.Format(descriptionFormat, threatLevel, outpostName);

        // 创建进度条目（战斗接口预留，currentAmount 需要战斗系统来更新）
        quest.progress.Add(new QuestProgressEntry
        {
            type = QuestRequirementType.ClearThreatZone,
            requiredAmount = 1, // 清除1个区域
            currentAmount = 0
        });

        quest.reward = new QuestReward
        {
            moneyReward = baseMoneyReward * threatLevel,
            reputationReward = baseReputationReward * threatLevel,
            resourceRewards = bonusResourceRewards
        };
    }

    private void GeneratePlaceholderQuest(QuestInstance quest, string outpostName)
    {
        quest.displayName = $"[Placeholder] {questType} - {outpostName}";
        quest.description = "This quest type is not yet implemented.";

        quest.progress.Add(new QuestProgressEntry
        {
            type = QuestRequirementType.DefeatEnemy,
            requiredAmount = 1,
            currentAmount = 0
        });

        quest.reward = new QuestReward
        {
            moneyReward = baseMoneyReward,
            reputationReward = baseReputationReward
        };
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(templateId))
            templateId = name;
    }
}
