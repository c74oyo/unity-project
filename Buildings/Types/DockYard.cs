using System.Collections.Generic;
using UnityEngine;
using Game.Building;

public class DockYard : MonoBehaviour
{
    public enum TradeMode { Import, Export }

    private readonly List<TradeTruck> _activeTrucks = new();
    private readonly HashSet<Transform> _occupiedDocks = new();
    private readonly Dictionary<TradeTruck, Transform> _truckDock = new();

    [Header("Points (Local)")]
    public Transform workerEntrancePoint;
    public Transform[] dockPoints;
    public Transform roadSpawnPoint;
    public Transform roadExitPoint;

    [Header("Spawn Throttle")]
    public bool throttleSpawn = true;
    public float spawnInterval = 8f;
    private float _spawnCooldown = 0f;

    [Header("Truck")]
    public TradeTruck truckPrefab;

    [Header("Loading Time")]
    public float baseLoadingSeconds = 4f;
    public bool useCardLoadSpeed = true;
    public Worksite worksite;
    public BuildingModifiers modifiers;

    [Header("Queue")]
    [SerializeField] private int queuedCount = 0;

    [Header("Trade")]
    public TradeMode tradeMode = TradeMode.Import;
    public List<ResourceAmount> importCargo = new();
    public List<ResourceAmount> exportCargo = new();
    public bool reserveExportOnSpawn = true;

    [Tooltip("可以是 GlobalInventory 或 BaseInventory")]
    public MonoBehaviour inventoryComponent;

    // Runtime property to get inventory as interface
    private IInventorySystem _inventory;
    public IInventorySystem Inventory
    {
        get
        {
            if (_inventory == null && inventoryComponent != null)
                _inventory = inventoryComponent as IInventorySystem;
            return _inventory;
        }
    }

    /// <summary>
    /// 运行时更新库存引用（由 BaseSceneLoader 在加载基地时调用）
    /// </summary>
    public void SetInventory(MonoBehaviour newInventory)
    {
        inventoryComponent = newInventory;
        _inventory = newInventory as IInventorySystem;
    }

    public int QueuedCount => queuedCount;
    public int ActiveCount => _activeTrucks.Count;
    public int DockCapacity => dockPoints != null ? dockPoints.Length : 0;

    [Header("Highway (Global)")]
    public HighwayManager highway;

    public float CardLoadSpeedMul
    {
        get
        {
            if (!useCardLoadSpeed) return 1f;

            if (modifiers == null)
                modifiers = GetComponentInParent<BuildingModifiers>() ?? GetComponentInChildren<BuildingModifiers>(true);

            if (modifiers == null)
            {
                Debug.LogWarning($"[DockYard] BuildingModifiers not found on {name}. Initialize in Awake/Start instead.", this);
                return 1f;
            }

            modifiers.useCardSlots = useCardLoadSpeed;
            return modifiers.GetMul(CardModifier.ModifierType.Dock_LoadSpeedMul, 0.1f);
        }
    }

    public float EffectiveLoadingSeconds
        => baseLoadingSeconds / CardLoadSpeedMul;

    private void Awake()
    {
        // Try to get inventory from component
        if (inventoryComponent != null)
        {
            _inventory = inventoryComponent as IInventorySystem;
        }

        // Fallback to GlobalInventory for backward compatibility
        if (_inventory == null)
        {
            var globalInv = GlobalInventory.Instance;
            if (globalInv != null)
            {
                inventoryComponent = globalInv;
                _inventory = globalInv as IInventorySystem;
            }
        }

        if (_inventory == null)
        {
            Debug.LogError("[DockYard] No valid inventory system found. Trade operations will not work.", this);
        }

        if (highway == null)
            highway = FindFirstObjectByType<HighwayManager>();

        if (worksite == null)
            worksite = GetComponentInChildren<Worksite>(true);

        if (modifiers == null)
            modifiers = GetComponentInParent<BuildingModifiers>() ?? GetComponentInChildren<BuildingModifiers>(true);
    }

