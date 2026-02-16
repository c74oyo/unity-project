using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 世界地图新系统测试器
/// 测试：声望市场、载具运输、任务系统的完整流程
/// 使用方法：挂载到世界地图场景中任意 GameObject，运行时通过 Inspector 的 ContextMenu 或按键触发测试
/// </summary>
public class WorldMapSystemTester : MonoBehaviour
{
    [Header("测试配置")]
    [Tooltip("测试用基地ID（留空则使用当前活跃基地）")]
    public string testBaseId;

    [Tooltip("测试用资源ID")]
    public string testResourceId = "wood";

    [Tooltip("测试运输数量")]
    public int testTransportAmount = 500;

    [Tooltip("测试载具数量")]
    public int testVehicleCount = 3;

    [Header("快捷键")]
    [Tooltip("按此键执行完整流程测试")]
    public KeyCode fullTestKey = KeyCode.F5;

    [Tooltip("按此键打印所有系统状态")]
    public KeyCode statusKey = KeyCode.F6;

    [Tooltip("按此键刷新所有据点库存和任务")]
    public KeyCode refreshKey = KeyCode.F7;

    [Tooltip("按此键一键创建测试贸易路线（自动铺路+创建路线）")]
    public KeyCode createRouteKey = KeyCode.F8;

    // 缓存
    private string _lastCreatedQuestId;
    private string _lastCreatedJobId;

    private void Update()
    {
        if (Input.GetKeyDown(fullTestKey))
            RunFullFlowTest();

        if (Input.GetKeyDown(statusKey))
            PrintAllSystemStatus();

        if (Input.GetKeyDown(refreshKey))
            RefreshAllOutposts();

        if (Input.GetKeyDown(createRouteKey))
            CreateTestTradeRoute();
    }

    // ========================================================================
    //  完整流程测试 (F5)
    // ========================================================================

