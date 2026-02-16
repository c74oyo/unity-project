using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Standalone Market sub-panel. Opened by NPCOutpostPopup when the Market tab is clicked.
/// Shows tradeable goods with Trade buttons. Clicking Trade opens a quantity popup,
/// confirming creates/augments a TradeRoute via TradeManager.
/// </summary>
public class MarketSubPanel : MonoBehaviour
{
    [Header("Navigation")]
    public Button backButton;
    public TextMeshProUGUI panelTitle;

    [Header("Sell Section")]
    public TextMeshProUGUI sellHeader;
    public Transform sellListParent;

    [Header("Buy Section")]
    public TextMeshProUGUI buyHeader;
    public Transform buyListParent;

    [Header("Empty State")]
    public TextMeshProUGUI emptyHint;

    [Header("Prefab")]
    public GameObject marketItemPrefab;

    [Header("Trade")]
    [Tooltip("Quantity selector popup (singleton or direct reference)")]
    public TradeQuantityPopup tradePopup;

    // Runtime
    private string _outpostId;
    private NPCOutpost _outpost;
    private NPCFaction _faction;
    private float _refreshTimer;
    private const float RefreshInterval = 1f;

    private readonly List<GameObject> _spawnedItems = new();

    private void Start()
    {
        if (backButton != null)
            backButton.onClick.AddListener(OnBack);
    }

    private void Update()
    {
        if (!gameObject.activeSelf) return;

        _refreshTimer -= Time.unscaledDeltaTime;
        if (_refreshTimer <= 0f)
        {
            _refreshTimer = RefreshInterval;
            Refresh();
        }
    }

    // ============ Open / Close ============

    public void Open(string outpostId, NPCOutpost outpost, NPCFaction faction)
    {
        _outpostId = outpostId;
        _outpost = outpost;
        _faction = faction;

        gameObject.SetActive(true);

        if (panelTitle != null)
        {
            string name = outpost != null ? outpost.displayName : outpostId;
            panelTitle.text = $"Market - {name}";
        }

        _refreshTimer = 0f;
    }

    public void Close()
    {
        ClearItems();
        gameObject.SetActive(false);
        _outpost = null;
        _faction = null;
    }

    private void OnBack()
    {
        if (NPCOutpostPopup.Instance != null)
            NPCOutpostPopup.Instance.ShowInfoCard();
    }

    // ============ Refresh ============

    private void Refresh()
    {
        ClearItems();

        if (_outpost == null || NPCManager.Instance == null) return;

        _outpost = NPCManager.Instance.GetOutpost(_outpostId);
        if (_outpost == null) return;

        string factionId = _outpost.factionId;
        bool canTrade = NPCManager.Instance.CanTradeWith(factionId);

        if (!canTrade)
        {
            ShowHint("Reputation too low to trade with this faction");
            if (sellHeader != null) sellHeader.gameObject.SetActive(false);
            if (buyHeader != null) buyHeader.gameObject.SetActive(false);
            return;
        }

        // Collect special goods
        HashSet<string> specialResourceIds = new();
        if (ReputationMarketSystem.Instance != null)
        {
            var listings = ReputationMarketSystem.Instance.GetAvailableGoods(factionId, _outpostId);
            foreach (var listing in listings)
            {
                if (listing.isSpecialGood)
                    specialResourceIds.Add(listing.resourceId);
            }
        }

        bool hasAnyGoods = false;

        // === Sell list (NPC sells to player) ===
        if (_outpost.sellStock != null && _outpost.sellStock.Count > 0)
        {
            if (sellHeader != null)
            {
                sellHeader.gameObject.SetActive(true);
                sellHeader.text = $"-- For Sale ({_outpost.sellStock.Count}) --";
            }

            int i = 0;
            foreach (var stock in _outpost.sellStock)
            {
                SpawnItem(sellListParent, stock, true, specialResourceIds.Contains(stock.resourceId), i);
                i++;
            }
            hasAnyGoods = true;
        }
        else
        {
            if (sellHeader != null) sellHeader.gameObject.SetActive(false);
        }

        // === Buy list (NPC buys from player) ===
        if (_outpost.buyDemand != null && _outpost.buyDemand.Count > 0)
        {
            if (buyHeader != null)
            {
                buyHeader.gameObject.SetActive(true);
                buyHeader.text = $"-- Wanted ({_outpost.buyDemand.Count}) --";
            }

            int i = 0;
            foreach (var stock in _outpost.buyDemand)
            {
                SpawnItem(buyListParent, stock, false, specialResourceIds.Contains(stock.resourceId), i);
                i++;
            }
            hasAnyGoods = true;
        }
        else
        {
            if (buyHeader != null) buyHeader.gameObject.SetActive(false);
        }

        if (emptyHint != null)
        {
            if (!hasAnyGoods)
                ShowHint("No goods available at this outpost");
            else
                emptyHint.gameObject.SetActive(false);
        }
    }

