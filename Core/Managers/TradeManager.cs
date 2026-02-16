using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 贸易管理器 - 管理所有贸易路线和实时运输
/// Phase 2: 从回合制改为实时运输系统
/// </summary>
public class TradeManager : MonoBehaviour
{
    public static TradeManager Instance { get; private set; }

    [Header("References")]
    public WorldMapManager worldMapManager;
    public RoadNetwork roadNetwork;
    public NPCManager npcManager;

    [Header("Settings")]
    [Tooltip("运输过程中的基础货物损失率（阶段三启用）")]
    [Range(0f, 0.2f)]
    public float baseCargoLossRate = 0.02f;

    [Tooltip("每格道路的基础移动时间（秒）")]
    public float baseTimePerRoadCell = 1f;

    // 运行时数据
    private Dictionary<string, TradeRoute> _tradeRoutes = new();
    private List<WorldMapTransportOrder> _activeOrders = new();

    // 事件
    public event Action<TradeRoute> OnTradeRouteCreated;
    public event Action<TradeRoute> OnTradeRouteRemoved;
    public event Action<WorldMapTransportOrder> OnTransportDispatched;
    public event Action<WorldMapTransportOrder> OnTransportCompleted;
    public event Action<WorldMapTransportOrder> OnTransportReturning;