    private void Update()
    {
        if (_spawnCooldown > 0f) _spawnCooldown -= Time.deltaTime;
        TrySpawnTrucks();
    }

    public void Enqueue(int count)
    {
        queuedCount += Mathf.Max(0, count);
        Debug.Log($"[DockYard] Enqueue({count}) -> queued={queuedCount}");
    }

    private void TrySpawnTrucks()
    {
        if (truckPrefab == null) return;
        if (dockPoints == null || dockPoints.Length == 0) return;
        if (roadSpawnPoint == null || roadExitPoint == null) return;
        if (highway == null || highway.SpawnPoint == null || highway.DespawnPoint == null) return;

        if (queuedCount <= 0) return;
        if (_activeTrucks.Count >= dockPoints.Length) return;
        if (throttleSpawn && _spawnCooldown > 0f) return;

        Transform freeDock = GetFirstUnoccupiedDock();
        if (freeDock == null) return;

        if (tradeMode == TradeMode.Export && reserveExportOnSpawn)
        {
            if (Inventory == null)
            {
                Debug.LogError("[DockYard] Export pre-consume FAIL: Inventory is null", this);
                return;
            }

            bool ok = Inventory.TryConsumeBatch(exportCargo, 1);
            Debug.Log($"[DockYard] Export pre-consume result={ok}");
            if (!ok) return;
        }

        SpawnOneTruck(freeDock);
        queuedCount--;

        if (throttleSpawn)
            _spawnCooldown = spawnInterval;
    }

    private Transform GetFirstUnoccupiedDock()
    {
        foreach (var dp in dockPoints)
        {
            if (dp == null) continue;
            if (_occupiedDocks.Contains(dp)) continue;
            return dp;
        }
        return null;
    }

    private void SpawnOneTruck(Transform dockPoint)
    {
        Transform spawn = highway.SpawnPoint;
        Transform despawn = highway.DespawnPoint;

        var truck = Instantiate(truckPrefab, spawn.position, spawn.rotation);

        _activeTrucks.Add(truck);
        _truckDock[truck] = dockPoint;
        _occupiedDocks.Add(dockPoint);

        // 关键：传入 EffectiveLoadingSeconds（统一加成后的装卸时间）
        truck.Init(
            this,
            roadSpawnPoint,
            dockPoint,
            roadExitPoint,
            despawn,
            EffectiveLoadingSeconds
        );
    }

    public void NotifyTruckArrived(TradeTruck truck) { }

    public void NotifyTruckFinishedLoading(TradeTruck truck)
    {
        if (Inventory == null)
        {
            Debug.LogWarning("[DockYard] Cannot complete trade: Inventory is null", this);
            return;
        }

        Debug.Log($"[DockYard] FinishedLoading tradeMode={tradeMode} reserveOnSpawn={reserveExportOnSpawn}");

        if (tradeMode == TradeMode.Import)
        {
            Inventory.AddBatch(importCargo, 1);
        }
        else
        {
            if (!reserveExportOnSpawn)
                Inventory.TryConsumeBatch(exportCargo, 1);
        }
    }

    public void NotifyTruckExited(TradeTruck truck)
    {
        _activeTrucks.Remove(truck);

        if (_truckDock.TryGetValue(truck, out var dp) && dp != null)
            _occupiedDocks.Remove(dp);

        _truckDock.Remove(truck);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        if (roadSpawnPoint) Gizmos.DrawSphere(roadSpawnPoint.position, 0.3f);
        if (roadExitPoint) Gizmos.DrawSphere(roadExitPoint.position, 0.3f);

        Gizmos.color = Color.green;
        if (dockPoints != null)
            foreach (var dp in dockPoints)
                if (dp) Gizmos.DrawSphere(dp.position, 0.25f);
    }
#endif
}
