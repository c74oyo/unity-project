using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPC管理器 - 管理所有NPC势力和据点
/// </summary>
public class NPCManager : MonoBehaviour
{
    public static NPCManager Instance { get; private set; }

    [Header("Faction Definitions")]
    [Tooltip("所有可用的NPC势力定义")]
    public List<NPCFaction> factionDefinitions = new();

    [Header("Default Outposts")]
    [Tooltip("场景启动时自动生成的NPC据点配置")]
    public List<DefaultOutpostConfig> defaultOutposts = new();

    [Header("References")]
    public WorldMapManager worldMapManager;
    public ReputationMarketSystem marketSystem;

    // 运行时数据
    private Dictionary<string, NPCFaction> _factionCache = new();
    private Dictionary<string, NPCOutpost> _outposts = new();
    private Dictionary<string, int> _playerReputation = new(); // factionId -> reputation

    // 事件
    public event Action<string, int, int> OnReputationChanged; // factionId, oldValue, newValue
    public event Action<NPCOutpost> OnOutpostDiscovered;
    public event Action<NPCOutpost> OnOutpostAdded;

    // ============ Lifecycle ============

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        CacheFactions();

        if (worldMapManager == null)
            worldMapManager = FindObjectOfType<WorldMapManager>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void CacheFactions()
    {
        _factionCache.Clear();
        foreach (var faction in factionDefinitions)
        {
            if (faction != null && !string.IsNullOrEmpty(faction.factionId))
            {
                _factionCache[faction.factionId] = faction;

                // 初始化好感度
                if (!_playerReputation.ContainsKey(faction.factionId))
                {
                    _playerReputation[faction.factionId] = faction.initialReputation;
                }
            }
        }
    }

    // ============ Faction Access ============

    /// <summary>
    /// 获取势力定义
    /// </summary>
    public NPCFaction GetFaction(string factionId)
    {
        if (string.IsNullOrEmpty(factionId)) return null;
        _factionCache.TryGetValue(factionId, out var faction);
        return faction;
    }

    /// <summary>
    /// 获取所有势力
    /// </summary>
    public List<NPCFaction> GetAllFactions()
    {
        return new List<NPCFaction>(factionDefinitions);
    }

    // ============ Reputation System ============

    /// <summary>
    /// 获取玩家与某势力的好感度
    /// </summary>
    public int GetReputation(string factionId)
    {
        if (_playerReputation.TryGetValue(factionId, out var rep))
            return rep;

        var faction = GetFaction(factionId);
        return faction != null ? faction.initialReputation : 0;
    }

    /// <summary>
    /// 设置好感度
    /// </summary>
    public void SetReputation(string factionId, int value)
    {
        int oldValue = GetReputation(factionId);
        int newValue = Mathf.Clamp(value, -100, 100);

        _playerReputation[factionId] = newValue;

        if (oldValue != newValue)
        {
            OnReputationChanged?.Invoke(factionId, oldValue, newValue);
            Debug.Log($"[NPCManager] Reputation with {factionId}: {oldValue} -> {newValue}");
        }
    }

    /// <summary>
    /// 修改好感度
    /// </summary>
    public void ModifyReputation(string factionId, int delta)
    {
        int current = GetReputation(factionId);
        SetReputation(factionId, current + delta);
    }

    /// <summary>
    /// 每回合处理好感度衰减
    /// </summary>
    public void ProcessReputationDecay()
    {
        foreach (var faction in factionDefinitions)
        {
            if (faction == null) continue;

            int current = GetReputation(faction.factionId);
            if (current == 0) continue;

            // 向0衰减
            float decay = faction.reputationDecayRate;
            if (current > 0)
            {
                SetReputation(faction.factionId, Mathf.Max(0, current - Mathf.CeilToInt(decay)));
            }
            else
            {
                SetReputation(faction.factionId, Mathf.Min(0, current + Mathf.CeilToInt(decay)));
            }
        }
    }