    // ============ Lifecycle ============

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (worldMapManager == null)
            worldMapManager = FindObjectOfType<WorldMapManager>();
        if (roadNetwork == null)
            roadNetwork = FindObjectOfType<RoadNetwork>();
        if (npcManager == null)
            npcManager = FindObjectOfType<NPCManager>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        // 从 BaseManager 加载待恢复的贸易数据（存档恢复）
        if (BaseManager.Instance != null)
            BaseManager.Instance.FlushPendingTradeData();
    }

    private void Update()
    {
        float currentTime = Time.time;

        // 1. 检查自动循环路线，派遣新运输
        foreach (var route in _tradeRoutes.Values)
        {
            if (route.CanDispatchRealtime(currentTime))
            {
                TryDispatchTransport(route);
            }
        }

        // 2. 更新所有活跃运输订单
        for (int i = _activeOrders.Count - 1; i >= 0; i--)
        {
            var order = _activeOrders[i];
            if (!order.IsActive) continue;

            // 推进时间
            order.elapsedTime += Time.deltaTime;

            // 状态转换
            if (order.state == WorldMapTransportOrder.OrderState.Dispatched)
            {
                order.state = WorldMapTransportOrder.OrderState.InTransit;
            }

            // 到达目的地
            if (order.state == WorldMapTransportOrder.OrderState.InTransit && order.HasArrived)
            {
                CompleteTransport(order);
            }

            // 返程到达（载具返回出发地）
            if (order.state == WorldMapTransportOrder.OrderState.Returning && order.HasArrived)
            {
                order.state = WorldMapTransportOrder.OrderState.Completed;
                OnTransportCompleted?.Invoke(order);

                Debug.Log($"[TradeManager] Transport returned: {order.orderId.Substring(0, 8)}");
            }
        }

        // 3. 清理已完成的订单
        _activeOrders.RemoveAll(o => o.IsFinished);
    }

    // ============ Trade Route Management ============

    /// <summary>
    /// 创建贸易路线
    /// </summary>
    public TradeRoute CreateTradeRoute(string sourceBaseId, Vector2Int sourceCell,
                                        string targetOutpostId, Vector2Int targetCell,
                                        Vector2Int sourceSize = default)
    {
        // 检查是否已有相同路线
        foreach (var existing in _tradeRoutes.Values)
        {
            if (existing.sourceBaseId == sourceBaseId && existing.targetOutpostId == targetOutpostId)
            {
                Debug.LogWarning("[TradeManager] Trade route already exists between these points");
                return existing;
            }
        }

        var route = new TradeRoute(sourceBaseId, sourceCell, targetOutpostId, targetCell);

        // 获取据点信息以获取占地大小
        Vector2Int targetSize = Vector2Int.one;
        if (npcManager != null)
        {
            var outpost = npcManager.GetOutpost(targetOutpostId);
            if (outpost != null)
                targetSize = outpost.size;
        }

        // 查找道路路径
        if (roadNetwork != null)
        {
            if (sourceSize == default)
                sourceSize = new Vector2Int(3, 3);

            var path = roadNetwork.FindPathBetweenAreasFromAnchor(sourceCell, sourceSize, targetCell, targetSize);
            if (path != null && path.Count > 0)
            {
                route.roadPath = path;
                route.isValid = true;
                Debug.Log($"[TradeManager] Found path: {path[0]} -> {path[path.Count - 1]}, {path.Count} cells");
            }
            else
            {
                Debug.LogWarning($"[TradeManager] No road path found between {sourceCell} and {targetCell}");
                route.isValid = false;
            }
        }

        _tradeRoutes[route.routeId] = route;
        OnTradeRouteCreated?.Invoke(route);

        Debug.Log($"[TradeManager] Created trade route: {route.displayName} (Valid: {route.isValid})");
        return route;
    }

    /// <summary>
    /// 移除贸易路线（同时取消该路线的所有活跃运输）
    /// </summary>
    public bool RemoveTradeRoute(string routeId)
    {
        if (_tradeRoutes.TryGetValue(routeId, out var route))
        {
            // 取消该路线的所有活跃订单
            foreach (var order in _activeOrders)
            {
                if (order.routeId == routeId && order.IsActive)
                    order.state = WorldMapTransportOrder.OrderState.Cancelled;
            }

            _tradeRoutes.Remove(routeId);
            OnTradeRouteRemoved?.Invoke(route);
            Debug.Log($"[TradeManager] Removed trade route: {route.displayName}");
            return true;
        }
        return false;
    }

    public TradeRoute GetTradeRoute(string routeId)
    {
        _tradeRoutes.TryGetValue(routeId, out var route);
        return route;
    }

    public List<TradeRoute> GetTradeRoutesByBase(string baseId)
    {
        var result = new List<TradeRoute>();
        foreach (var route in _tradeRoutes.Values)
        {
            if (route.sourceBaseId == baseId)
                result.Add(route);
        }
        return result;
    }

    public List<TradeRoute> GetAllTradeRoutes()
    {
        return new List<TradeRoute>(_tradeRoutes.Values);
    }

    /// <summary>
    /// 刷新路线的道路路径
    /// </summary>
    public void RefreshRoutePath(string routeId)
    {
        var route = GetTradeRoute(routeId);
        if (route == null || roadNetwork == null) return;

        Vector2Int targetSize = Vector2Int.one;
        if (npcManager != null)
        {
            var outpost = npcManager.GetOutpost(route.targetOutpostId);
            if (outpost != null)
                targetSize = outpost.size;
        }

        var path = roadNetwork.FindPathToArea(route.sourceCell, route.targetCell, targetSize);
        if (path != null && path.Count > 0)
        {
            route.roadPath = path;
            route.isValid = true;
        }
        else
        {
            if (route.roadPath != null)
                route.roadPath.Clear();
            else
                route.roadPath = new List<Vector2Int>();
            route.isValid = false;
        }
    }

    // ============ Real-time Transport ============

    /// <summary>
    /// 尝试派遣一次运输（自动循环调用或手动调用）
    /// </summary>
    public bool TryDispatchTransport(TradeRoute route)
    {
        if (route == null || !route.isValid || route.cargoItems.Count == 0)
            return false;

        // 检查路径是否可通行（道路是否完全损坏）
        if (roadNetwork != null && !roadNetwork.IsPathPassable(route.roadPath))
        {
            Debug.LogWarning($"[TradeManager] Route '{route.displayName}' is blocked by damaged roads!");
            return false;
        }

        // 检查源基地是否有DockYard
        if (!HasDockYard(route.sourceBaseId))
        {
            Debug.LogWarning($"[TradeManager] Base '{route.sourceBaseId}' has no DockYard, cannot dispatch");
            return false;
        }

        // 获取基地存档数据
        if (BaseManager.Instance == null) return false;
        var baseSave = BaseManager.Instance.GetBaseSaveData(route.sourceBaseId);
        if (baseSave == null) return false;

        // 构建本次运输的货物列表（出口：从基地扣除资源）
        var transportCargo = new List<TransportCargoItem>();
        var exportCargo = route.GetExportCargo();

        foreach (var cargo in exportCargo)
        {
            float available = baseSave.GetResourceAmount(cargo.resourceId);
            int toSend = Mathf.Min(cargo.amount, Mathf.FloorToInt(available));

            if (toSend <= 0)
            {
                Debug.Log($"[TradeManager] Not enough '{cargo.resourceId}' in base (have {available}, need {cargo.amount})");
                continue;
            }

            // 从基地库存扣除
            baseSave.TryConsumeResource(cargo.resourceId, toSend);
            transportCargo.Add(new TransportCargoItem(cargo.resourceId, toSend, TradeDirection.Export));
        }

        // 进口货物：从NPC据点扣除
        var importCargo = route.GetImportCargo();
        if (npcManager != null)
        {
            var outpost = npcManager.GetOutpost(route.targetOutpostId);
            if (outpost != null)
            {
                foreach (var cargo in importCargo)
                {
                    var stock = outpost.GetSellStock(cargo.resourceId);
                    if (stock == null || stock.amount <= 0) continue;

                    int toSend = Mathf.Min(cargo.amount, stock.amount);

                    // 从NPC据点扣除
                    outpost.TryDeductSellStock(cargo.resourceId, toSend);
                    transportCargo.Add(new TransportCargoItem(cargo.resourceId, toSend, TradeDirection.Import));
                }
            }
        }

        if (transportCargo.Count == 0)
        {
            Debug.Log($"[TradeManager] No cargo to transport on route '{route.displayName}'");
            return false;
        }

        // 计算运输时间
        float travelTime = CalculateTravelTime(route);

        // 创建运输订单
        var order = new WorldMapTransportOrder(
            route.routeId,
            route.sourceBaseId,
            route.targetOutpostId,
            transportCargo,
            travelTime
        );

        _activeOrders.Add(order);
        route.lastDispatchTime = Time.time;
        route.totalTrips++;

        // 保存基地数据变更
        BaseManager.Instance.UpdateBaseSaveData(baseSave);

        OnTransportDispatched?.Invoke(order);

        Debug.Log($"[TradeManager] Dispatched transport on '{route.displayName}': " +
                  $"{transportCargo.Count} cargo types, ETA {travelTime:F1}s");

        return true;
    }

    /// <summary>
    /// 完成运输（卡车到达目的地）
    /// </summary>
    private void CompleteTransport(WorldMapTransportOrder order)
    {
        order.state = WorldMapTransportOrder.OrderState.Delivering;

        if (BaseManager.Instance == null)
        {
            order.state = WorldMapTransportOrder.OrderState.Completed;
            return;
        }

        var baseSave = BaseManager.Instance.GetBaseSaveData(order.sourceBaseId);

        foreach (var cargo in order.cargoItems)
        {
            if (cargo.direction == TradeDirection.Export)
            {
                // 出口：货物交付给NPC据点，获得金钱
                DeliverExportCargo(order, cargo, baseSave);
            }
            else if (cargo.direction == TradeDirection.Import)
            {
                // 进口：货物交付给基地
                DeliverImportCargo(order, cargo, baseSave);
            }
        }

        // 保存基地数据变更
        if (baseSave != null)
            BaseManager.Instance.UpdateBaseSaveData(baseSave);

        // 应用道路磨损
        ApplyRoadWear(order);

        // 进入返程状态（载具需要返回出发地）
        // 返程只计纯路途时间（空车返回，不含装卸时间）
        var returnRoute = GetTradeRoute(order.routeId);
        if (returnRoute != null)
        {
            order.travelDuration = CalculateRoadTravelTime(returnRoute);
        }
        order.state = WorldMapTransportOrder.OrderState.Returning;
        order.elapsedTime = 0f; // 重置计时器用于返程
        OnTransportReturning?.Invoke(order);

        Debug.Log($"[TradeManager] Transport delivering done, returning: delivered={order.totalDelivered}, lost={order.totalLost}");
    }

    /// <summary>
    /// 运输完成后应用道路磨损
    /// </summary>
    private void ApplyRoadWear(WorldMapTransportOrder order)
    {
        if (roadNetwork == null) return;

        var route = GetTradeRoute(order.routeId);
        if (route == null || route.roadPath == null) return;

        // 计算本次运输的总货物量
        int totalCargo = 0;
        foreach (var cargo in order.cargoItems)
        {
            totalCargo += cargo.amount;
        }

        // 应用磨损到路径上的所有道路
        bool allPassable = roadNetwork.ApplyTransportWearToPath(route.roadPath, totalCargo);

        if (!allPassable)
        {
            // 如果有道路损坏到不可通行，标记路线无效
            route.isValid = false;
            Debug.LogWarning($"[TradeManager] Route '{route.displayName}' became invalid due to road damage!");
        }
    }

    /// <summary>
    /// 交付出口货物（基地→NPC据点），获得金钱
    /// </summary>
    private void DeliverExportCargo(WorldMapTransportOrder order, TransportCargoItem cargo, BaseSaveData baseSave)
    {
        var route = GetTradeRoute(order.routeId);
        int lost = CalculateCargoLoss(cargo.amount, route);
        int delivered = cargo.amount - lost;
        order.totalDelivered += delivered;
        order.totalLost += lost;
        if (route != null) route.totalCargoLost += lost;

        if (npcManager == null) return;

        // 获取NPC据点所属势力
        var outpost = npcManager.GetOutpost(order.targetOutpostId);
        if (outpost == null) return;

        // 计算卖出价格
        float sellPrice = npcManager.CalculateSellPrice(outpost.factionId, cargo.resourceId, delivered);

        // 添加到NPC据点的买入库存
        outpost.AddBuyDemand(cargo.resourceId, delivered, sellPrice / Mathf.Max(1, delivered));

        // 金钱给基地
        if (baseSave != null && sellPrice > 0f)
        {
            baseSave.money += sellPrice;
            Debug.Log($"[TradeManager] Export: {delivered}x {cargo.resourceId} -> +${sellPrice:F1} to base");
        }

        // 增加好感度（每次成功交付+1）
        npcManager.ModifyReputation(outpost.factionId, 1);
    }

    /// <summary>
    /// 交付进口货物（NPC据点→基地）
    /// </summary>
    private void DeliverImportCargo(WorldMapTransportOrder order, TransportCargoItem cargo, BaseSaveData baseSave)
    {
        var route = GetTradeRoute(order.routeId);
        int lost = CalculateCargoLoss(cargo.amount, route);
        int delivered = cargo.amount - lost;
        order.totalDelivered += delivered;
        order.totalLost += lost;
        if (route != null) route.totalCargoLost += lost;

        if (baseSave == null) return;

        // 添加资源到基地
        baseSave.AddResource(cargo.resourceId, delivered);

        // 计算进口成本
        if (npcManager != null)
        {
            var outpost = npcManager.GetOutpost(order.targetOutpostId);
            if (outpost != null)
            {
                float buyCost = npcManager.CalculateBuyPrice(outpost.factionId, cargo.resourceId, delivered);
                if (buyCost > 0f)
                {
                    baseSave.money -= buyCost;
                    Debug.Log($"[TradeManager] Import: {delivered}x {cargo.resourceId} -> -${buyCost:F1} from base");
                }
            }
        }
    }

    // ============ Cargo Loss Calculation ============

    /// <summary>
    /// 计算运输途中的货物损失数量
    /// 损失 = 数量 * 基础损失率 * (1 - 道路保护率) + 道路损坏额外损失
    /// 道路保护率 = 路径上所有格子的 cargoLossProtection 平均值
    /// </summary>
    public int CalculateCargoLoss(int amount, TradeRoute route)
    {
        if (amount <= 0) return 0;

        // 基础损失
        float avgProtection = CalculateAverageRoadProtection(route);
        float baseLoss = 0f;
        if (baseCargoLossRate > 0f)
        {
            float lossRate = baseCargoLossRate * (1f - avgProtection);
            baseLoss = amount * lossRate;
        }

        // 道路损坏额外损失
        float damageLoss = CalculateDamageCargoLoss(amount, route);

        int totalLost = Mathf.FloorToInt(baseLoss + damageLoss);
        return Mathf.Clamp(totalLost, 0, amount - 1); // 至少交付1个
    }

    /// <summary>
    /// 计算因道路损坏导致的额外货物损失
    /// </summary>
    private float CalculateDamageCargoLoss(int amount, TradeRoute route)
    {
        if (route == null || route.roadPath == null || roadNetwork == null)
            return 0f;

        // 检查路径中是否有严重损坏的道路
        float maxDamageLossRate = 0f;
        foreach (var cell in route.roadPath)
        {
            var segment = roadNetwork.GetRoadAt(cell);
            if (segment != null)
            {
                float lossRate = segment.GetDamageCargoLossRate();
                if (lossRate > maxDamageLossRate)
                    maxDamageLossRate = lossRate;
            }
        }

        return amount * maxDamageLossRate;
    }

    /// <summary>
    /// 计算路线的平均道路保护率
    /// </summary>
    private float CalculateAverageRoadProtection(TradeRoute route)
    {
        if (route == null || route.roadPath == null || route.roadPath.Count == 0 || roadNetwork == null)
            return 0f;

        float totalProtection = 0f;
        int count = 0;
        foreach (var cell in route.roadPath)
        {
            var segment = roadNetwork.GetRoadAt(cell);
            if (segment != null)
            {
                var roadType = roadNetwork.GetRoadType(segment.roadTypeId);
                if (roadType != null)
                {
                    totalProtection += roadType.cargoLossProtection;
                    count++;
                }
            }
        }
        return count > 0 ? totalProtection / count : 0f;
    }

    // ============ DockYard Check ============

    /// <summary>
    /// 检查基地是否有DockYard建筑
    /// </summary>
    public bool HasDockYard(string baseId)
    {
        if (BaseManager.Instance == null) return false;
        var baseSave = BaseManager.Instance.GetBaseSaveData(baseId);
        return baseSave != null && baseSave.hasDockYard;
    }

    // ============ Travel Time Calculation ============

    /// <summary>
    /// 计算路线的运输时间（秒），考虑道路损坏的时间惩罚
    /// </summary>
    public float CalculateTravelTime(TradeRoute route)
    {
        float roadTime = CalculateRoadTravelTime(route);
        if (roadTime >= float.MaxValue) return float.MaxValue;

        // 加上装货时间（出发地码头装卸）
        float loadingTime = GetBaseLoadingTime(route.sourceBaseId);
        return roadTime + loadingTime;
    }

    /// <summary>
    /// 计算纯路途时间（不含装卸），用于返程计算
    /// </summary>
    public float CalculateRoadTravelTime(TradeRoute route)
    {
        if (route == null || route.roadPath == null || route.roadPath.Count == 0)
            return 30f; // 默认最小运输时间

        if (roadNetwork != null)
        {
            // 使用考虑道路损坏的运输时间计算
            float time = roadNetwork.CalculatePathTravelTimeWithDamage(route.roadPath, baseTimePerRoadCell);

            // 如果返回无穷大，表示道路不可通行
            if (time >= float.MaxValue)
                return float.MaxValue;

            return time;
        }

        return route.roadPath.Count * baseTimePerRoadCell;
    }

    /// <summary>
    /// 获取基地的装卸时间（来自 DockYard 的 EffectiveLoadingSeconds，已含卡片加速）
    /// </summary>
    public float GetBaseLoadingTime(string baseId)
    {
        if (BaseManager.Instance == null) return 0f;
        var baseSave = BaseManager.Instance.GetBaseSaveData(baseId);
        if (baseSave == null || !baseSave.hasDockYard) return 0f;
        return baseSave.dockLoadingSeconds;
    }

    /// <summary>
    /// 计算路线的基础运输时间（不考虑道路损坏）
    /// </summary>
    public float CalculateBaseTravelTime(TradeRoute route)
    {
        if (route == null || route.roadPath == null || route.roadPath.Count == 0)
            return 30f;

        if (roadNetwork != null)
            return roadNetwork.CalculatePathTravelTime(route.roadPath, baseTimePerRoadCell);

        return route.roadPath.Count * baseTimePerRoadCell;
    }

    // ============ Query ============

    /// <summary>
    /// 获取所有活跃运输订单
    /// </summary>
    public List<WorldMapTransportOrder> GetActiveOrders()
    {
        return new List<WorldMapTransportOrder>(_activeOrders);
    }

    /// <summary>
    /// 获取指定路线的活跃运输订单
    /// </summary>
    public List<WorldMapTransportOrder> GetOrdersByRoute(string routeId)
    {
        var result = new List<WorldMapTransportOrder>();
        foreach (var order in _activeOrders)
        {
            if (order.routeId == routeId && order.IsActive)
                result.Add(order);
        }
        return result;
    }

    /// <summary>
    /// 获取指定基地的活跃运输订单
    /// </summary>
    public List<WorldMapTransportOrder> GetOrdersByBase(string baseId)
    {
        var result = new List<WorldMapTransportOrder>();
        foreach (var order in _activeOrders)
        {
            if (order.sourceBaseId == baseId && order.IsActive)
                result.Add(order);
        }
        return result;
    }

    // ============ Utility ============

    public bool CanCreateTradeRoute(Vector2Int from, Vector2Int to, Vector2Int targetSize = default)
    {
        if (roadNetwork == null) return false;
        if (targetSize == default) targetSize = Vector2Int.one;
        return roadNetwork.IsConnectedToArea(from, to, targetSize);
    }

    public float EstimateRouteProfit(TradeRoute route)
    {
        if (npcManager == null || route == null) return 0f;

        var outpost = npcManager.GetOutpost(route.targetOutpostId);
        if (outpost == null) return 0f;

        float totalProfit = 0f;

        foreach (var cargo in route.GetExportCargo())
        {
            float price = npcManager.CalculateSellPrice(outpost.factionId, cargo.resourceId, cargo.amount);
            totalProfit += price;
        }

        foreach (var cargo in route.GetImportCargo())
        {
            float cost = npcManager.CalculateBuyPrice(outpost.factionId, cargo.resourceId, cargo.amount);
            totalProfit -= cost;
        }

        return totalProfit;
    }

    // ============ Save/Load ============

    public List<TradeRoute> GetAllRoutesForSave()
    {
        var result = new List<TradeRoute>();
        foreach (var route in _tradeRoutes.Values)
            result.Add(route.Clone());
        return result;
    }

    public List<WorldMapTransportOrder> GetActiveOrdersForSave()
    {
        var result = new List<WorldMapTransportOrder>();
        foreach (var order in _activeOrders)
        {
            if (order.IsActive)
                result.Add(order);
        }
        return result;
    }

    public void LoadFromSaveData(List<TradeRoute> savedRoutes, List<WorldMapTransportOrder> savedOrders = null)
    {
        _tradeRoutes.Clear();
        if (savedRoutes != null)
        {
            foreach (var route in savedRoutes)
                _tradeRoutes[route.routeId] = route;
        }

        _activeOrders.Clear();
        if (savedOrders != null)
        {
            foreach (var order in savedOrders)
            {
                if (order.IsActive)
                    _activeOrders.Add(order);
            }
        }

        Debug.Log($"[TradeManager] Loaded {_tradeRoutes.Count} routes, {_activeOrders.Count} active orders");
    }

    // ============ Debug ============
