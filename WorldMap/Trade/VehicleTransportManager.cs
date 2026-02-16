using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 载具运输管理器 - 管理多趟运输任务的调度和载具占用/归还
/// 与 TradeManager 协作：本管理器负责"多趟"调度逻辑，TradeManager 负责单趟运输执行
/// </summary>
public class VehicleTransportManager : MonoBehaviour
{
    public static VehicleTransportManager Instance { get; private set; }

    [Header("References")]
    public TradeManager tradeManager;
    public NPCManager npcManager;

    // 运行时数据
    private List<MultiTripTransportJob> _activeJobs = new();
    private Dictionary<string, int> _vehiclesInUse = new(); // baseId -> 在途载具数

    // orderId -> jobId 映射，用于在运输完成时找到关联的 job
    private Dictionary<string, string> _orderToJobMap = new();

    // 事件
    public event Action<MultiTripTransportJob> OnJobCreated;
    public event Action<MultiTripTransportJob> OnJobCompleted;
    public event Action<MultiTripTransportJob, int, int> OnTripCompleted; // job, tripIndex, totalTrips

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

        // 取消事件订阅
        if (tradeManager != null)
            tradeManager.OnTransportCompleted -= HandleTransportCompleted;
    }

    private void Start()
    {
        if (tradeManager == null)
            tradeManager = FindObjectOfType<TradeManager>();
        if (npcManager == null)
            npcManager = FindObjectOfType<NPCManager>();

        // 订阅运输完成事件
        if (tradeManager != null)
            tradeManager.OnTransportCompleted += HandleTransportCompleted;
    }

    private void Update()
    {
        // 对每个活跃的 job，尝试派遣更多趟次（并行调度）
        for (int i = _activeJobs.Count - 1; i >= 0; i--)
        {
            var job = _activeJobs[i];

            if (job.state == MultiTripState.Cancelled)
            {
                ReturnAllVehicles(job);
                _activeJobs.RemoveAt(i);
                continue;
            }

            if (job.state != MultiTripState.Active) continue;

            // 并行调度：只要有空闲载具且有剩余趟次，就继续派遣
            while (job.CanDispatchMore)
            {
                if (!TryDispatchNextTrip(job))
                    break;
            }

            // 检查完成状态
            if (job.IsComplete)
            {
                job.state = MultiTripState.Completed;
                OnJobCompleted?.Invoke(job);

                Debug.Log($"[VehicleTransportManager] Job '{job.jobId.Substring(0, 8)}' completed: " +
                          $"delivered={job.totalDelivered}, lost={job.totalLost}, trips={job.tripsCompleted}");
            }
        }

        // 清理已完成的 jobs
        _activeJobs.RemoveAll(j => j.state == MultiTripState.Completed || j.state == MultiTripState.Cancelled);
    }

    // ============ Job Management ============

    /// <summary>
    /// 创建多趟运输任务
    /// </summary>
    /// <param name="routeId">贸易路线ID</param>
    /// <param name="cargo">总货物列表</param>
    /// <param name="vehicleCount">分配的载具数量</param>
    /// <param name="questInstanceId">关联的任务ID（可选）</param>
    public MultiTripTransportJob CreateTransportJob(string routeId, List<TransportCargoItem> cargo,
                                                      int vehicleCount, string questInstanceId = null)
    {
        if (tradeManager == null) return null;

        var route = tradeManager.GetTradeRoute(routeId);
        if (route == null)
        {
            Debug.LogWarning("[VehicleTransportManager] Route not found: " + routeId);
            return null;
        }

        // 检查载具可用性
        int available = GetAvailableVehicles(route.sourceBaseId);
        int actualVehicles = Mathf.Min(vehicleCount, available);

        if (actualVehicles <= 0)
        {
            Debug.LogWarning("[VehicleTransportManager] No vehicles available in base: " + route.sourceBaseId);
            return null;
        }

        // 占用载具
        ConsumeVehicles(route.sourceBaseId, route.vehicleResourceId, actualVehicles);

        var job = new MultiTripTransportJob(
            routeId, route.sourceBaseId, route.targetOutpostId,
            cargo, route.vehicleCapacityPerUnit, actualVehicles
        );
        job.vehicleResourceId = route.vehicleResourceId;
        job.questInstanceId = questInstanceId;

        _activeJobs.Add(job);
        OnJobCreated?.Invoke(job);

        Debug.Log($"[VehicleTransportManager] Created job: {job.totalAmount} units, " +
                  $"{job.totalTripsNeeded} trips, {actualVehicles} vehicles, " +
                  $"quest={questInstanceId ?? "none"}");

        return job;
    }

    /// <summary>
    /// 尝试为 job 派遣下一趟运输
    /// </summary>
    private bool TryDispatchNextTrip(MultiTripTransportJob job)
    {
        if (tradeManager == null || !job.CanDispatchMore) return false;

        var route = tradeManager.GetTradeRoute(job.routeId);
        if (route == null || !route.isValid) return false;

        // 构建本趟货物
        var tripCargo = job.BuildNextTripCargo();
        if (tripCargo.Count == 0) return false;

        // 临时修改路线的货物列表来调用 TradeManager 的派遣
        // 保存原始 cargo
        var originalCargo = route.cargoItems;
        var originalAutoLoop = route.autoLoop;

        // 设置本趟货物
        route.cargoItems = new List<TradeCargoItem>();
        foreach (var c in tripCargo)
        {
            route.cargoItems.Add(new TradeCargoItem(c.resourceId, c.amount, c.direction));
        }
        route.autoLoop = false; // 防止自动循环干扰

        // 派遣
        bool success = tradeManager.TryDispatchTransport(route);

        // 恢复原始 cargo
        route.cargoItems = originalCargo;
        route.autoLoop = originalAutoLoop;

        if (success)
        {
            // 获取刚派遣的订单ID
            var orders = tradeManager.GetOrdersByRoute(route.routeId);
            string latestOrderId = null;
            if (orders.Count > 0)
                latestOrderId = orders[orders.Count - 1].orderId;

            job.RecordDispatch(latestOrderId);

            if (latestOrderId != null)
                _orderToJobMap[latestOrderId] = job.jobId;

            Debug.Log($"[VehicleTransportManager] Trip {job.tripsDispatched}/{job.totalTripsNeeded} dispatched " +
                      $"for job '{job.jobId.Substring(0, 8)}'");
        }

        return success;
    }

    // ============ Transport Completion Handler ============

    /// <summary>
    /// 处理运输订单完成（由 TradeManager 事件触发）
    /// </summary>
    private void HandleTransportCompleted(WorldMapTransportOrder order)
    {
        if (order == null) return;

        // 查找关联的 job
        if (!_orderToJobMap.TryGetValue(order.orderId, out string jobId))
            return;

        _orderToJobMap.Remove(order.orderId);

        var job = _activeJobs.Find(j => j.jobId == jobId);
        if (job == null) return;

        // 记录趟次完成，载具返回
        job.RecordTripCompleted(order.orderId, order.totalDelivered, order.totalLost);

        OnTripCompleted?.Invoke(job, job.tripsCompleted, job.totalTripsNeeded);

        Debug.Log($"[VehicleTransportManager] Trip completed for job '{job.jobId.Substring(0, 8)}': " +
                  $"{job.tripsCompleted}/{job.totalTripsNeeded}, delivered={order.totalDelivered}");
    }

    // ============ Vehicle Management ============

    /// <summary>
    /// 获取基地可用载具数量（库存中的载具 - 在途的载具）
    /// </summary>
    public int GetAvailableVehicles(string baseId)
    {
        int inInventory = GetVehiclesInInventory(baseId);
        _vehiclesInUse.TryGetValue(baseId, out int inUse);
        return Mathf.Max(0, inInventory - inUse);
    }

    /// <summary>
    /// 获取基地在途载具数量
    /// </summary>
    public int GetVehiclesInTransit(string baseId)
    {
        _vehiclesInUse.TryGetValue(baseId, out int count);
        return count;
    }

    /// <summary>
    /// 从基地库存中消耗载具（标记为在用）
    /// </summary>
    private void ConsumeVehicles(string baseId, string vehicleResId, int count)
    {
        if (!_vehiclesInUse.ContainsKey(baseId))
            _vehiclesInUse[baseId] = 0;
        _vehiclesInUse[baseId] += count;

        // 从基地存档的资源中扣除
        if (BaseManager.Instance != null)
        {
            var baseSave = BaseManager.Instance.GetBaseSaveData(baseId);
            if (baseSave != null)
            {
                baseSave.TryConsumeResource(vehicleResId, count);
                BaseManager.Instance.UpdateBaseSaveData(baseSave);
            }
        }

        Debug.Log($"[VehicleTransportManager] Consumed {count} vehicles from base '{baseId}' " +
                  $"(total in use: {_vehiclesInUse[baseId]})");
    }

    /// <summary>
    /// 归还载具到基地（运输完成后）
    /// </summary>
    private void ReturnVehicles(string baseId, string vehicleResId, int count)
    {
        if (_vehiclesInUse.ContainsKey(baseId))
        {
            _vehiclesInUse[baseId] = Mathf.Max(0, _vehiclesInUse[baseId] - count);
        }

        // 归还到基地存档的资源中
        if (BaseManager.Instance != null)
        {
            var baseSave = BaseManager.Instance.GetBaseSaveData(baseId);
            if (baseSave != null)
            {
                baseSave.AddResource(vehicleResId, count);
                BaseManager.Instance.UpdateBaseSaveData(baseSave);
            }
        }

        Debug.Log($"[VehicleTransportManager] Returned {count} vehicles to base '{baseId}'");
    }

    /// <summary>
    /// 归还 job 的所有在途载具
    /// </summary>
    private void ReturnAllVehicles(MultiTripTransportJob job)
    {
        if (job.assignedVehicles > 0)
        {
            ReturnVehicles(job.sourceBaseId, job.vehicleResourceId, job.assignedVehicles);
        }
    }

    /// <summary>
    /// 获取基地库存中的载具数量
    /// </summary>
    private int GetVehiclesInInventory(string baseId)
    {
        if (BaseManager.Instance == null) return 0;
        var baseSave = BaseManager.Instance.GetBaseSaveData(baseId);
        if (baseSave == null) return 0;

        // 默认检查 vehicle_truck，也可以扩展为检查所有载具类型
        return Mathf.FloorToInt(baseSave.GetResourceAmount("vehicle_truck"));
    }

    // ============ Queries ============

    /// <summary>
    /// 获取所有活跃的运输任务
    /// </summary>
    public List<MultiTripTransportJob> GetActiveJobs()
    {
        return new List<MultiTripTransportJob>(_activeJobs);
    }

    /// <summary>
    /// 获取指定基地的活跃运输任务
    /// </summary>
    public List<MultiTripTransportJob> GetJobsByBase(string baseId)
    {
        var result = new List<MultiTripTransportJob>();
        foreach (var job in _activeJobs)
        {
            if (job.sourceBaseId == baseId)
                result.Add(job);
        }
        return result;
    }

    /// <summary>
    /// 获取指定任务关联的运输 job
    /// </summary>
    public MultiTripTransportJob GetJobByQuest(string questInstanceId)
    {
        if (string.IsNullOrEmpty(questInstanceId)) return null;
        return _activeJobs.Find(j => j.questInstanceId == questInstanceId);
    }

    /// <summary>
    /// 取消运输任务
    /// </summary>
    public void CancelJob(string jobId)
    {
        var job = _activeJobs.Find(j => j.jobId == jobId);
        if (job != null)
        {
            job.state = MultiTripState.Cancelled;
            Debug.Log($"[VehicleTransportManager] Job '{jobId.Substring(0, 8)}' cancelled");
        }
    }

    // ============ Save/Load ============

    public VehicleTransportSaveData GetSaveData()
    {
        var data = new VehicleTransportSaveData();
        foreach (var job in _activeJobs)
        {
            if (job.state == MultiTripState.Active || job.state == MultiTripState.Pending)
                data.activeJobs.Add(job);
        }

        foreach (var kvp in _vehiclesInUse)
            data.vehiclesInUse.Add(new VehicleUsageEntry(kvp.Key, kvp.Value));

        return data;
    }

    public void LoadFromSaveData(VehicleTransportSaveData data)
    {
        _activeJobs.Clear();
        _vehiclesInUse.Clear();
        _orderToJobMap.Clear();

        if (data == null) return;

        foreach (var job in data.activeJobs)
        {
            _activeJobs.Add(job);

            // 重建 order -> job 映射
            foreach (var orderId in job.activeOrderIds)
            {
                _orderToJobMap[orderId] = job.jobId;
            }
        }

        foreach (var entry in data.vehiclesInUse)
            _vehiclesInUse[entry.baseId] = entry.count;

        Debug.Log($"[VehicleTransportManager] Loaded {_activeJobs.Count} active jobs");
    }
}

/// <summary>
/// 载具运输存档数据
/// </summary>
[Serializable]
public class VehicleTransportSaveData
{
    public List<MultiTripTransportJob> activeJobs = new();
    public List<VehicleUsageEntry> vehiclesInUse = new();
}

[Serializable]
public class VehicleUsageEntry
{
    public string baseId;
    public int count;

    public VehicleUsageEntry() { }

    public VehicleUsageEntry(string baseId, int count)
    {
        this.baseId = baseId;
        this.count = count;
    }
}