    /// <summary>
    /// 检查是否可以与势力交易
    /// </summary>
    public bool CanTradeWith(string factionId)
    {
        var faction = GetFaction(factionId);
        if (faction == null) return false;

        int reputation = GetReputation(factionId);
        return faction.CanTradeWith(reputation);
    }

    /// <summary>
    /// 获取与势力交易的税率
    /// </summary>
    public float GetTaxRate(string factionId)
    {
        var faction = GetFaction(factionId);
        if (faction == null) return 0.1f;

        int reputation = GetReputation(factionId);
        return faction.GetEffectiveTaxRate(reputation);
    }

    // ============ Outpost Management ============

    /// <summary>
    /// 创建NPC据点
    /// </summary>
    public NPCOutpost CreateOutpost(string factionId, Vector2Int cell, string name = null)
    {
        var faction = GetFaction(factionId);
        if (faction == null)
        {
            Debug.LogWarning($"[NPCManager] Unknown faction: {factionId}");
            return null;
        }

        var outpost = new NPCOutpost(factionId, cell, name);
        _outposts[outpost.outpostId] = outpost;

        // 在WorldMapManager中标记
        if (worldMapManager != null)
        {
            // 设置据点所在格子
            var cellData = worldMapManager.GetOrCreateCellData(cell);
            if (cellData != null)
            {
                cellData.occupation = WorldMapCellData.OccupationType.NPCOutpost;
                cellData.occupationId = outpost.outpostId;
            }

            // 设置势力范围
            worldMapManager.SetNPCTerritoryDiamond(cell, faction.defaultTerritoryRadius, factionId);
        }

        OnOutpostAdded?.Invoke(outpost);
        Debug.Log($"[NPCManager] Created outpost '{outpost.displayName}' for faction '{factionId}' at {cell}");

        return outpost;
    }

    /// <summary>
    /// 获取据点
    /// </summary>
    public NPCOutpost GetOutpost(string outpostId)
    {
        _outposts.TryGetValue(outpostId, out var outpost);
        return outpost;
    }

    /// <summary>
    /// 获取指定位置的据点
    /// </summary>
    public NPCOutpost GetOutpostAt(Vector2Int cell)
    {
        foreach (var outpost in _outposts.Values)
        {
            if (outpost.cell == cell)
                return outpost;
        }
        return null;
    }

    /// <summary>
    /// 获取某势力的所有据点
    /// </summary>
    public List<NPCOutpost> GetOutpostsByFaction(string factionId)
    {
        var result = new List<NPCOutpost>();
        foreach (var outpost in _outposts.Values)
        {
            if (outpost.factionId == factionId)
                result.Add(outpost);
        }
        return result;
    }

    /// <summary>
    /// 获取所有已发现的据点
    /// </summary>
    public List<NPCOutpost> GetDiscoveredOutposts()
    {
        var result = new List<NPCOutpost>();
        foreach (var outpost in _outposts.Values)
        {
            if (outpost.isDiscovered)
                result.Add(outpost);
        }
        return result;
    }

    /// <summary>
    /// 获取所有据点（包括未发现的）
    /// </summary>
    public List<NPCOutpost> GetAllOutposts()
    {
        return new List<NPCOutpost>(_outposts.Values);
    }

    /// <summary>
    /// 通过ID获取势力（GetFaction的别名，为了兼容性）
    /// </summary>
    public NPCFaction GetFactionById(string factionId)
    {
        return GetFaction(factionId);
    }

    /// <summary>
    /// 发现据点
    /// </summary>
    public void DiscoverOutpost(string outpostId)
    {
        var outpost = GetOutpost(outpostId);
        if (outpost == null || outpost.isDiscovered) return;

        outpost.isDiscovered = true;
        OnOutpostDiscovered?.Invoke(outpost);

        Debug.Log($"[NPCManager] Discovered outpost: {outpost.displayName}");
    }

    // ============ Trade Operations ============

