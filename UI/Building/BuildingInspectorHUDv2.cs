using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BuildingInspectorHUDv2 : MonoBehaviour
{
    [Header("Pick (optional)")]
    public Camera cam;
    public LayerMask buildingMask;          // 只勾 Building 层
    public float pickDistance = 500f;

    [Header("External mode lock (optional)")]
    public PlacementManager placement;      // 如果你希望建造/拆除模式时不选建筑，可拖进来

    [Header("Root")]
    public GameObject panelRoot;            // 整个面板 Root（用于整体显示/隐藏）
    public Button closeButton;

    [Header("Header TMP")]
    public TMP_Text titleTMP;
    public TMP_Text typeTMP;                // 可选：显示有哪些组件（Producer/Worksite/Power等）

    [Header("Tabs")]
    public Button tabOverview;
    public Button tabProduction;
    public Button tabWorkers;
    public Button tabPower;
    public Button tabTrade;
    public Button tabStorage;

    [Header("Tab Panels")]
    public GameObject panelOverview;
    public GameObject panelProduction;
    public GameObject panelWorkers;
    public GameObject panelPower;
    public GameObject panelTrade;
    public GameObject panelStorage;

    [Header("Tab TMP (one big text per tab)")]
    public TMP_Text overviewTMP;
    public TMP_Text productionTMP;
    public TMP_Text workersTMP;
    public TMP_Text powerTMP;
    public TMP_Text tradeTMP;
    public TMP_Text storageTMP;

    [Header("Workers Controls (optional)")]
    public Button btnWorkerMinus;
    public Button btnWorkerPlus;
    public Button btnWorkerZero;
    public Button btnWorkerMax;

    [Header("Card Slots UI (NEW)")]
    public WorksiteSlotsPanelUI slotsPanel; // 把你 Workers 页里挂了 WorksiteSlotsPanelUI 的对象拖进来

    [Header("Refresh")]
    public float refreshInterval = 0.2f;
    float _timer;

    [Header("Runtime")]
    public GameObject targetRoot; // 当前选中的建筑根对象

    // cached comps
    ProducerBuilding _producer;
    Worksite _worksite;
    PowerConsumer _power;
    PowerGenerator _powerGen;
    DockYard _dockyard;
    WarehouseBuilding _warehouse;

    enum Tab { Overview, Production, Workers, Power, Trade, Storage }
    Tab _tab = Tab.Overview;

    readonly StringBuilder _sb = new StringBuilder(512);

    void Reset()
    {
        cam = Camera.main;
    }

    void Awake()
    {
        if (closeButton) closeButton.onClick.AddListener(Close);

        if (tabOverview) tabOverview.onClick.AddListener(() => SetTab(Tab.Overview));
        if (tabProduction) tabProduction.onClick.AddListener(() => SetTab(Tab.Production));
        if (tabWorkers) tabWorkers.onClick.AddListener(() => SetTab(Tab.Workers));
        if (tabPower) tabPower.onClick.AddListener(() => SetTab(Tab.Power));
        if (tabTrade) tabTrade.onClick.AddListener(() => SetTab(Tab.Trade));
        if (tabStorage) tabStorage.onClick.AddListener(() => SetTab(Tab.Storage));

        if (btnWorkerMinus) btnWorkerMinus.onClick.AddListener(() => AddDesiredWorkers(-1));
        if (btnWorkerPlus) btnWorkerPlus.onClick.AddListener(() => AddDesiredWorkers(+1));
        if (btnWorkerZero) btnWorkerZero.onClick.AddListener(() => SetDesiredWorkers(0));
        if (btnWorkerMax) btnWorkerMax.onClick.AddListener(SetDesiredWorkersToMax);

        if (panelRoot) panelRoot.SetActive(false);
        SetTab(Tab.Overview);
    }

    void Update()
    {
        // 点击选择建筑
        if (Input.GetMouseButtonDown(0))
        {
            if (!EventSystem.current || !EventSystem.current.IsPointerOverGameObject())
            {
                if (placement == null || (!placement.BuildMode && !placement.DemolishMode))
                    TryPick();
            }
        }

        _timer += Time.deltaTime;
        if (_timer >= refreshInterval)
        {
            _timer = 0f;
            Refresh();
        }
    }

    void TryPick()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out var hit, pickDistance, buildingMask)) return;

        var root = hit.collider.GetComponentInParent<BuildableInstance>()?.gameObject;
        if (root == null) root = hit.collider.transform.root.gameObject;

        SetTarget(root);
    }

    public void SetTarget(GameObject root)
    {
        targetRoot = root;
        CacheComponents();

        if (panelRoot) panelRoot.SetActive(targetRoot != null);

        // NEW：绑定卡槽面板（无论当前在哪个 tab，都先 bind，显示时才 refresh）
        if (slotsPanel != null)
            slotsPanel.Bind(_worksite);

        if (targetRoot != null)
            SetTab(_tab);

        Refresh();
    }

    void CacheComponents()
    {
        _producer = null;
        _worksite = null;
        _power = null;
        _powerGen = null;
        _dockyard = null;
        _warehouse = null;

        if (targetRoot == null) return;

        _producer = targetRoot.GetComponentInChildren<ProducerBuilding>(true);
        _worksite = targetRoot.GetComponentInChildren<Worksite>(true);
        _power = targetRoot.GetComponentInChildren<PowerConsumer>(true);
        _powerGen = targetRoot.GetComponentInChildren<PowerGenerator>(true);
        _dockyard = targetRoot.GetComponentInChildren<DockYard>(true);
        _warehouse = targetRoot.GetComponentInChildren<WarehouseBuilding>(true);
    }

    public void Close()
    {
        targetRoot = null;
        CacheComponents();

        if (slotsPanel != null)
            slotsPanel.Bind(null);

        if (panelRoot) panelRoot.SetActive(false);
    }

    void SetTab(Tab tab)
    {
        _tab = tab;

        if (panelOverview) panelOverview.SetActive(tab == Tab.Overview);
        if (panelProduction) panelProduction.SetActive(tab == Tab.Production);
        if (panelWorkers) panelWorkers.SetActive(tab == Tab.Workers);
        if (panelPower) panelPower.SetActive(tab == Tab.Power);
        if (panelTrade) panelTrade.SetActive(tab == Tab.Trade);
        if (panelStorage) panelStorage.SetActive(tab == Tab.Storage);

        // NEW：切到 Workers 页时，立即刷新槽位显示（按钮/文本）
        if (tab == Tab.Workers && slotsPanel != null)
            slotsPanel.Refresh();

        Refresh();
    }

    void Refresh()
    {
        if (titleTMP == null) return;

        if (targetRoot == null)
        {
            titleTMP.text = "Building: (none)";
            if (typeTMP) typeTMP.text = "Tap a Building to inspect.";
            return;
        }

        titleTMP.text = targetRoot.name;

        if (typeTMP)
        {
            _sb.Clear();
            _sb.Append("Has: ");
            bool any = false;

            if (_producer) { _sb.Append("Producer "); any = true; }
            if (_worksite) { _sb.Append("Worksite "); any = true; }
            if (_power) { _sb.Append("Power "); any = true; }
            if (_powerGen) { _sb.Append("PowerGen "); any = true; }
            if (_dockyard) { _sb.Append("DockYard "); any = true; }
            if (_warehouse) { _sb.Append("Warehouse "); any = true; }

            if (!any) _sb.Append("(no known components)");
            typeTMP.text = _sb.ToString();
        }

        // NEW：持续刷新槽位（你给 refreshInterval=0.2，足够把卡牌变化同步到 UI）
        if (_tab == Tab.Workers && slotsPanel != null)
            slotsPanel.Refresh();

        if (overviewTMP) overviewTMP.text = BuildOverviewText();
        if (productionTMP) productionTMP.text = BuildProductionText();
        if (workersTMP) workersTMP.text = BuildWorkersText();
        if (powerTMP) powerTMP.text = BuildPowerText();
        if (tradeTMP) tradeTMP.text = BuildTradeText();
        if (storageTMP) storageTMP.text = BuildStorageText();
    }

    string BuildOverviewText()
    {
        _sb.Clear();

        if (_producer)
        {
            _sb.AppendLine($"Producer: {_producer.State}");
            if (!string.IsNullOrEmpty(_producer.BlockReason))
                _sb.AppendLine($"Reason: {_producer.BlockReason}");
            _sb.AppendLine($"Efficiency: {_producer.LastEfficiency:0.##}");

            // 资源区加成概览
            if (_producer.HasResourceZoneBonus && _producer.ActiveResourceZone != null)
            {
                var zone = _producer.ActiveResourceZone;
                _sb.AppendLine($"<color=#00FF88>◆ Zone: {zone.zoneId}</color>");
                if (zone.efficiencyBonus > 1f)
                    _sb.AppendLine($"  Output Bonus: {zone.efficiencyBonus:0.##}x");
                if (zone.qualityResourceChance > 0f)
                    _sb.AppendLine($"  Quality Chance: {zone.qualityResourceChance:P0}");
                if (zone.byproductChance > 0f)
                    _sb.AppendLine($"  Byproduct Chance: {zone.byproductChance:P0}");
            }

            _sb.AppendLine();
        }

        if (_worksite)
        {
            _sb.AppendLine("Workers:");
            _sb.AppendLine($"Desired: {_worksite.desiredWorkers}/{_worksite.maxWorkers}");
            _sb.AppendLine($"Present: {_worksite.PresentWorkers}   EnRoute: {_worksite.EnRouteWorkers}");
            if (_worksite.NoPathCount > 0) _sb.AppendLine($"NoPathCount: {_worksite.NoPathCount}");
            if (!_worksite.HasEntrance) _sb.AppendLine("Entrance: MISSING");

            // NEW：卡槽概览
            _sb.AppendLine($"CardSlots: {_worksite.FilledSlotCount}/{_worksite.SlotCount}");
            _sb.AppendLine();
        }

        if (_power)
        {
            _sb.AppendLine("Power Consumer:");
            _sb.AppendLine($"HasPower: {_power.HasPower}");
            _sb.AppendLine($"Demand: {_power.Demand:0.##}   Priority: {_power.priority}");
            _sb.AppendLine();
        }

        if (_powerGen)
        {
            _sb.AppendLine("Power Generator:");
            _sb.AppendLine($"Supply: {_powerGen.Supply:0.##}   Range: {_powerGen.EffectiveRange:0.##}");
            if (_powerGen.useWorkerBoost)
                _sb.AppendLine($"Workers: {_powerGen.assignedWorkers}/{_powerGen.maxWorkers}");
            _sb.AppendLine();
        }

        if (_dockyard)
{
    _sb.AppendLine("DockYard:");
    _sb.AppendLine($"Queued: {_dockyard.QueuedCount}");
    _sb.AppendLine($"Active: {_dockyard.ActiveCount}/{_dockyard.DockCapacity}");
    _sb.AppendLine($"Mode: {_dockyard.tradeMode}");

    // NEW: Loading bonus info
    _sb.AppendLine($"Load Base: {_dockyard.baseLoadingSeconds:0.##}s");
    _sb.AppendLine($"Load Mul:  {_dockyard.CardLoadSpeedMul:0.##}x");
    _sb.AppendLine($"Load Eff:  {_dockyard.EffectiveLoadingSeconds:0.##}s");

    _sb.AppendLine();
}


        if (_warehouse)
        {
            _sb.AppendLine("Warehouse:");
            _sb.AppendLine("Storage building detected (capacity extension).");
        }

        if (_sb.Length == 0)
            _sb.Append("No data to show.");

        return _sb.ToString();
    }

    string BuildProductionText()
    {
        _sb.Clear();

        if (!_producer)
        {
            _sb.Append("This building has no ProducerBuilding.");
            return _sb.ToString();
        }

        _sb.AppendLine($"State: {_producer.State}");
        if (!string.IsNullOrEmpty(_producer.BlockReason))
            _sb.AppendLine($"Reason: {_producer.BlockReason}");
        _sb.AppendLine($"Efficiency: {_producer.LastEfficiency:0.##}");
        _sb.AppendLine();

        float zoneMul = (_producer.HasResourceZoneBonus && _producer.ActiveResourceZone != null)
            ? _producer.ActiveResourceZone.efficiencyBonus : 1f;

        ResourceZoneType activeZone = _producer.ActiveResourceZone;

        _sb.AppendLine("Inputs (/sec @100%):");
        AppendBatch(_producer.inputsPerSecond, 1f, null);

        _sb.AppendLine();
        _sb.AppendLine($"Outputs (/sec @100%){(zoneMul > 1f ? $" <color=#00FF88>x{zoneMul:0.##}</color>" : "")}:");
        AppendBatch(_producer.outputsPerSecond, zoneMul, activeZone);

        // 资源区额外产出详情
        if (_producer.HasResourceZoneBonus && _producer.ActiveResourceZone != null)
        {
            var zone = _producer.ActiveResourceZone;
            _sb.AppendLine();
            _sb.AppendLine($"<color=#00FF88>=== Zone Bonus: {zone.zoneId} ({zone.efficiencyBonus:0.##}x) ===</color>");

            bool any = false;
            if (_producer.outputsPerSecond != null)
            {
                foreach (var ra in _producer.outputsPerSecond)
                {
                    if (ra.res == null || ra.amount <= 0) continue;

                    // 品质资源
                    if (ra.res.qualityVariant != null && zone.qualityResourceChance > 0f)
                    {
                        float qRate = ra.amount * zone.qualityResourceChance;
                        string qName = ra.res.qualityVariant.displayName ?? ra.res.qualityVariant.name;
                        _sb.AppendLine($"  <color=#FFD700>★ {qName}</color>: ~{qRate:0.##}/sec");
                        any = true;
                    }

                    // 副产品
                    if (ra.res.HasByproducts && zone.byproductChance > 0f)
                    {
                        foreach (var bp in ra.res.possibleByproducts)
                        {
                            if (bp == null) continue;
                            float bpRate = ra.amount * zone.byproductChance * ra.res.byproductBaseChance;
                            if (bpRate <= 0f) continue;
                            string bpName = bp.displayName ?? bp.name;
                            _sb.AppendLine($"  <color=#FF8800>◇ {bpName}</color>: ~{bpRate:0.###}/sec");
                            any = true;
                        }
                    }
                }
            }

            if (!any)
                _sb.AppendLine("  (No quality variant / byproduct configured)");
        }

        return _sb.ToString();
    }

    string BuildWorkersText()
    {
        _sb.Clear();

        if (!_worksite)
        {
            _sb.Append("This building has no Worksite.");
            return _sb.ToString();
        }

        _sb.AppendLine($"Desired: {_worksite.desiredWorkers}/{_worksite.maxWorkers}");
        _sb.AppendLine($"Present: {_worksite.PresentWorkers}");
        _sb.AppendLine($"EnRoute: {_worksite.EnRouteWorkers}");
        if (_worksite.NoPathCount > 0) _sb.AppendLine($"NoPathCount: {_worksite.NoPathCount}");

        // NEW：卡槽信息
        _sb.AppendLine($"CardSlots: {_worksite.FilledSlotCount}/{_worksite.SlotCount}");

        if (!_worksite.HasEntrance)
            _sb.AppendLine("Problem: workerEntrancePoint is missing.");

        _sb.AppendLine();
        _sb.AppendLine("Tip: Use +/- buttons to change Desired (mobile-friendly).");

        return _sb.ToString();
    }

    string BuildPowerText()
    {
        _sb.Clear();

        bool hasAny = false;

        // 发电信息
        if (_powerGen)
        {
            hasAny = true;
            _sb.AppendLine("=== Power Generator ===");
            _sb.AppendLine($"Base Supply: {_powerGen.BaseSupply:0.##}");
            _sb.AppendLine($"Base Range: {_powerGen.BaseRange:0.##}");
            _sb.AppendLine();
            _sb.AppendLine($"Effective Supply: {_powerGen.Supply:0.##}");
            _sb.AppendLine($"Effective Range: {_powerGen.EffectiveRange:0.##}");

            if (_powerGen.useWorkerBoost)
            {
                _sb.AppendLine();
                _sb.AppendLine($"Workers: {_powerGen.assignedWorkers}/{_powerGen.maxWorkers}");
                _sb.AppendLine($"Worker Supply Mul: {_powerGen.WorkerSupplyMul:0.##}x");
                _sb.AppendLine($"Worker Range Mul: {_powerGen.WorkerRangeMul:0.##}x");
            }

            if (_powerGen.externalSupplyMul != 1f || _powerGen.externalRangeMul != 1f)
            {
                _sb.AppendLine();
                _sb.AppendLine($"External Supply Mul: {_powerGen.externalSupplyMul:0.##}x");
                _sb.AppendLine($"External Range Mul: {_powerGen.externalRangeMul:0.##}x");
            }

            if (_powerGen.useCardRadiusFromWorksite && _powerGen.CardRangeMul != 1f)
            {
                _sb.AppendLine($"Card Range Mul: {_powerGen.CardRangeMul:0.##}x");
            }

            _sb.AppendLine();
        }

        // 耗电信息
        if (_power)
        {
            hasAny = true;
            _sb.AppendLine("=== Power Consumer ===");
            _sb.AppendLine($"HasPower: {_power.HasPower}");
            _sb.AppendLine($"Demand: {_power.Demand:0.##}");
            _sb.AppendLine($"Priority: {_power.priority}");
        }

        if (!hasAny)
        {
            _sb.Append("This building has no power components.");
        }

        return _sb.ToString();
    }

    string BuildTradeText()
    {
        _sb.Clear();

        if (!_dockyard)
        {
            _sb.Append("This building is not a DockYard.");
            return _sb.ToString();
        }

      _sb.AppendLine($"Mode: {_dockyard.tradeMode}");
_sb.AppendLine($"Queued: {_dockyard.QueuedCount}");
_sb.AppendLine($"Active: {_dockyard.ActiveCount}/{_dockyard.DockCapacity}");
_sb.AppendLine();
_sb.AppendLine($"Load Base: {_dockyard.baseLoadingSeconds:0.##}s");
_sb.AppendLine($"Load Mul:  {_dockyard.CardLoadSpeedMul:0.##}x");
_sb.AppendLine($"Load Eff:  {_dockyard.EffectiveLoadingSeconds:0.##}s");

        return _sb.ToString();
    }

    string BuildStorageText()
    {
        _sb.Clear();

        if (_warehouse)
        {
            _sb.AppendLine("Warehouse detected.");
            _sb.AppendLine("This building extends global storage capacity.");
        }
        else
        {
            _sb.Append("This building is not a WarehouseBuilding.");
        }

        return _sb.ToString();
    }

    void AppendBatch(System.Collections.Generic.List<ResourceAmount> list, float multiplier = 1f, ResourceZoneType activeZone = null)
    {
        if (list == null || list.Count == 0)
        {
            _sb.AppendLine("  (none)");
            return;
        }

        foreach (var ra in list)
        {
            if (ra.res == null) continue;
            float actual = ra.amount * multiplier;

            // 检查该资源是否与资源区匹配（获得加成）
            bool hasZoneBonus = (activeZone != null && activeZone.normalResource == ra.res);

            string resName = ra.res.name;
            if (hasZoneBonus)
                resName = $"<color=#00FF00>{resName}</color>"; // 绿色标记获得资源区加成的资源

            if (multiplier > 1f)
                _sb.AppendLine($"  - {resName} x{actual:0.##} <color=#888888>(base {ra.amount})</color>");
            else
                _sb.AppendLine($"  - {resName} x{ra.amount}");
        }
    }

    // ===== Workers control =====
    void AddDesiredWorkers(int delta)
    {
        if (_worksite == null) return;
        SetDesiredWorkers(_worksite.desiredWorkers + delta);
    }

    void SetDesiredWorkers(int value)
    {
        if (_worksite == null) return;
        _worksite.SetDesired(value);
        Refresh();
    }

    void SetDesiredWorkersToMax()
    {
        if (_worksite == null) return;
        _worksite.SetDesired(_worksite.maxWorkers);
        Refresh();
    }
}