    // ============ Spawn ============

    private void SpawnItem(Transform parent, ResourceStock stock,
                           bool isSell, bool isSpecial, int index)
    {
        if (marketItemPrefab == null || parent == null) return;

        var go = Instantiate(marketItemPrefab, parent);
        go.SetActive(true);
        _spawnedItems.Add(go);

        var ui = go.GetComponent<MarketGoodsItemUI>();
        if (ui != null)
        {
            ui.Setup(stock, isSell, isSpecial, OnTradeButtonClicked);
            ui.SetAlternateBackground(index % 2 == 1);
        }
    }

    // ============ Trade Flow ============

    /// <summary>
    /// Called when a Trade button is clicked on a market item.
    /// Runs pre-checks, then opens quantity popup.
    /// </summary>
    private void OnTradeButtonClicked(ResourceStock stock, bool isSell)
    {
        if (stock == null || _outpost == null) return;

        string resourceName = MarketGoodsItemUI.FormatResourceName(stock.resourceId);
        string action = isSell ? "Buy" : "Sell";

        // --- Pre-checks with error popups ---

        // 1. Player base
        string baseId = NPCOutpostPopup.Instance != null
            ? NPCOutpostPopup.Instance.GetPlayerBaseId()
            : null;

        if (string.IsNullOrEmpty(baseId))
        {
            ShowTradeError($"{action} {resourceName}", "No player base found.");
            return;
        }

        // 2. DockYard
        if (TradeManager.Instance != null && !TradeManager.Instance.HasDockYard(baseId))
        {
            ShowTradeError($"{action} {resourceName}",
                "Your base does not have a DockYard.\nBuild a DockYard to enable trade routes.");
            return;
        }

        // 3. Base cell position
        var baseSave = BaseManager.Instance?.GetBaseSaveData(baseId);
        if (baseSave == null)
        {
            ShowTradeError($"{action} {resourceName}", "Base data not found.");
            return;
        }

        Vector2Int baseCell = Vector2Int.zero;
        if (WorldMapManager.Instance != null)
            baseCell = WorldMapManager.Instance.WorldToCell(baseSave.worldPosition);

        // 4. Road connectivity
        if (TradeManager.Instance != null)
        {
            bool connected = TradeManager.Instance.CanCreateTradeRoute(
                baseCell, _outpost.cell, _outpost.size);
            if (!connected)
            {
                ShowTradeError($"{action} {resourceName}",
                    "No road connection between your base and this outpost.\nBuild a road to connect them.");
                return;
            }
        }

        // 5. Max quantity
        int stockAmount = stock.amount;
        int reputationMax = NPCManager.Instance != null
            ? NPCManager.Instance.GetMaxTradeQuantity(_outpost.factionId, stock.resourceId)
            : int.MaxValue;

        int maxQty;
        if (isSell)
        {
            // NPC sells → player buys (Import): limited by NPC stock & reputation
            maxQty = Mathf.Min(stockAmount, reputationMax);
        }
        else
        {
            // NPC buys → player sells (Export): limited by player inventory & reputation
            int playerStock = 0;
            var playerBase = BaseManager.Instance?.GetBaseSaveData(baseId);
            if (playerBase != null)
                playerStock = Mathf.FloorToInt(playerBase.GetResourceAmount(stock.resourceId));
            maxQty = Mathf.Min(playerStock, reputationMax);
        }

        if (maxQty <= 0)
        {
            string reason = isSell
                ? "No stock available for this resource."
                : "You don't have this resource in your base.";
            ShowTradeError($"{action} {resourceName}", reason);
            return;
        }

        // 6. Unit price (with tax/multiplier)
        float unitPrice;
        string factionId = _outpost.factionId;
        if (isSell) // NPC sells → player buys (Import)
        {
            unitPrice = NPCManager.Instance != null
                ? NPCManager.Instance.CalculateBuyPrice(factionId, stock.resourceId, 1)
                : stock.pricePerUnit;
        }
        else // NPC buys → player sells (Export)
        {
            unitPrice = NPCManager.Instance != null
                ? NPCManager.Instance.CalculateSellPrice(factionId, stock.resourceId, 1)
                : stock.pricePerUnit;
        }

        // --- All checks passed, open quantity popup ---
        var popup = GetPopup();
        if (popup == null) return;

        Vector2Int capturedBaseCell = baseCell;
        string capturedBaseId = baseId;

        // stockDisplay: for buying show NPC stock, for selling show player inventory
        int stockDisplay = isSell ? stockAmount : maxQty;

        popup.Open(
            resourceName,
            isSell,
            unitPrice,
            maxQty,
            stockDisplay,
            (chosenQuantity) => OnTradeConfirmed(stock, isSell, chosenQuantity,
                                                  capturedBaseId, capturedBaseCell)
        );
    }