    /// <summary>
    /// 计算购买价格（玩家从NPC购买）
    /// </summary>
    public float CalculateBuyPrice(string factionId, string resourceId, int amount)
    {
        var outposts = GetOutpostsByFaction(factionId);
        float basePrice = 0f;

        foreach (var outpost in outposts)
        {
            var stock = outpost.GetSellStock(resourceId);
            if (stock != null)
            {
                basePrice = stock.pricePerUnit;
                break;
            }
        }

        if (basePrice <= 0) return 0f;

        // 应用税率
        float taxRate = GetTaxRate(factionId);
        float price = basePrice * amount * (1f + taxRate);

        // 应用声望市场倍率
        if (marketSystem != null)
            price *= marketSystem.GetPriceMultiplier(factionId);

        return price;
    }

    /// <summary>
    /// 计算出售价格（玩家卖给NPC）
    /// </summary>
    public float CalculateSellPrice(string factionId, string resourceId, int amount)
    {
        var outposts = GetOutpostsByFaction(factionId);
        float basePrice = 0f;

        foreach (var outpost in outposts)
        {
            var demand = outpost.GetBuyDemand(resourceId);
            if (demand != null)
            {
                basePrice = demand.pricePerUnit;
                break;
            }
        }

        if (basePrice <= 0) return 0f;

        // 应用税率（卖出时税率降低收益）
        float taxRate = GetTaxRate(factionId);
        float price = basePrice * amount * (1f - taxRate);

        // 应用声望市场倍率（卖出时高声望=倍率低=实际收益更高）
        if (marketSystem != null)
        {
            float multiplier = marketSystem.GetPriceMultiplier(factionId);
            // 对卖出价格，倍率反向作用：高声望(低倍率)提高收益
            if (multiplier > 0f)
                price /= multiplier;
        }

        return price;
    }

    // ============ Reputation Market Queries ============

    /// <summary>
    /// 获取指定势力在当前声望下可交易的资源ID列表
    /// </summary>
    public List<string> GetTradeableResources(string factionId)
    {
        if (marketSystem != null)
        {
            var listings = marketSystem.GetAvailableGoods(factionId, null);
            var result = new List<string>();
            foreach (var l in listings)
                result.Add(l.resourceId);
            return result;
        }

        // fallback: 返回据点当前库存中的所有资源
        var outposts = GetOutpostsByFaction(factionId);
        var fallback = new List<string>();
        foreach (var outpost in outposts)
        {
            foreach (var stock in outpost.sellStock)
            {
                if (!fallback.Contains(stock.resourceId))
                    fallback.Add(stock.resourceId);
            }
        }
        return fallback;
    }

    /// <summary>
    /// 获取指定势力在当前声望下某资源的最大交易量
    /// </summary>
    public int GetMaxTradeQuantity(string factionId, string resourceId)
    {
        if (marketSystem != null)
            return marketSystem.GetMaxTradeQuantity(factionId, resourceId);
        return int.MaxValue;
    }

    // ============ Save/Load ============

    public NPCManagerSaveData GetSaveData()
    {
        var data = new NPCManagerSaveData();

        foreach (var kvp in _playerReputation)
        {
            data.reputations.Add(new ReputationData(kvp.Key, kvp.Value));
        }

        foreach (var outpost in _outposts.Values)
        {
            data.outposts.Add(outpost.Clone());
        }

        return data;
    }

    public void LoadFromSaveData(NPCManagerSaveData data)
    {
        if (data == null) return;

        _playerReputation.Clear();
        foreach (var rep in data.reputations)
        {
            _playerReputation[rep.factionId] = rep.value;
        }

        _outposts.Clear();
        foreach (var outpost in data.outposts)
        {
            _outposts[outpost.outpostId] = outpost;
        }
    }

    // ============ Initialization ============

