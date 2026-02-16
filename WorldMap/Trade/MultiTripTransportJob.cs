using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 多趟运输任务 - 管理一批货物通过多次载具往返来完成运输
/// 例如：1000单位货物，每车100单位，分配3辆车并行运输
/// </summary>
[Serializable]
public class MultiTripTransportJob
{
    [Header("Identity")]
    public string jobId;
    public string routeId;
    public string sourceBaseId;
    public string targetOutpostId;

    [Header("Cargo")]
    [Tooltip("总货物列表")]
    public List<TransportCargoItem> totalCargo = new();

    [Tooltip("总货物量（所有货物的 amount 之和）")]
    public int totalAmount;

    [Header("Vehicle")]
    [Tooltip("载具资源ID")]
    public string vehicleResourceId = "vehicle_truck";

    [Tooltip("每车容量")]
    public int vehicleCapacity = 100;

    [Tooltip("分配的载具总数")]
    public int assignedVehicles;

    [Header("Progress")]
    [Tooltip("总共需要的趟次")]
    public int totalTripsNeeded;

    [Tooltip("已派遣的趟次")]
    public int tripsDispatched;

    [Tooltip("已完成的趟次")]
    public int tripsCompleted;

    [Tooltip("总共已交付的货物量")]
    public int totalDelivered;

    [Tooltip("总共损失的货物量")]
    public int totalLost;

    [Tooltip("当前在途的载具数")]
    public int vehiclesInTransit;

    [Header("State")]
    public MultiTripState state = MultiTripState.Pending;
    public float createdTime;

    [Header("Linked Orders")]
    [Tooltip("当前活跃的运输订单ID列表")]
    public List<string> activeOrderIds = new();

    [Header("Quest Link")]
    [Tooltip("关联的任务实例ID（如果该运输是为了完成任务）")]
    public string questInstanceId;

    // ============ Constructor ============

    public MultiTripTransportJob()
    {
        jobId = Guid.NewGuid().ToString();
        createdTime = Time.time;
    }

    public MultiTripTransportJob(string routeId, string sourceBaseId, string targetOutpostId,
                                   List<TransportCargoItem> cargo, int vehicleCapacity, int vehicleCount)
    {
        this.jobId = Guid.NewGuid().ToString();
        this.routeId = routeId;
        this.sourceBaseId = sourceBaseId;
        this.targetOutpostId = targetOutpostId;
        this.vehicleCapacity = vehicleCapacity;
        this.assignedVehicles = vehicleCount;
        this.createdTime = Time.time;
        this.state = MultiTripState.Active;

        // 复制货物列表
        if (cargo != null)
        {
            foreach (var c in cargo)
                totalCargo.Add(c.Clone());
        }

        // 计算总量和趟次
        totalAmount = 0;
        foreach (var c in totalCargo)
            totalAmount += c.amount;

        totalTripsNeeded = vehicleCapacity > 0
            ? Mathf.CeilToInt((float)totalAmount / vehicleCapacity)
            : 1;
    }

    // ============ Properties ============

    /// <summary>
    /// 任务是否完成（所有趟次已完成）
    /// </summary>
    public bool IsComplete => tripsCompleted >= totalTripsNeeded;

    /// <summary>
    /// 完成进度 (0~1)
    /// </summary>
    public float Progress => totalTripsNeeded > 0
        ? (float)tripsCompleted / totalTripsNeeded : 0f;

    /// <summary>
    /// 剩余需要派遣的趟次
    /// </summary>
    public int RemainingTrips => Mathf.Max(0, totalTripsNeeded - tripsDispatched);

    /// <summary>
    /// 剩余需要运输的货物量
    /// </summary>
    public int RemainingCargo => Mathf.Max(0, totalAmount - (tripsDispatched * vehicleCapacity));

    /// <summary>
    /// 是否可以派遣更多趟次（有剩余趟次且有空闲载具）
    /// </summary>
    public bool CanDispatchMore => state == MultiTripState.Active
                                    && tripsDispatched < totalTripsNeeded
                                    && vehiclesInTransit < assignedVehicles;

    // ============ Methods ============

    /// <summary>
    /// 计算下一趟应运输的货物量
    /// </summary>
    public int GetNextTripCargoAmount()
    {
        int remaining = RemainingCargo;
        return Mathf.Min(remaining, vehicleCapacity);
    }

    /// <summary>
    /// 构建下一趟的货物列表（按比例分配各种资源）
    /// </summary>
    public List<TransportCargoItem> BuildNextTripCargo()
    {
        var tripCargo = new List<TransportCargoItem>();
        int tripCapacity = GetNextTripCargoAmount();
        if (tripCapacity <= 0) return tripCargo;

        // 已发送的总量
        int alreadySent = tripsDispatched * vehicleCapacity;

        // 按货物列表顺序分配
        int allocated = 0;
        int accumulated = 0;

        foreach (var cargo in totalCargo)
        {
            if (allocated >= tripCapacity) break;

            // 该货物类型还剩多少需要发送
            int sentForThis = Mathf.Min(cargo.amount, Mathf.Max(0, alreadySent - accumulated));
            accumulated += cargo.amount;
            int remainingForThis = cargo.amount - sentForThis;

            if (remainingForThis <= 0) continue;

            int toSend = Mathf.Min(remainingForThis, tripCapacity - allocated);
            if (toSend > 0)
            {
                tripCargo.Add(new TransportCargoItem(cargo.resourceId, toSend, cargo.direction));
                allocated += toSend;
            }
        }

        return tripCargo;
    }

    /// <summary>
    /// 记录一趟已派遣
    /// </summary>
    public void RecordDispatch(string orderId)
    {
        tripsDispatched++;
        vehiclesInTransit++;
        if (orderId != null)
            activeOrderIds.Add(orderId);
    }

    /// <summary>
    /// 记录一趟已完成（载具返回）
    /// </summary>
    public void RecordTripCompleted(string orderId, int delivered, int lost)
    {
        tripsCompleted++;
        vehiclesInTransit = Mathf.Max(0, vehiclesInTransit - 1);
        totalDelivered += delivered;
        totalLost += lost;

        if (orderId != null)
            activeOrderIds.Remove(orderId);

        // 检查是否全部完成
        if (IsComplete)
        {
            state = MultiTripState.Completed;
        }
    }
}

/// <summary>
/// 多趟运输任务状态
/// </summary>
public enum MultiTripState
{
    Pending,       // 等待载具
    Active,        // 运输中（有趟次在派遣或在途）
    Paused,        // 暂停（玩家手动或载具不足）
    Completed,     // 全部趟次完成
    Cancelled      // 已取消
}
