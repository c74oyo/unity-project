using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 任务管理器 - 管理所有NPC任务的生成、接受、进度追踪和完成
/// </summary>
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Header("References")]
    public NPCManager npcManager;
    public VehicleTransportManager vehicleTransportManager;

    [Header("Settings")]
    [Tooltip("任务公告板刷新间隔（秒）")]
    public float questRefreshInterval = 300f;

    [Tooltip("每个据点最大可展示任务数")]
    public int maxQuestsPerOutpost = 5;

    [Header("Quest Templates")]
    [Tooltip("全局任务模板列表（如果势力没有配置自己的模板，使用这里的）")]
    public List<QuestTemplate> globalTemplates = new();

    // 运行时数据
    private Dictionary<string, QuestInstance> _activeQuests = new();     // instanceId -> quest（玩家已接受）
    private Dictionary<string, List<QuestInstance>> _questBoards = new(); // outpostId -> available quests
    private List<CompletedQuestRecord> _completedRecords = new();

    // 战斗处理接口（预留）
    private ICombatQuestHandler _combatHandler;

    // 事件
    public event Action<QuestInstance> OnQuestAccepted;
    public event Action<QuestInstance> OnQuestCompleted;
    public event Action<QuestInstance> OnQuestFailed;
    public event Action<string> OnQuestBoardRefreshed; // outpostId

    // ============ Lifecycle ============

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (vehicleTransportManager != null)
        {
            vehicleTransportManager.OnTripCompleted -= HandleTripCompleted;
            vehicleTransportManager.OnJobCompleted -= HandleJobCompleted;
        }
    }

    private void Start()
    {
        if (npcManager == null)
            npcManager = FindObjectOfType<NPCManager>();
        if (vehicleTransportManager == null)
            vehicleTransportManager = FindObjectOfType<VehicleTransportManager>();

        // 订阅运输完成事件
        if (vehicleTransportManager != null)
        {
            vehicleTransportManager.OnTripCompleted += HandleTripCompleted;
            vehicleTransportManager.OnJobCompleted += HandleJobCompleted;
        }
    }

    private void Update()
    {
        // 检查过期任务
        CheckExpiredQuests();
    }

    // ============ Quest Board ============

    /// <summary>
    /// 获取据点公告板上的可接受任务
    /// </summary>
    public List<QuestInstance> GetAvailableQuests(string outpostId)
    {
        if (_questBoards.TryGetValue(outpostId, out var quests))
            return new List<QuestInstance>(quests);
        return new List<QuestInstance>();
    }

    /// <summary>
    /// 获取玩家已接受的所有活跃任务
    /// </summary>
    public List<QuestInstance> GetActiveQuests()
    {
        return new List<QuestInstance>(_activeQuests.Values);
    }

    /// <summary>
    /// 获取指定势力的活跃任务
    /// </summary>
    public List<QuestInstance> GetActiveQuestsByFaction(string factionId)
    {
        var result = new List<QuestInstance>();
        foreach (var quest in _activeQuests.Values)
        {
            if (quest.factionId == factionId)
                result.Add(quest);
        }
        return result;
    }

    /// <summary>
    /// 为据点生成任务
    /// </summary>
    public void GenerateQuestsForOutpost(string outpostId)
    {
        if (npcManager == null) return;

        var outpost = npcManager.GetOutpost(outpostId);
        if (outpost == null) return;

        var faction = npcManager.GetFaction(outpost.factionId);
        if (faction == null) return;

        int reputation = npcManager.GetReputation(outpost.factionId);

        // 清除旧的可接受任务
        if (!_questBoards.ContainsKey(outpostId))
            _questBoards[outpostId] = new List<QuestInstance>();

        var board = _questBoards[outpostId];
        board.Clear();

        // 获取模板列表（优先使用势力配置，fallback 到全局模板）
        var templates = globalTemplates;

        int questCount = Mathf.Min(faction.maxActiveQuests, maxQuestsPerOutpost);

        for (int i = 0; i < questCount && templates.Count > 0; i++)
        {
            // 随机选择一个符合声望要求的模板
            var validTemplates = new List<QuestTemplate>();
            foreach (var t in templates)
            {
                if (t != null && reputation >= t.minReputationToAppear)
                    validTemplates.Add(t);
            }

            if (validTemplates.Count == 0) break;

            var template = validTemplates[UnityEngine.Random.Range(0, validTemplates.Count)];
            var quest = template.GenerateQuest(outpost.factionId, outpostId, outpost.displayName);

            if (quest != null)
            {
                board.Add(quest);
            }
        }

        // 更新据点的任务ID列表
        outpost.questBoardIds.Clear();
        foreach (var q in board)
            outpost.questBoardIds.Add(q.instanceId);

        outpost.lastQuestRefreshTime = Time.time;

        OnQuestBoardRefreshed?.Invoke(outpostId);
        Debug.Log($"[QuestManager] Generated {board.Count} quests for outpost '{outpost.displayName}'");
    }

    /// <summary>
    /// 刷新所有据点的任务公告板
    /// </summary>
    public void RefreshAllQuestBoards()
    {
        if (npcManager == null) return;

        var outposts = npcManager.GetAllOutposts();
        foreach (var outpost in outposts)
        {
            if (outpost.isDiscovered && outpost.canTrade)
            {
                GenerateQuestsForOutpost(outpost.outpostId);
            }
        }
    }

    // ============ Quest Acceptance ============

    /// <summary>
    /// 接受任务
    /// </summary>
    public bool AcceptQuest(string questInstanceId, string baseId)
    {
        // 在公告板中查找
        QuestInstance quest = null;
        string fromOutpostId = null;

        foreach (var kvp in _questBoards)
        {
            foreach (var q in kvp.Value)
            {
                if (q.instanceId == questInstanceId)
                {
                    quest = q;
                    fromOutpostId = kvp.Key;
                    break;
                }
            }
            if (quest != null) break;
        }

        if (quest == null)
        {
            Debug.LogWarning("[QuestManager] Quest not found on any board: " + questInstanceId);
            return false;
        }

        // 检查声望
        if (npcManager != null)
        {
            int reputation = npcManager.GetReputation(quest.factionId);
            if (reputation < quest.minReputationToAccept)
            {
                Debug.LogWarning($"[QuestManager] Reputation too low to accept quest: " +
                                 $"need {quest.minReputationToAccept}, have {reputation}");
                return false;
            }
        }

        // 从公告板移除
        if (fromOutpostId != null && _questBoards.ContainsKey(fromOutpostId))
            _questBoards[fromOutpostId].Remove(quest);

        // 设置为已接受
        quest.state = QuestState.Accepted;
        quest.acceptedTime = Time.time;
        quest.acceptedByBaseId = baseId;

        // 如果有时限，设置截止时间
        if (quest.deadlineTime > 0)
            quest.deadlineTime = Time.time + (quest.deadlineTime - quest.generatedTime);

        _activeQuests[quest.instanceId] = quest;
        OnQuestAccepted?.Invoke(quest);

        Debug.Log($"[QuestManager] Quest accepted: '{quest.displayName}' by base '{baseId}'");
        return true;
    }

    // ============ Quest Submission ============

    /// <summary>
    /// 提交完成的任务，发放奖励
    /// </summary>
    public bool TrySubmitQuest(string questInstanceId)
    {
        if (!_activeQuests.TryGetValue(questInstanceId, out var quest))
        {
            Debug.LogWarning("[QuestManager] Quest not found: " + questInstanceId);
            return false;
        }

        if (!quest.IsAllRequirementsMet)
        {
            Debug.LogWarning("[QuestManager] Quest requirements not met: " + quest.displayName);
            return false;
        }

        // 发放奖励
        GrantRewards(quest);

        // 标记完成
        quest.state = QuestState.Completed;
        _activeQuests.Remove(questInstanceId);

        // 记录完成
        RecordCompletion(quest);

        OnQuestCompleted?.Invoke(quest);
        Debug.Log($"[QuestManager] Quest completed: '{quest.displayName}', " +
                  $"money={quest.reward?.moneyReward}, rep={quest.reward?.reputationReward}");

        return true;
    }

    /// <summary>
    /// 发放任务奖励
    /// </summary>
    private void GrantRewards(QuestInstance quest)
    {
        if (quest.reward == null) return;

        // 金钱奖励
        if (quest.reward.moneyReward > 0 && BaseManager.Instance != null)
        {
            var baseSave = BaseManager.Instance.GetBaseSaveData(quest.acceptedByBaseId);
            if (baseSave != null)
            {
                baseSave.money += quest.reward.moneyReward;
                BaseManager.Instance.UpdateBaseSaveData(baseSave);
            }
        }

        // 声望奖励
        if (quest.reward.reputationReward != 0 && npcManager != null)
        {
            npcManager.ModifyReputation(quest.factionId, quest.reward.reputationReward);
        }

        // 资源奖励
        if (quest.reward.resourceRewards != null && BaseManager.Instance != null)
        {
            var baseSave = BaseManager.Instance.GetBaseSaveData(quest.acceptedByBaseId);
            if (baseSave != null)
            {
                foreach (var reward in quest.reward.resourceRewards)
                {
                    baseSave.AddResource(reward.resourceId, reward.amount);
                }
                BaseManager.Instance.UpdateBaseSaveData(baseSave);
            }
        }
    }

    /// <summary>
    /// 记录已完成的任务
    /// </summary>
    private void RecordCompletion(QuestInstance quest)
    {
        var existing = _completedRecords.Find(r => r.questId == quest.questId && r.factionId == quest.factionId);
        if (existing != null)
        {
            existing.timesCompleted++;
            existing.completedTime = Time.time;
        }
        else
        {
            _completedRecords.Add(new CompletedQuestRecord
            {
                questId = quest.questId,
                factionId = quest.factionId,
                completedTime = Time.time,
                timesCompleted = 1
            });
        }
    }

    // ============ Quest Progress Updates ============

    /// <summary>
    /// 更新运送任务进度（由 VehicleTransportManager 调用）
    /// </summary>
    public void UpdateDeliveryProgress(string questInstanceId, string resourceId, int deliveredAmount)
    {
        if (string.IsNullOrEmpty(questInstanceId)) return;

        if (_activeQuests.TryGetValue(questInstanceId, out var quest))
        {
            quest.UpdateDeliveryProgress(resourceId, deliveredAmount);
            Debug.Log($"[QuestManager] Quest '{quest.displayName}' progress: " +
                      $"{resourceId} +{deliveredAmount}, total progress={quest.TotalProgress:P0}");
        }
    }

    /// <summary>
    /// 更新战斗任务进度（预留，由战斗系统调用）
    /// </summary>
    public void UpdateCombatProgress(string questInstanceId, Vector2Int clearedCell, bool success)
    {
        if (string.IsNullOrEmpty(questInstanceId)) return;

        if (_activeQuests.TryGetValue(questInstanceId, out var quest))
        {
            quest.UpdateCombatProgress(clearedCell, success);
        }
    }

    // ============ Transport Event Handlers ============

    /// <summary>
    /// 处理每趟运输完成
    /// </summary>
    private void HandleTripCompleted(MultiTripTransportJob job, int tripIndex, int totalTrips)
    {
        if (string.IsNullOrEmpty(job.questInstanceId)) return;

        // 更新任务进度
        foreach (var cargo in job.totalCargo)
        {
            if (cargo.direction == TradeDirection.Export)
            {
                // 按已完成趟次比例计算本趟实际交付量
                int perTrip = Mathf.CeilToInt((float)cargo.amount / totalTrips);
                int delivered = Mathf.Min(perTrip, cargo.amount - (perTrip * (tripIndex - 1)));
                if (delivered > 0)
                {
                    UpdateDeliveryProgress(job.questInstanceId, cargo.resourceId, delivered);
                }
            }
        }
    }

    /// <summary>
    /// 处理整个运输任务完成
    /// </summary>
    private void HandleJobCompleted(MultiTripTransportJob job)
    {
        if (string.IsNullOrEmpty(job.questInstanceId)) return;

        // 确保最终进度正确
        if (_activeQuests.TryGetValue(job.questInstanceId, out var quest))
        {
            // 强制同步最终交付量
            foreach (var cargo in job.totalCargo)
            {
                if (cargo.direction == TradeDirection.Export)
                {
                    foreach (var entry in quest.progress)
                    {
                        if (entry.type == QuestRequirementType.DeliverResource
                            && entry.resourceId == cargo.resourceId)
                        {
                            // 设置为实际交付总量（扣除损失）
                            entry.currentAmount = Mathf.Min(entry.requiredAmount, job.totalDelivered);
                            break;
                        }
                    }
                }
            }

            if (quest.IsAllRequirementsMet)
                quest.state = QuestState.ReadyToSubmit;

            Debug.Log($"[QuestManager] Transport job completed for quest '{quest.displayName}', " +
                      $"delivered={job.totalDelivered}, progress={quest.TotalProgress:P0}");
        }
    }

    // ============ Expiration ============

    private void CheckExpiredQuests()
    {
        var expiredIds = new List<string>();

        foreach (var kvp in _activeQuests)
        {
            if (kvp.Value.IsExpired && kvp.Value.IsActive)
            {
                kvp.Value.state = QuestState.Failed;
                expiredIds.Add(kvp.Key);
                OnQuestFailed?.Invoke(kvp.Value);
                Debug.Log($"[QuestManager] Quest expired: '{kvp.Value.displayName}'");
            }
        }

        foreach (var id in expiredIds)
            _activeQuests.Remove(id);

        // 清理公告板上过期的任务
        foreach (var board in _questBoards.Values)
        {
            board.RemoveAll(q => q.IsExpired);
        }
    }

    // ============ Combat Interface ============

    /// <summary>
    /// 注册战斗处理器（预留，战斗系统实现后调用）
    /// </summary>
    public void RegisterCombatHandler(ICombatQuestHandler handler)
    {
        _combatHandler = handler;
        Debug.Log("[QuestManager] Combat handler registered");
    }

    /// <summary>
    /// 尝试开始战斗任务（预留）
    /// </summary>
    public bool TryStartCombatQuest(string questInstanceId)
    {
        if (_combatHandler == null)
        {
            Debug.LogWarning("[QuestManager] No combat handler registered");
            return false;
        }

        if (!_activeQuests.TryGetValue(questInstanceId, out var quest))
            return false;

        if (quest.questType != QuestType.ThreatClear
            && quest.questType != QuestType.CombatDefend
            && quest.questType != QuestType.CombatRaid)
            return false;

        foreach (var entry in quest.progress)
        {
            if (entry.type == QuestRequirementType.ClearThreatZone && !entry.isComplete)
            {
                if (_combatHandler.CanStartCombat(entry.targetCell, 1))
                {
                    _combatHandler.StartCombat(questInstanceId, entry.targetCell, 1, (success) =>
                    {
                        UpdateCombatProgress(questInstanceId, entry.targetCell, success);
                    });
                    return true;
                }
            }
        }

        return false;
    }

    // ============ Save/Load ============

    public QuestManagerSaveData GetSaveData()
    {
        var data = new QuestManagerSaveData();

        foreach (var quest in _activeQuests.Values)
            data.activeQuests.Add(quest.Clone());

        foreach (var kvp in _questBoards)
        {
            foreach (var quest in kvp.Value)
                data.availableQuests.Add(quest.Clone());
        }

        data.completedQuests = new List<CompletedQuestRecord>(_completedRecords);

        return data;
    }

    public void LoadFromSaveData(QuestManagerSaveData data)
    {
        _activeQuests.Clear();
        _questBoards.Clear();
        _completedRecords.Clear();

        if (data == null) return;

        foreach (var quest in data.activeQuests)
        {
            _activeQuests[quest.instanceId] = quest;
        }

        // 按据点分组恢复公告板
        foreach (var quest in data.availableQuests)
        {
            if (!_questBoards.ContainsKey(quest.outpostId))
                _questBoards[quest.outpostId] = new List<QuestInstance>();
            _questBoards[quest.outpostId].Add(quest);
        }

        if (data.completedQuests != null)
            _completedRecords = new List<CompletedQuestRecord>(data.completedQuests);

        Debug.Log($"[QuestManager] Loaded {_activeQuests.Count} active quests, " +
                  $"{data.availableQuests.Count} available quests");
    }

    // ============ Debug ============

#if UNITY_EDITOR
    [ContextMenu("Debug: Refresh All Quest Boards")]
    private void DebugRefreshAll()
    {
        if (!Application.isPlaying) return;
        RefreshAllQuestBoards();
    }

    [ContextMenu("Debug: Print Active Quests")]
    private void DebugPrintActiveQuests()
    {
        string info = $"[QuestManager] Active Quests ({_activeQuests.Count}):\n";
        foreach (var quest in _activeQuests.Values)
        {
            info += $"  {quest.displayName}: {quest.state} ({quest.TotalProgress:P0})\n";
        }
        Debug.Log(info);
    }
#endif
}

/// <summary>
/// 任务管理器存档数据
/// </summary>
[Serializable]
public class QuestManagerSaveData
{
    public List<QuestInstance> activeQuests = new();
    public List<QuestInstance> availableQuests = new();
    public List<CompletedQuestRecord> completedQuests = new();
}

/// <summary>
/// 已完成任务记录
/// </summary>
[Serializable]
public class CompletedQuestRecord
{
    public string questId;
    public string factionId;
    public float completedTime;
    public int timesCompleted;
}