    /// <summary>
    /// 初始化默认据点（由 WorldMapInitializer 调用）
    /// 只有在尚未创建据点时才会执行
    /// </summary>
    public void InitializeDefaultOutposts()
    {
        if (defaultOutposts == null || defaultOutposts.Count == 0)
        {
            Debug.Log("[NPCManager] No default outposts configured.");
            return;
        }

        int created = 0;
        foreach (var config in defaultOutposts)
        {
            if (string.IsNullOrEmpty(config.factionId))
            {
                Debug.LogWarning("[NPCManager] Default outpost config has empty factionId, skipping.");
                continue;
            }

            // 检查该位置是否已有据点（防止重复创建）
            var existing = GetOutpostAt(config.cell);
            if (existing != null)
            {
                Debug.Log($"[NPCManager] Outpost already exists at {config.cell}, skipping.");
                continue;
            }

            var outpost = CreateOutpost(config.factionId, config.cell, config.displayName);
            if (outpost != null)
            {
                outpost.isDiscovered = config.startDiscovered;
                outpost.canTrade = config.canTrade;
                created++;
            }
        }

        Debug.Log($"[NPCManager] Initialized {created}/{defaultOutposts.Count} default outposts. Total: {_outposts.Count}");

        // 自动确保场景中有 NPCOutpostVisualizer
        EnsureOutpostVisualizer();
    }

    /// <summary>
    /// 确保场景中存在 NPCOutpostVisualizer，如果没有则自动创建
    /// </summary>
    private void EnsureOutpostVisualizer()
    {
        var visualizer = FindObjectOfType<NPCOutpostVisualizer>();
        if (visualizer == null)
        {
            Debug.Log("[NPCManager] NPCOutpostVisualizer not found, creating one automatically...");
            var vizGO = new GameObject("NPCOutpostVisualizer");
            visualizer = vizGO.AddComponent<NPCOutpostVisualizer>();
            visualizer.npcManager = this;
            visualizer.worldMapManager = worldMapManager;
            visualizer.onlyShowDiscovered = false; // 默认显示所有据点
        }

        // 强制刷新
        visualizer.RebuildAllOutposts();
    }

    // ============ Debug ============
#if UNITY_EDITOR
    [ContextMenu("Debug: Print All Reputations")]
    private void DebugPrintReputations()
    {
        string info = "[NPCManager] Reputations:\n";
        foreach (var faction in factionDefinitions)
        {
            if (faction == null) continue;
            int rep = GetReputation(faction.factionId);
            string level = NPCFaction.GetReputationLevel(rep);
            info += $"  {faction.displayName}: {rep} ({level})\n";
        }
        Debug.Log(info);
    }

    [ContextMenu("Debug: Print All Outposts")]
    private void DebugPrintOutposts()
    {
        string info = $"[NPCManager] Outposts ({_outposts.Count}):\n";
        foreach (var outpost in _outposts.Values)
        {
            var faction = GetFaction(outpost.factionId);
            info += $"  {outpost.displayName} ({faction?.displayName ?? "Unknown"}) at {outpost.cell}\n";
        }
        Debug.Log(info);
    }
#endif
}

/// <summary>
/// NPC管理器保存数据
/// </summary>
[Serializable]
public class NPCManagerSaveData
{
    public List<ReputationData> reputations = new();
    public List<NPCOutpost> outposts = new();
}

[Serializable]
public class ReputationData
{
    public string factionId;
    public int value;

    public ReputationData() { }

    public ReputationData(string factionId, int value)
    {
        this.factionId = factionId;
        this.value = value;
    }
}

/// <summary>
/// 默认据点配置 — 在Inspector中设置，场景启动时自动创建
/// </summary>
[Serializable]
public class DefaultOutpostConfig
{
    [Tooltip("所属势力ID（必须与 NPCFaction.factionId 匹配）")]
    public string factionId;

    [Tooltip("据点名称")]
    public string displayName;

    [Tooltip("大地图格子坐标")]
    public Vector2Int cell;

    [Tooltip("是否一开始就被发现")]
    public bool startDiscovered = true;

    [Tooltip("是否可以交易")]
    public bool canTrade = true;
}