#if UNITY_EDITOR
    [ContextMenu("Debug: Print All Trade Routes")]
    private void DebugPrintRoutes()
    {
        string info = $"[TradeManager] Trade Routes ({_tradeRoutes.Count}):\n";
        foreach (var route in _tradeRoutes.Values)
        {
            info += $"  {route.displayName}: {route.sourceCell} -> {route.targetCell} " +
                    $"(Valid: {route.isValid}, Active: {route.isActive}, AutoLoop: {route.autoLoop}, " +
                    $"Cargo: {route.cargoItems.Count}, Trips: {route.totalTrips})\n";
        }
        Debug.Log(info);
    }

    [ContextMenu("Debug: Print Active Orders")]
    private void DebugPrintOrders()
    {
        string info = $"[TradeManager] Active Orders ({_activeOrders.Count}):\n";
        foreach (var order in _activeOrders)
        {
            info += $"  {order.orderId.Substring(0, 8)}: {order.state} " +
                    $"Progress={order.Progress:P0} ({order.elapsedTime:F1}/{order.travelDuration:F1}s) " +
                    $"Cargo={order.cargoItems.Count}\n";
        }
        Debug.Log(info);
    }

    [ContextMenu("Debug: Force Dispatch All Routes")]
    private void DebugForceDispatchAll()
    {
        if (!Application.isPlaying) return;
        foreach (var route in _tradeRoutes.Values)
        {
            if (route.isValid && route.isActive)
                TryDispatchTransport(route);
        }
    }
#endif
}

/// <summary>
/// 贸易结果（保留用于事件回调兼容性）
/// </summary>
[Serializable]
public class TradeResult
{
    public string routeId;
    public int turn;
    public List<CargoResult> exportedItems = new();
    public List<CargoResult> importedItems = new();

    public int TotalExported
    {
        get
        {
            int total = 0;
            foreach (var item in exportedItems) total += item.delivered;
            return total;
        }
    }

    public int TotalImported
    {
        get
        {
            int total = 0;
            foreach (var item in importedItems) total += item.delivered;
            return total;
        }
    }

    public int TotalLost
    {
        get
        {
            int total = 0;
            foreach (var item in exportedItems) total += item.lost;
            foreach (var item in importedItems) total += item.lost;
            return total;
        }
    }
}

[Serializable]
public class CargoResult
{
    public string resourceId;
    public int sent;
    public int delivered;
    public int lost;

    public CargoResult() { }

    public CargoResult(string resourceId, int sent, int delivered, int lost)
    {
        this.resourceId = resourceId;
        this.sent = sent;
        this.delivered = delivered;
        this.lost = lost;
    }
}
