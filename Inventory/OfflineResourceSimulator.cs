using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 离线资源模拟器 - 在大地图时持续计算各基地的资源变化
/// 即使不在建造场景中，资源也会根据保存的流动数据继续累积/消耗
/// </summary>
public class OfflineResourceSimulator : MonoBehaviour
{
    public static OfflineResourceSimulator Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("模拟更新间隔（秒）")]
    public float simulationInterval = 1f;

    [Tooltip("是否启用离线计算")]
    public bool enableOfflineSimulation = true;

    [Tooltip("最大离线时间（分钟），超过这个时间不再计算")]
    public float maxOfflineMinutes = 1440f;  // 24小时

    [Header("Debug")]
    [SerializeField] private float _timeSinceLastUpdate = 0f;
    [SerializeField] private int _simulatedBasesCount = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // 游戏启动时，计算离线期间的资源变化
        ProcessOfflineTime();
    }

    private void Update()
    {
        if (!enableOfflineSimulation) return;

        _timeSinceLastUpdate += Time.deltaTime;

        if (_timeSinceLastUpdate >= simulationInterval)
        {
            SimulateAllBases(_timeSinceLastUpdate);
            _timeSinceLastUpdate = 0f;
        }
    }

    /// <summary>
    /// 处理离线期间的资源变化
    /// </summary>
    public void ProcessOfflineTime()
    {
        if (BaseManager.Instance == null) return;

        var allBases = BaseManager.Instance.AllBaseSaveData;
        foreach (var baseSave in allBases)
        {
            ProcessOfflineTimeForBase(baseSave);
        }

        // 保存更新后的数据
        BaseManager.Instance.SaveCurrentGame();
    }

    /// <summary>
    /// 处理单个基地的离线时间
    /// </summary>
    private void ProcessOfflineTimeForBase(BaseSaveData baseSave)
    {
        if (baseSave == null || baseSave.lastSaveTimeTicks == 0) return;
        if (baseSave.resourceFlows == null || baseSave.resourceFlows.Count == 0) return;

        // 计算离线时间（分钟）
        DateTime lastSave = new DateTime(baseSave.lastSaveTimeTicks);
        TimeSpan offlineTime = DateTime.Now - lastSave;
        float offlineMinutes = (float)offlineTime.TotalMinutes;

        // 限制最大离线时间
        offlineMinutes = Mathf.Min(offlineMinutes, maxOfflineMinutes);

        if (offlineMinutes <= 0) return;

        Debug.Log($"[OfflineSimulator] Base '{baseSave.baseName}' offline for {offlineMinutes:F1} minutes");

        // 应用资源变化
        ApplyResourceChanges(baseSave, offlineMinutes);

        // 更新时间戳
        baseSave.UpdateTimestamp();
    }

    /// <summary>
    /// 模拟所有基地的资源变化
    /// </summary>
    public void SimulateAllBases(float deltaSeconds)
    {
        if (BaseManager.Instance == null) return;

        float deltaMinutes = deltaSeconds / 60f;
        _simulatedBasesCount = 0;

        var allBases = BaseManager.Instance.AllBaseSaveData;
        foreach (var baseSave in allBases)
        {
            if (baseSave.resourceFlows == null || baseSave.resourceFlows.Count == 0)
                continue;

            ApplyResourceChanges(baseSave, deltaMinutes);
            _simulatedBasesCount++;
        }
    }

    /// <summary>
    /// 应用资源变化
    /// </summary>
    private void ApplyResourceChanges(BaseSaveData baseSave, float minutes)
    {
        if (baseSave.resources == null)
            baseSave.resources = new List<ResourceSaveData>();

        // 创建资源字典方便查找和更新
        var resourceDict = baseSave.resources.ToDictionary(r => r.resourceName, r => r);

        foreach (var flow in baseSave.resourceFlows)
        {
            // 计算净变化（每分钟生产 - 每分钟消耗）
            float netPerMinute = flow.productionPerMinute - flow.consumptionPerMinute;
            float change = netPerMinute * minutes;

            if (Mathf.Approximately(change, 0f)) continue;

            // 更新资源
            if (resourceDict.TryGetValue(flow.resourceName, out var resSave))
            {
                float newAmount = resSave.amount + change;

                // 限制在 0 到容量之间
                newAmount = Mathf.Max(0f, newAmount);
                if (baseSave.baseCapacity > 0)
                    newAmount = Mathf.Min(newAmount, baseSave.baseCapacity);

                resSave.amount = newAmount;
            }
            else if (change > 0)
            {
                // 新资源（只有生产时才添加）
                float newAmount = Mathf.Min(change, baseSave.baseCapacity > 0 ? baseSave.baseCapacity : float.MaxValue);
                baseSave.resources.Add(new ResourceSaveData(flow.resourceName, newAmount));
            }
        }

        // 不再移除数量为0的资源，这样玩家可以看到哪些资源被消耗完了
        // baseSave.resources.RemoveAll(r => r.amount <= 0);
    }

    /// <summary>
    /// 强制更新指定基地
    /// </summary>
    public void ForceUpdateBase(string baseId)
    {
        if (BaseManager.Instance == null) return;

        var baseSave = BaseManager.Instance.GetBaseSaveData(baseId);
        if (baseSave != null)
        {
            ProcessOfflineTimeForBase(baseSave);
        }
    }

    /// <summary>
    /// 获取基地的预计资源状态（用于UI显示）
    /// </summary>
    public Dictionary<string, float> GetProjectedResources(string baseId, float minutesAhead)
    {
        var result = new Dictionary<string, float>();

        if (BaseManager.Instance == null) return result;

        var baseSave = BaseManager.Instance.GetBaseSaveData(baseId);
        if (baseSave == null) return result;

        // 复制当前资源
        foreach (var res in baseSave.resources)
        {
            result[res.resourceName] = res.amount;
        }

        // 应用预测变化
        if (baseSave.resourceFlows != null)
        {
            foreach (var flow in baseSave.resourceFlows)
            {
                float netPerMinute = flow.productionPerMinute - flow.consumptionPerMinute;
                float change = netPerMinute * minutesAhead;

                if (!result.ContainsKey(flow.resourceName))
                    result[flow.resourceName] = 0f;

                result[flow.resourceName] = Mathf.Max(0f, result[flow.resourceName] + change);
            }
        }

        return result;
    }

    // ============ Debug ============
#if UNITY_EDITOR
    [ContextMenu("Debug: Process Offline Time")]
    private void DebugProcessOfflineTime()
    {
        ProcessOfflineTime();
    }

    [ContextMenu("Debug: Simulate 1 Hour")]
    private void DebugSimulate1Hour()
    {
        SimulateAllBases(3600f);  // 1小时 = 3600秒
    }
#endif
}