    /// <summary>
    /// Show a trade error in the popup (title + error message + cancel button only).
    /// </summary>
    private void ShowTradeError(string title, string errorMessage)
    {
        var popup = GetPopup();
        if (popup != null)
            popup.OpenError(title, errorMessage);
        else
            Debug.LogWarning($"[MarketSubPanel] {title}: {errorMessage}");
    }

    private TradeQuantityPopup GetPopup()
    {
        var popup = tradePopup != null ? tradePopup : TradeQuantityPopup.Instance;
        if (popup == null)
            Debug.LogError("[MarketSubPanel] TradeQuantityPopup not found!");
        return popup;
    }

    /// <summary>
    /// Called when the player confirms a trade quantity.
    /// Creates/finds a TradeRoute and adds the cargo.
    /// </summary>
    private void OnTradeConfirmed(ResourceStock stock, bool isSell, int quantity,
                                   string baseId, Vector2Int baseCell)
    {
        if (TradeManager.Instance == null || _outpost == null)
        {
            Debug.LogError("[MarketSubPanel] TradeManager or outpost is null");
            return;
        }

        // Direction mapping:
        // isSell (NPC sells) → player imports → TradeDirection.Import
        // !isSell (NPC buys) → player exports → TradeDirection.Export
        TradeDirection direction = isSell ? TradeDirection.Import : TradeDirection.Export;

        // Get base size
        Vector2Int baseSize = BaseManager.Instance != null
            ? BaseManager.Instance.baseGridSize
            : new Vector2Int(3, 3);

        // Find or create route (returns existing if same base→outpost already exists)
        TradeRoute route = TradeManager.Instance.CreateTradeRoute(
            baseId, baseCell, _outpostId, _outpost.cell, baseSize);

        if (route == null)
        {
            Debug.LogError("[MarketSubPanel] Failed to create/find trade route");
            ShowHint("Failed to create trade route");
            return;
        }

        // Add cargo (merges with existing if same resource+direction)
        route.AddCargoItem(stock.resourceId, quantity, direction);
        route.isActive = true;

        string dirLabel = isSell ? "Import" : "Export";
        Debug.Log($"[MarketSubPanel] Trade route set up: {dirLabel} {quantity}x {stock.resourceId} " +
                  $"via route {route.routeId.Substring(0, 8)}...");

        // Create a MultiTripTransportJob via VehicleTransportManager to actually dispatch trucks
        if (VehicleTransportManager.Instance != null)
        {
            var cargo = new System.Collections.Generic.List<TransportCargoItem>
            {
                new TransportCargoItem(stock.resourceId, quantity, direction)
            };

            // Use all available vehicles (min 1) for this job
            int availableVehicles = VehicleTransportManager.Instance.GetAvailableVehicles(baseId);
            if (availableVehicles <= 0)
            {
                Debug.LogWarning($"[MarketSubPanel] No vehicles available at base '{baseId}'. " +
                                 "Job created but will not dispatch until vehicles are available.");
            }
            int vehicleCount = Mathf.Max(1, availableVehicles);

            var job = VehicleTransportManager.Instance.CreateTransportJob(
                route.routeId, cargo, vehicleCount);

            if (job != null)
            {
                Debug.Log($"[MarketSubPanel] Transport job created: {job.totalTripsNeeded} trips, " +
                          $"{job.assignedVehicles} vehicles assigned");
            }
            else
            {
                Debug.LogWarning("[MarketSubPanel] Failed to create transport job — " +
                                 "check vehicle availability and route validity.");
            }
        }
        else
        {
            Debug.LogWarning("[MarketSubPanel] VehicleTransportManager not found. " +
                             "Route created but no transport job dispatched.");
        }

        // Trigger refresh to show updated stock
        _refreshTimer = 0f;
    }

    // ============ Helpers ============

    private void ShowHint(string message)
    {
        if (emptyHint != null)
        {
            emptyHint.gameObject.SetActive(true);
            emptyHint.text = message;
        }
        Debug.LogWarning($"[MarketSubPanel] {message}");
    }

    private void ClearItems()
    {
        foreach (var go in _spawnedItems)
        {
            if (go != null) Destroy(go);
        }
        _spawnedItems.Clear();
    }
}