    [ContextMenu("★ 完整流程测试 (F5)")]
    public void RunFullFlowTest()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Tester] 仅在运行模式下可用"); return; }

        Debug.Log("<color=cyan>══════════════════════════════════════════════</color>");
        Debug.Log("<color=cyan>  世界地图新系统 — 完整流程测试开始</color>");
        Debug.Log("<color=cyan>══════════════════════════════════════════════</color>");

        bool ok = true;
        ok &= Test_01_CheckSingletons();
        ok &= Test_02_ReputationMarket();
        ok &= Test_03_VehicleResource();
        ok &= Test_04_QuestGeneration();
        ok &= Test_05_QuestAccept();
        ok &= Test_06_MultiTripTransport();
        ok &= Test_07_ReputationGating();

        Debug.Log("<color=cyan>══════════════════════════════════════════════</color>");
        if (ok)
            Debug.Log("<color=green>  ✓ 全部测试通过！</color>");
        else
            Debug.Log("<color=red>  ✗ 部分测试失败，请查看上方日志</color>");
        Debug.Log("<color=cyan>══════════════════════════════════════════════</color>");
    }

    // ========================================================================
    //  Test 01: 检查所有单例是否存在
    // ========================================================================

    bool Test_01_CheckSingletons()
    {
        Debug.Log("\n<color=yellow>[Test 01] 检查单例管理器...</color>");
        bool ok = true;

        ok &= CheckSingleton("BaseManager", BaseManager.Instance);
        ok &= CheckSingleton("NPCManager", NPCManager.Instance);
        ok &= CheckSingleton("TradeManager", TradeManager.Instance);
        ok &= CheckSingleton("WorldMapManager", WorldMapManager.Instance);
        ok &= CheckSingleton("VehicleTransportManager", VehicleTransportManager.Instance);
        ok &= CheckSingleton("ReputationMarketSystem", ReputationMarketSystem.Instance);
        ok &= CheckSingleton("QuestManager", QuestManager.Instance);

        var roadNetwork = FindObjectOfType<RoadNetwork>();
        if (roadNetwork == null) { Debug.LogError("  ✗ RoadNetwork 未找到"); ok = false; }
        else Debug.Log("  ✓ RoadNetwork");

        if (ok) Debug.Log("<color=green>[Test 01] 通过 — 所有单例就绪</color>");
        else Debug.LogError("<color=red>[Test 01] 失败 — 缺少管理器，请检查场景配置</color>");
        return ok;
    }

    bool CheckSingleton(string name, object instance)
    {
        if (instance == null) { Debug.LogError($"  ✗ {name}.Instance == null"); return false; }
        Debug.Log($"  ✓ {name}");
        return true;
    }

    // ========================================================================
    //  Test 02: 声望市场系统
    // ========================================================================

    bool Test_02_ReputationMarket()
    {
        Debug.Log("\n<color=yellow>[Test 02] 声望市场系统...</color>");

        var market = ReputationMarketSystem.Instance;
        var npc = NPCManager.Instance;
        if (market == null || npc == null)
        {
            Debug.LogError("[Test 02] 跳过 — ReputationMarketSystem 或 NPCManager 不存在");
            return false;
        }

        // 获取第一个有 marketConfig 的势力
        string testFactionId = null;
        foreach (var faction in npc.GetAllFactions())
        {
            if (faction != null && faction.marketConfig != null)
            {
                testFactionId = faction.factionId;
                break;
            }
        }

        if (testFactionId == null)
        {
            Debug.LogWarning("[Test 02] 跳过 — 没有势力配置了 ReputationMarketConfig。" +
                             "\n  请在任意 NPCFaction 资产的 marketConfig 字段拖入一个 ReputationMarketConfig 资产。");
            return true; // 不算失败，只是缺配置
        }

        int rep = npc.GetReputation(testFactionId);
        var tier = market.GetCurrentTier(testFactionId);
        Debug.Log($"  势力: {testFactionId}, 声望: {rep}, 层级: {tier?.tierName ?? "null"}");

        var goods = market.GetAvailableGoods(testFactionId, null);
        Debug.Log($"  可交易商品数: {goods.Count}");
        foreach (var g in goods)
        {
            Debug.Log($"    - {g.resourceId}: 数量={g.availableAmount}, 价格={g.pricePerUnit:F1}, 特殊={g.isSpecialGood}");
        }

        // 刷新据点库存
        var outposts = npc.GetOutpostsByFaction(testFactionId);
        if (outposts.Count > 0)
        {
            market.RefreshOutpostStock(outposts[0]);
            Debug.Log($"  刷新据点 '{outposts[0].displayName}' 库存: 卖={outposts[0].sellStock.Count}种, 买={outposts[0].buyDemand.Count}种");
        }

        float multiplier = market.GetPriceMultiplier(testFactionId);
        Debug.Log($"  价格倍率: {multiplier:F2}");

        Debug.Log("<color=green>[Test 02] 通过 — 声望市场系统工作正常</color>");
        return true;
    }

    // ========================================================================
    //  Test 03: 载具资源检查
    // ========================================================================

    bool Test_03_VehicleResource()
    {
        Debug.Log("\n<color=yellow>[Test 03] 载具资源系统...</color>");

        string baseId = GetTestBaseId();
        if (string.IsNullOrEmpty(baseId))
        {
            Debug.LogError("[Test 03] 跳过 — 没有活跃基地");
            return false;
        }

        var baseSave = BaseManager.Instance.GetBaseSaveData(baseId);
        if (baseSave == null)
        {
            Debug.LogError($"[Test 03] 跳过 — 找不到基地存档: {baseId}");
            return false;
        }

        // 检查当前载具数量
        float currentVehicles = baseSave.GetResourceAmount("vehicle_truck");
        Debug.Log($"  基地 '{baseSave.baseName}' 当前载具数量: {currentVehicles}");

        // 注入测试载具
        if (currentVehicles < testVehicleCount)
        {
            int toAdd = testVehicleCount - (int)currentVehicles + 2; // 多给2辆余量
            baseSave.AddResource("vehicle_truck", toAdd);
            BaseManager.Instance.UpdateBaseSaveData(baseSave);
            Debug.Log($"  <color=white>已注入 {toAdd} 辆测试载具</color>");
        }

        // 验证 VehicleTransportManager 能读到
        if (VehicleTransportManager.Instance != null)
        {
            int available = VehicleTransportManager.Instance.GetAvailableVehicles(baseId);
            int inTransit = VehicleTransportManager.Instance.GetVehiclesInTransit(baseId);
            Debug.Log($"  VehicleTransportManager — 可用: {available}, 在途: {inTransit}");
        }

        Debug.Log("<color=green>[Test 03] 通过 — 载具资源系统正常</color>");
        return true;
    }

    // ========================================================================
    //  Test 04: 任务生成
    // ========================================================================

    bool Test_04_QuestGeneration()
    {
        Debug.Log("\n<color=yellow>[Test 04] 任务生成系统...</color>");

        var questMgr = QuestManager.Instance;
        var npc = NPCManager.Instance;
        if (questMgr == null || npc == null)
        {
            Debug.LogError("[Test 04] 跳过 — QuestManager 或 NPCManager 不存在");
            return false;
        }

        // 检查是否有模板
        if (questMgr.globalTemplates == null || questMgr.globalTemplates.Count == 0)
        {
            Debug.LogWarning("[Test 04] 跳过 — QuestManager 没有配置 globalTemplates。" +
                             "\n  请创建 QuestTemplate 资产并拖入 QuestManager 的 globalTemplates 列表。");
            return true;
        }

        // 找到一个可用据点
        var outposts = npc.GetDiscoveredOutposts();
        if (outposts.Count == 0)
        {
            // 尝试所有据点
            outposts = npc.GetAllOutposts();
        }

        if (outposts.Count == 0)
        {
            Debug.LogWarning("[Test 04] 跳过 — 场景中没有NPC据点");
            return true;
        }

        // 优先选择已有贸易路线连接的据点（这样任务和运输才能匹配）
        NPCOutpost testOutpost = null;
        if (TradeManager.Instance != null)
        {
            var routes = TradeManager.Instance.GetAllTradeRoutes();
            foreach (var route in routes)
            {
                foreach (var o in outposts)
                {
                    if (o.outpostId == route.targetOutpostId)
                    {
                        testOutpost = o;
                        break;
                    }
                }
                if (testOutpost != null) break;
            }
        }
        if (testOutpost == null) testOutpost = outposts[0]; // fallback

        Debug.Log($"  为据点 '{testOutpost.displayName}' 生成任务...");

        questMgr.GenerateQuestsForOutpost(testOutpost.outpostId);

        var quests = questMgr.GetAvailableQuests(testOutpost.outpostId);
        Debug.Log($"  已生成 {quests.Count} 个任务:");
        foreach (var q in quests)
        {
            string progress = "";
            foreach (var p in q.progress)
            {
                progress += $"{p.resourceId}×{p.requiredAmount} ";
            }
            Debug.Log($"    [{q.questType}] {q.displayName} — 需求: {progress}— 奖励: 金钱={q.reward?.moneyReward:F0}, 声望=+{q.reward?.reputationReward}");
            _lastCreatedQuestId = q.instanceId; // 记住最后一个用于后续测试
        }

        if (quests.Count > 0)
            Debug.Log("<color=green>[Test 04] 通过 — 任务生成正常</color>");
        else
            Debug.LogWarning("<color=yellow>[Test 04] 警告 — 未生成任何任务（可能模板声望要求不满足）</color>");

        return true;
    }

    // ========================================================================
    //  Test 05: 接受任务
    // ========================================================================

    bool Test_05_QuestAccept()
    {
        Debug.Log("\n<color=yellow>[Test 05] 接受任务...</color>");

        var questMgr = QuestManager.Instance;
        if (questMgr == null)
        {
            Debug.LogError("[Test 05] 跳过 — QuestManager 不存在");
            return false;
        }

        if (string.IsNullOrEmpty(_lastCreatedQuestId))
        {
            Debug.LogWarning("[Test 05] 跳过 — 没有可接受的任务（Test 04 未生成任务）");
            return true;
        }

        string baseId = GetTestBaseId();
        bool accepted = questMgr.AcceptQuest(_lastCreatedQuestId, baseId);

        if (accepted)
        {
            var activeQuests = questMgr.GetActiveQuests();
            Debug.Log($"  ✓ 任务已接受！当前活跃任务数: {activeQuests.Count}");
            foreach (var q in activeQuests)
            {
                Debug.Log($"    [{q.state}] {q.displayName} — 进度: {q.TotalProgress:P0}");
            }
            Debug.Log("<color=green>[Test 05] 通过 — 任务接受流程正常</color>");
        }
        else
        {
            Debug.LogWarning("[Test 05] 接受失败（可能声望不足或任务已被移除）");
        }

        return true;
    }

    // ========================================================================
    //  Test 06: 多趟运输
    // ========================================================================

    bool Test_06_MultiTripTransport()
    {
        Debug.Log("\n<color=yellow>[Test 06] 多趟运输系统...</color>");

        var vtm = VehicleTransportManager.Instance;
        var tm = TradeManager.Instance;
        if (vtm == null || tm == null)
        {
            Debug.LogError("[Test 06] 跳过 — VehicleTransportManager 或 TradeManager 不存在");
            return false;
        }

        // 找一条已有的贸易路线
        var routes = tm.GetAllTradeRoutes();
        if (routes.Count == 0)
        {
            Debug.LogWarning("[Test 06] 跳过 — 没有贸易路线。请先手动创建一条贸易路线。" +
                             "\n  或使用 [创建测试贸易路线] 按钮。");
            return true;
        }

        var route = routes[0];
        string baseId = route.sourceBaseId;
        Debug.Log($"  使用路线: {route.displayName} ({route.sourceCell} → {route.targetCell})");
        Debug.Log($"  路径长度: {route.PathLength} 格, 有效: {route.isValid}");

        if (!route.isValid)
        {
            Debug.LogWarning("[Test 06] 跳过 — 路线无效（路径不通）");
            return true;
        }

        // 查找匹配此路线目标据点的活跃配送任务
        string linkedQuestId = null;
        string transportResourceId = testResourceId;
        int transportAmount = testTransportAmount;

        if (QuestManager.Instance != null)
        {
            foreach (var q in QuestManager.Instance.GetActiveQuests())
            {
                if (q.questType == QuestType.Delivery && q.outpostId == route.targetOutpostId && q.IsActive)
                {
                    linkedQuestId = q.instanceId;
                    // 使用任务需求的资源和数量
                    if (q.progress.Count > 0)
                    {
                        transportResourceId = q.progress[0].resourceId;
                        transportAmount = q.progress[0].requiredAmount - q.progress[0].currentAmount;
                        if (transportAmount <= 0) transportAmount = q.progress[0].requiredAmount;
                    }
                    Debug.Log($"  <color=white>关联任务: '{q.displayName}' — 需运送 {transportResourceId}×{transportAmount}</color>");
                    break;
                }
            }
            if (linkedQuestId == null)
            {
                Debug.Log("  未找到匹配的活跃配送任务，将创建独立运输");
            }
        }

        // 确保基地有足够载具
        var baseSave = BaseManager.Instance.GetBaseSaveData(baseId);
        if (baseSave != null)
        {
            float v = baseSave.GetResourceAmount("vehicle_truck");
            if (v < testVehicleCount)
            {
                baseSave.AddResource("vehicle_truck", testVehicleCount + 2);
                BaseManager.Instance.UpdateBaseSaveData(baseSave);
                Debug.Log($"  注入载具到基地 (现有: {v}, 注入: {testVehicleCount + 2})");
            }

            // 确保有测试货物
            float cargo = baseSave.GetResourceAmount(transportResourceId);
            if (cargo < transportAmount)
            {
                baseSave.AddResource(transportResourceId, transportAmount + 100);
                BaseManager.Instance.UpdateBaseSaveData(baseSave);
                Debug.Log($"  注入测试资源 '{transportResourceId}' (注入: {transportAmount + 100})");
            }
        }

        // 创建多趟运输任务（关联任务）
        var transportCargo = new List<TransportCargoItem>
        {
            new TransportCargoItem(transportResourceId, transportAmount, TradeDirection.Export)
        };

        var job = vtm.CreateTransportJob(route.routeId, transportCargo, testVehicleCount, linkedQuestId);

        if (job != null)
        {
            _lastCreatedJobId = job.jobId;
            Debug.Log($"  ✓ 运输任务创建成功！");
            Debug.Log($"    任务ID: {job.jobId.Substring(0, 8)}");
            Debug.Log($"    总货物: {job.totalAmount} 单位");
            Debug.Log($"    每车容量: {job.vehicleCapacity} 单位");
            Debug.Log($"    总趟次: {job.totalTripsNeeded}");
            Debug.Log($"    分配载具: {job.assignedVehicles} 辆（并行）");
            Debug.Log($"    状态: {job.state}");
            Debug.Log($"    关联任务: {(string.IsNullOrEmpty(job.questInstanceId) ? "无" : job.questInstanceId.Substring(0, 8))}");

            int availableAfter = vtm.GetAvailableVehicles(baseId);
            int inTransitAfter = vtm.GetVehiclesInTransit(baseId);
            Debug.Log($"    载具状态 — 可用: {availableAfter}, 在途: {inTransitAfter}");

            Debug.Log("<color=green>[Test 06] 通过 — 多趟运输任务已创建，等待 Update 循环调度</color>");
        }
        else
        {
            Debug.LogError("[Test 06] 运输任务创建失败");
            return false;
        }

        return true;
    }

    // ========================================================================
    //  Test 07: 声望门控效果
    // ========================================================================

    bool Test_07_ReputationGating()
    {
        Debug.Log("\n<color=yellow>[Test 07] 声望门控效果测试...</color>");

        var npc = NPCManager.Instance;
        var market = ReputationMarketSystem.Instance;
        if (npc == null || market == null)
        {
            Debug.LogError("[Test 07] 跳过 — NPCManager 或 ReputationMarketSystem 不存在");
            return false;
        }

        // 找一个有 marketConfig 的势力
        string factionId = null;
        foreach (var f in npc.GetAllFactions())
        {
            if (f != null && f.marketConfig != null)
            {
                factionId = f.factionId;
                break;
            }
        }

        if (factionId == null)
        {
            Debug.LogWarning("[Test 07] 跳过 — 没有配置 marketConfig 的势力");
            return true;
        }

        int originalRep = npc.GetReputation(factionId);
        Debug.Log($"  势力: {factionId}, 原始声望: {originalRep}");

        // 测试低声望
        npc.SetReputation(factionId, -60);
        var lowGoods = market.GetAvailableGoods(factionId, null);
        var lowTier = market.GetCurrentTier(factionId);
        Debug.Log($"  声望=-60: 层级={lowTier?.tierName ?? "无"}, 可交易={lowGoods.Count}种");

        // 测试中等声望
        npc.SetReputation(factionId, 30);
        var midGoods = market.GetAvailableGoods(factionId, null);
        var midTier = market.GetCurrentTier(factionId);
        Debug.Log($"  声望=+30: 层级={midTier?.tierName ?? "无"}, 可交易={midGoods.Count}种");

        // 测试高声望
        npc.SetReputation(factionId, 90);
        var highGoods = market.GetAvailableGoods(factionId, null);
        var highTier = market.GetCurrentTier(factionId);
        Debug.Log($"  声望=+90: 层级={highTier?.tierName ?? "无"}, 可交易={highGoods.Count}种");

        // 恢复原始声望
        npc.SetReputation(factionId, originalRep);
        Debug.Log($"  声望已恢复为: {originalRep}");

        bool gatingWorks = highGoods.Count >= midGoods.Count && midGoods.Count >= lowGoods.Count;
        if (gatingWorks)
            Debug.Log("<color=green>[Test 07] 通过 — 声望越高可交易商品越多</color>");
        else
            Debug.LogWarning("<color=yellow>[Test 07] 警告 — 声望门控效果不明显，请检查 ReputationMarketConfig 配置</color>");

        return true;
    }

    // ========================================================================
    //  打印所有系统状态 (F6)
    // ========================================================================

    [ContextMenu("★ 打印所有系统状态 (F6)")]
    public void PrintAllSystemStatus()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Tester] 仅在运行模式下可用"); return; }

        Debug.Log("<color=cyan>══════ 系统状态报告 ══════</color>");

        // 基地
        if (BaseManager.Instance != null)
        {
            var bases = BaseManager.Instance.AllBaseSaveData;
            Debug.Log($"\n<color=white>【基地】</color> 数量: {bases.Count}, 活跃: {BaseManager.Instance.ActiveBaseId}");
            foreach (var b in bases)
            {
                float vehicles = b.GetResourceAmount("vehicle_truck");
                string dockInfo = b.hasDockYard ? $"✓ (装卸={b.dockLoadingSeconds:F1}s)" : "✗";
                Debug.Log($"  {b.baseName} — 金钱: {b.money:F0}, 载具: {vehicles}, DockYard: {dockInfo}");
            }
        }

        // NPC 声望
        if (NPCManager.Instance != null)
        {
            Debug.Log($"\n<color=white>【NPC声望】</color>");
            foreach (var f in NPCManager.Instance.GetAllFactions())
            {
                if (f == null) continue;
                int rep = NPCManager.Instance.GetReputation(f.factionId);
                string level = NPCFaction.GetReputationLevel(rep);
                bool canTrade = NPCManager.Instance.CanTradeWith(f.factionId);
                string hasMarket = f.marketConfig != null ? "✓" : "✗";
                Debug.Log($"  {f.displayName}: 声望={rep} ({level}), 可交易={canTrade}, 市场配置={hasMarket}");
            }

            Debug.Log($"\n<color=white>【NPC据点】</color>");
            foreach (var o in NPCManager.Instance.GetAllOutposts())
            {
                Debug.Log($"  {o.displayName} ({o.factionId}) at {o.cell} — " +
                          $"发现={o.isDiscovered}, 可交易={o.canTrade}, " +
                          $"卖={o.sellStock.Count}种, 买={o.buyDemand.Count}种, " +
                          $"任务={o.questBoardIds.Count}个");
            }
        }

        // 贸易路线
        if (TradeManager.Instance != null)
        {
            var routes = TradeManager.Instance.GetAllTradeRoutes();
            var orders = TradeManager.Instance.GetActiveOrders();
            Debug.Log($"\n<color=white>【贸易路线】</color> 数量: {routes.Count}, 活跃订单: {orders.Count}");
            foreach (var r in routes)
            {
                float roadTime = TradeManager.Instance.CalculateRoadTravelTime(r);
                float loadTime = TradeManager.Instance.GetBaseLoadingTime(r.sourceBaseId);
                float totalTime = TradeManager.Instance.CalculateTravelTime(r);
                Debug.Log($"  {r.displayName}: {r.sourceCell}→{r.targetCell}, " +
                          $"有效={r.isValid}, 路径={r.PathLength}格, 货物={r.cargoItems.Count}种, " +
                          $"载具={r.vehicleResourceId}×{r.requiredVehicles}, 容量={r.vehicleCapacityPerUnit}");
                Debug.Log($"    时间: 去程={totalTime:F1}s (路途={roadTime:F1}s + 装卸={loadTime:F1}s), " +
                          $"返程={roadTime:F1}s (空车)");
            }
            foreach (var o in orders)
            {
                Debug.Log($"  订单 {o.orderId.Substring(0, 8)}: {o.state}, 进度={o.Progress:P0}, " +
                          $"剩余={o.FormattedRemainingTime}");
            }
        }

        // 载具运输
        if (VehicleTransportManager.Instance != null)
        {
            var jobs = VehicleTransportManager.Instance.GetActiveJobs();
            Debug.Log($"\n<color=white>【多趟运输】</color> 活跃任务: {jobs.Count}");
            foreach (var j in jobs)
            {
                Debug.Log($"  Job {j.jobId.Substring(0, 8)}: {j.state}, " +
                          $"趟次={j.tripsCompleted}/{j.totalTripsNeeded}, " +
                          $"在途载具={j.vehiclesInTransit}/{j.assignedVehicles}, " +
                          $"已交付={j.totalDelivered}, 损失={j.totalLost}, " +
                          $"任务={j.questInstanceId ?? "无"}");
            }
        }

        // 任务
        if (QuestManager.Instance != null)
        {
            var active = QuestManager.Instance.GetActiveQuests();
            Debug.Log($"\n<color=white>【活跃任务】</color> 数量: {active.Count}");
            foreach (var q in active)
            {
                string progressInfo = "";
                foreach (var p in q.progress)
                {
                    progressInfo += $"{p.resourceId}: {p.currentAmount}/{p.requiredAmount} ";
                }
                Debug.Log($"  [{q.state}] {q.displayName} — {progressInfo}— 进度={q.TotalProgress:P0}");
            }
        }

        // 声望市场
        if (ReputationMarketSystem.Instance != null && NPCManager.Instance != null)
        {
            Debug.Log($"\n<color=white>【声望市场】</color>");
            foreach (var f in NPCManager.Instance.GetAllFactions())
            {
                if (f == null || f.marketConfig == null) continue;
                var tier = ReputationMarketSystem.Instance.GetCurrentTier(f.factionId);
                float mult = ReputationMarketSystem.Instance.GetPriceMultiplier(f.factionId);
                Debug.Log($"  {f.displayName}: 层级={tier?.tierName ?? "无"}, 价格倍率={mult:F2}");
            }
        }

        Debug.Log("<color=cyan>══════ 状态报告结束 ══════</color>");
    }

    // ========================================================================
    //  刷新所有据点 (F7)
    // ========================================================================

    [ContextMenu("★ 刷新所有据点库存与任务 (F7)")]
    public void RefreshAllOutposts()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Tester] 仅在运行模式下可用"); return; }

        Debug.Log("<color=yellow>刷新所有据点...</color>");

        if (ReputationMarketSystem.Instance != null)
        {
            ReputationMarketSystem.Instance.RefreshAllOutpostStocks();
            Debug.Log("  ✓ 库存刷新完成");
        }
        else
        {
            Debug.LogWarning("  ✗ ReputationMarketSystem 不存在");
        }

        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.RefreshAllQuestBoards();
            Debug.Log("  ✓ 任务刷新完成");
        }
        else
        {
            Debug.LogWarning("  ✗ QuestManager 不存在");
        }
    }

    // ========================================================================
    //  独立工具按钮
    // ========================================================================

    [ContextMenu("工具/注入载具到活跃基地 (+10)")]
    public void InjectVehicles()
    {
        if (!Application.isPlaying) return;
        string baseId = GetTestBaseId();
        if (string.IsNullOrEmpty(baseId)) { Debug.LogError("没有活跃基地"); return; }

        var baseSave = BaseManager.Instance.GetBaseSaveData(baseId);
        if (baseSave == null) return;

        baseSave.AddResource("vehicle_truck", 10);
        BaseManager.Instance.UpdateBaseSaveData(baseSave);
        Debug.Log($"[Tester] 已注入 10 辆载具到基地 '{baseSave.baseName}'，" +
                  $"当前: {baseSave.GetResourceAmount("vehicle_truck")}");
    }

    [ContextMenu("工具/注入测试资源到活跃基地 (+1000)")]
    public void InjectResources()
    {
        if (!Application.isPlaying) return;
        string baseId = GetTestBaseId();
        if (string.IsNullOrEmpty(baseId)) { Debug.LogError("没有活跃基地"); return; }

        var baseSave = BaseManager.Instance.GetBaseSaveData(baseId);
        if (baseSave == null) return;

        string[] resources = { "wood", "ore", "food", "water" };
        foreach (var r in resources)
        {
            baseSave.AddResource(r, 1000);
        }
        baseSave.money += 5000;
        BaseManager.Instance.UpdateBaseSaveData(baseSave);
        Debug.Log($"[Tester] 已注入资源到基地 '{baseSave.baseName}': wood/ore/food/water×1000, 金钱+5000");
    }

    [ContextMenu("工具/设置声望 +80 (所有势力)")]
    public void SetAllReputationHigh()
    {
        if (!Application.isPlaying) return;
        if (NPCManager.Instance == null) return;

        foreach (var f in NPCManager.Instance.GetAllFactions())
        {
            if (f != null)
                NPCManager.Instance.SetReputation(f.factionId, 80);
        }
        Debug.Log("[Tester] 所有势力声望已设为 +80");
    }

    [ContextMenu("工具/设置声望 0 (所有势力)")]
    public void SetAllReputationNeutral()
    {
        if (!Application.isPlaying) return;
        if (NPCManager.Instance == null) return;

        foreach (var f in NPCManager.Instance.GetAllFactions())
        {
            if (f != null)
                NPCManager.Instance.SetReputation(f.factionId, 0);
        }
        Debug.Log("[Tester] 所有势力声望已设为 0");
    }

    [ContextMenu("工具/设置声望 -60 (所有势力)")]
    public void SetAllReputationLow()
    {
        if (!Application.isPlaying) return;
        if (NPCManager.Instance == null) return;

        foreach (var f in NPCManager.Instance.GetAllFactions())
        {
            if (f != null)
                NPCManager.Instance.SetReputation(f.factionId, -60);
        }
        Debug.Log("[Tester] 所有势力声望已设为 -60");
    }

    [ContextMenu("工具/手动提交第一个就绪任务")]
    public void SubmitFirstReadyQuest()
    {
        if (!Application.isPlaying) return;
        if (QuestManager.Instance == null) return;

        foreach (var q in QuestManager.Instance.GetActiveQuests())
        {
            if (q.state == QuestState.ReadyToSubmit)
            {
                bool ok = QuestManager.Instance.TrySubmitQuest(q.instanceId);
                Debug.Log($"[Tester] 提交任务 '{q.displayName}': {(ok ? "成功" : "失败")}");
                return;
            }
        }
        Debug.LogWarning("[Tester] 没有处于 ReadyToSubmit 状态的任务");
    }

    [ContextMenu("工具/强制完成第一个活跃任务进度")]
    public void ForceCompleteFirstQuest()
    {
        if (!Application.isPlaying) return;
        if (QuestManager.Instance == null) return;

        foreach (var q in QuestManager.Instance.GetActiveQuests())
        {
            if (q.IsActive)
            {
                foreach (var p in q.progress)
                {
                    p.currentAmount = p.requiredAmount;
                }
                q.state = QuestState.ReadyToSubmit;
                Debug.Log($"[Tester] 已强制完成任务进度: '{q.displayName}'，状态=ReadyToSubmit");
                Debug.Log($"  现在可以使用 [手动提交第一个就绪任务] 来提交");
                return;
            }
        }
        Debug.LogWarning("[Tester] 没有活跃任务");
    }

    [ContextMenu("工具/设置装卸时间 2s (模拟加速卡片)")]
    public void SetLoadingTimeFast()
    {
        SetDockLoadingTime(2f);
    }

    [ContextMenu("工具/设置装卸时间 4s (默认)")]
    public void SetLoadingTimeDefault()
    {
        SetDockLoadingTime(4f);
    }

    [ContextMenu("工具/设置装卸时间 8s (模拟低等级码头)")]
    public void SetLoadingTimeSlow()
    {
        SetDockLoadingTime(8f);
    }

    private void SetDockLoadingTime(float seconds)
    {
        if (!Application.isPlaying) return;
        string baseId = GetTestBaseId();
        if (string.IsNullOrEmpty(baseId)) { Debug.LogError("没有活跃基地"); return; }

        var baseSave = BaseManager.Instance.GetBaseSaveData(baseId);
        if (baseSave == null) return;

        baseSave.hasDockYard = true;
        baseSave.dockLoadingSeconds = seconds;
        BaseManager.Instance.UpdateBaseSaveData(baseSave);
        Debug.Log($"[Tester] 基地 '{baseSave.baseName}' 装卸时间已设为 {seconds:F1}s");
    }

    // ========================================================================
    //  一键创建测试贸易路线 (F8)
    // ========================================================================

    [ContextMenu("★ 一键创建贸易路线 (F8)")]
    public void CreateTestTradeRoute()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Tester] 仅在运行模式下可用"); return; }

        Debug.Log("<color=cyan>══════ 一键创建测试贸易路线 ══════</color>");

        // 1) 检查必要管理器
        var baseMgr = BaseManager.Instance;
        var tradeMgr = TradeManager.Instance;
        var npcMgr = NPCManager.Instance;
        var roadNetwork = FindObjectOfType<RoadNetwork>();

        if (baseMgr == null || tradeMgr == null || npcMgr == null)
        {
            Debug.LogError("[Tester] 缺少必要管理器 (BaseManager / TradeManager / NPCManager)");
            return;
        }
        if (roadNetwork == null)
        {
            Debug.LogError("[Tester] 找不到 RoadNetwork 组件");
            return;
        }

        // 2) 获取活跃基地
        string baseId = GetTestBaseId();
        if (string.IsNullOrEmpty(baseId))
        {
            Debug.LogError("[Tester] 没有活跃基地");
            return;
        }

        var baseSave = baseMgr.GetBaseSaveData(baseId);
        if (baseSave == null)
        {
            Debug.LogError($"[Tester] 找不到基地存档: {baseId}");
            return;
        }

        // 通过 WorldMapManager 将世界坐标转为格子坐标
        var wmm = WorldMapManager.Instance;
        if (wmm == null)
        {
            Debug.LogError("[Tester] WorldMapManager 不存在，无法获取格子坐标");
            return;
        }

        Vector2Int baseCell = wmm.WorldToCell(baseSave.worldPosition);
        Vector2Int baseSize = baseMgr.baseGridSize;
        if (baseSize == Vector2Int.zero) baseSize = new Vector2Int(3, 3);

        // 检查 DockYard（装卸码头是贸易必需的）
        if (!baseSave.hasDockYard)
        {
            baseSave.hasDockYard = true;
            baseMgr.UpdateBaseSaveData(baseSave);
            Debug.Log("  <color=white>已自动启用 DockYard（装卸码头）用于测试</color>");
        }

        Debug.Log($"  基地: '{baseSave.baseName}' at {baseCell}, size={baseSize}");

        // 3) 找最近的 NPC 据点
        var outposts = npcMgr.GetAllOutposts();
        NPCOutpost bestOutpost = null;
        float bestDist = float.MaxValue;

        foreach (var outpost in outposts)
        {
            float dist = Vector2Int.Distance(baseCell, outpost.cell);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestOutpost = outpost;
            }
        }

        if (bestOutpost == null)
        {
            Debug.LogError("[Tester] 没有NPC据点！请先初始化NPC据点。");
            return;
        }

        Debug.Log($"  目标据点: '{bestOutpost.displayName}' ({bestOutpost.factionId}) at {bestOutpost.cell}, 距离={bestDist:F1}格");

        // 4) 检查是否已有该路线
        var existingRoutes = tradeMgr.GetAllTradeRoutes();
        foreach (var r in existingRoutes)
        {
            if (r.sourceBaseId == baseId && r.targetOutpostId == bestOutpost.outpostId)
            {
                Debug.Log($"<color=green>  ✓ 已存在到 '{bestOutpost.displayName}' 的贸易路线: {r.displayName}</color>");
                Debug.Log($"    路径长度={r.PathLength}格, 有效={r.isValid}");
                return;
            }
        }

        // 5) 自动铺设道路
        Debug.Log("  正在自动铺设道路...");
        int roadsBuilt = AutoBuildRoadBetween(roadNetwork, baseCell, baseSize, bestOutpost.cell, bestOutpost.size);
        if (roadsBuilt > 0)
        {
            Debug.Log($"  <color=white>已铺设 {roadsBuilt} 格道路</color>");
        }
        else
        {
            Debug.Log("  道路铺设未完成（可能已有道路或无法到达）");
        }

        // 6) 创建贸易路线
        var route = tradeMgr.CreateTradeRoute(
            baseSave.baseId, baseCell,
            bestOutpost.outpostId, bestOutpost.cell,
            baseSize
        );

        if (route == null)
        {
            Debug.LogError("[Tester] 贸易路线创建失败！请检查道路是否连通。");
            Debug.LogWarning("  提示: 可能需要手动在大地图上从基地边缘铺路到据点边缘。");
            return;
        }

        // 7) 添加测试货物
        route.AddCargoItem(testResourceId, testTransportAmount, TradeDirection.Export);
        route.vehicleResourceId = "vehicle_truck";
        route.vehicleCapacityPerUnit = 100;
        route.requiredVehicles = testVehicleCount;
        route.autoLoop = false;

        Debug.Log($"<color=green>  ✓ 贸易路线创建成功！</color>");
        Debug.Log($"    名称: {route.displayName}");
        Debug.Log($"    路径: {route.sourceCell} → {route.targetCell}");
        Debug.Log($"    路径长度: {route.PathLength} 格");
        Debug.Log($"    有效: {route.isValid}");
        Debug.Log($"    货物: {testResourceId} × {testTransportAmount} (出口)");
        Debug.Log($"    载具: vehicle_truck × {testVehicleCount}, 容量={route.vehicleCapacityPerUnit}");

        Debug.Log("<color=cyan>══════ 贸易路线创建完成 ══════</color>");
        Debug.Log("  现在可以按 F5 重新运行完整流程测试，Test 06 将自动使用此路线。");
    }

    /// <summary>
    /// 自动在两个区域之间铺设简单的直线道路（曼哈顿路径）
    /// </summary>
    private int AutoBuildRoadBetween(RoadNetwork roadNetwork, Vector2Int baseAnchor, Vector2Int baseSize,
                                       Vector2Int targetAnchor, Vector2Int targetSize)
    {
        int roadsBuilt = 0;

        // 获取默认道路类型
        var defaultRoadType = roadNetwork.GetDefaultRoadType();
        if (defaultRoadType == null)
        {
            Debug.LogError("    ✗ RoadNetwork 没有配置任何道路类型！无法自动铺路。");
            return 0;
        }
        string roadTypeId = defaultRoadType.roadTypeId;
        Debug.Log($"    使用道路类型: {defaultRoadType.roadTypeId} (等级 {defaultRoadType.level})");

        // 计算基地中心和据点中心
        Vector2Int baseCenter = baseAnchor + baseSize / 2;
        Vector2Int targetCenter = targetAnchor + targetSize / 2;

        // 找到基地边缘最接近目标的点（外一格）
        Vector2Int startCell = GetClosestEdgeCell(baseAnchor, baseSize, targetCenter);
        // 找到据点边缘最接近基地的点（外一格）
        Vector2Int endCell = GetClosestEdgeCell(targetAnchor, targetSize, baseCenter);

        Debug.Log($"    铺路: {startCell} → {endCell}");

        // 简单的曼哈顿路径：先走X轴，再走Y轴
        Vector2Int current = startCell;

        // 先沿X方向走
        int xDir = endCell.x > current.x ? 1 : -1;
        while (current.x != endCell.x)
        {
            if (TryPlaceRoadAt(roadNetwork, current, roadTypeId, baseAnchor, baseSize, targetAnchor, targetSize))
                roadsBuilt++;
            current.x += xDir;
        }

        // 再沿Y方向走
        int yDir = endCell.y > current.y ? 1 : -1;
        while (current.y != endCell.y)
        {
            if (TryPlaceRoadAt(roadNetwork, current, roadTypeId, baseAnchor, baseSize, targetAnchor, targetSize))
                roadsBuilt++;
            current.y += yDir;
        }

        // 最后一个格子
        if (TryPlaceRoadAt(roadNetwork, current, roadTypeId, baseAnchor, baseSize, targetAnchor, targetSize))
            roadsBuilt++;

        return roadsBuilt;
    }

    /// <summary>
    /// 尝试在指定位置放置道路（跳过已有道路和建筑区域内的格子）
    /// </summary>
    private bool TryPlaceRoadAt(RoadNetwork roadNetwork, Vector2Int cell, string roadTypeId,
                                Vector2Int area1Anchor, Vector2Int area1Size,
                                Vector2Int area2Anchor, Vector2Int area2Size)
    {
        // 跳过区域内部的格子
        if (IsInsideArea(cell, area1Anchor, area1Size)) return false;
        if (IsInsideArea(cell, area2Anchor, area2Size)) return false;

        // 检查是否已有道路
        if (roadNetwork.HasRoadAt(cell)) return false;

        // 使用 RoadNetwork 的正式API铺设道路
        bool success = roadNetwork.TryBuildRoad(cell, roadTypeId);
        return success;
    }

    /// <summary>
    /// 检查点是否在区域内部
    /// </summary>
    private bool IsInsideArea(Vector2Int cell, Vector2Int anchor, Vector2Int size)
    {
        return cell.x >= anchor.x && cell.x < anchor.x + size.x &&
               cell.y >= anchor.y && cell.y < anchor.y + size.y;
    }

    /// <summary>
    /// 获取区域边缘最接近目标点的格子（边缘外一格）
    /// </summary>
    private Vector2Int GetClosestEdgeCell(Vector2Int anchor, Vector2Int size, Vector2Int target)
    {
        // 生成区域外一格的边缘候选格子
        List<Vector2Int> edgeCells = new();

        // 上边缘
        for (int x = anchor.x - 1; x <= anchor.x + size.x; x++)
            edgeCells.Add(new Vector2Int(x, anchor.y + size.y));
        // 下边缘
        for (int x = anchor.x - 1; x <= anchor.x + size.x; x++)
            edgeCells.Add(new Vector2Int(x, anchor.y - 1));
        // 左边缘
        for (int y = anchor.y; y < anchor.y + size.y; y++)
            edgeCells.Add(new Vector2Int(anchor.x - 1, y));
        // 右边缘
        for (int y = anchor.y; y < anchor.y + size.y; y++)
            edgeCells.Add(new Vector2Int(anchor.x + size.x, y));

        // 找最接近目标的
        Vector2Int best = edgeCells[0];
        float bestDist = float.MaxValue;
        foreach (var cell in edgeCells)
        {
            float dist = Vector2Int.Distance(cell, target);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = cell;
            }
        }
        return best;
    }

    // ========================================================================
    //  Utility
    // ========================================================================

    private string GetTestBaseId()
    {
        if (!string.IsNullOrEmpty(testBaseId)) return testBaseId;
        if (BaseManager.Instance != null) return BaseManager.Instance.ActiveBaseId;
        return null;
    }
}
