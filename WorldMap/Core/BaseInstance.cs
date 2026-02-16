using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BaseInstance - 单个基地实体
/// 包含独立的库存、网格、建筑列表
/// </summary>
public class BaseInstance : MonoBehaviour
{
    [Header("Base Info")]
    public string baseName = "New Base";
    public string baseId = "";

    [Header("Systems")]
    public BaseInventory inventory;
    public GridSystem grid;
    public Transform buildingRoot;

    [Header("Position")]
    public Transform baseCenter;

    [Header("Runtime")]
    [SerializeField] private List<GameObject> _buildings = new();

    // Events
    public event Action<GameObject> OnBuildingAdded;
    public event Action<GameObject> OnBuildingRemoved;
    public event Action OnBaseDestroyed;

    // ============ Resource Zone ============
    private ResourceZoneType _cachedResourceZone;
    private bool _resourceZoneCached = false;

    /// <summary>
    /// 该基地所在格子的资源区类型
    /// 来源1：BaseSceneLoader 从 BaseSaveData 恢复时直接设置
    /// 来源2：在大地图场景中从 WorldMapManager 查询
    /// </summary>
    public ResourceZoneType ResourceZone
    {
        get
        {
            if (!_resourceZoneCached)
            {
                _resourceZoneCached = true;
                // 如果还没被直接设置，则尝试从 WorldMapManager 查询（大地图场景中）
                if (_cachedResourceZone == null && WorldMapManager.Instance != null)
                {
                    Vector2Int cell = WorldMapManager.Instance.WorldToCell(Position);
                    _cachedResourceZone = WorldMapManager.Instance.GetCellResourceZone(cell);
                }
            }
            return _cachedResourceZone;
        }
    }

    /// <summary>
    /// 直接设置资源区类型（BaseSceneLoader 进入基地场景时调用）
    /// </summary>
    public void SetResourceZone(ResourceZoneType zoneType)
    {
        _cachedResourceZone = zoneType;
        _resourceZoneCached = true;
    }

    /// <summary>
    /// 强制刷新资源区缓存（基地位置变更时调用）
    /// </summary>
    public void InvalidateResourceZoneCache()
    {
        _resourceZoneCached = false;
        _cachedResourceZone = null;
    }

    // ============ Public Properties ============
    public Vector3 Position => baseCenter != null ? baseCenter.position : transform.position;
    public int BuildingCount => _buildings.Count;

    // ============ Lifecycle ============
    private void Awake()
    {
        // Generate ID if not set
        if (string.IsNullOrEmpty(baseId))
            baseId = Guid.NewGuid().ToString();

        // Find or create inventory
        if (inventory == null)
        {
            inventory = GetComponentInChildren<BaseInventory>();
            if (inventory == null)
            {
                var invGO = new GameObject("Inventory");
                invGO.transform.SetParent(transform);
                inventory = invGO.AddComponent<BaseInventory>();
            }
        }

        if (inventory != null)
            inventory.ownerBase = this;

        // Find or create grid
        if (grid == null)
        {
            grid = GetComponentInChildren<GridSystem>();
            if (grid == null)
            {
                var gridGO = new GameObject("GridSystem");
                gridGO.transform.SetParent(transform);
                grid = gridGO.AddComponent<GridSystem>();
            }
        }

        // Create building root if not exists
        if (buildingRoot == null)
        {
            var rootGO = new GameObject("Buildings");
            rootGO.transform.SetParent(transform);
            buildingRoot = rootGO.transform;
        }

        // Register with BaseManager
        if (BaseManager.Instance != null)
            BaseManager.Instance.RegisterBase(this);
    }

    private void OnDestroy()
    {
        OnBaseDestroyed?.Invoke();

        if (BaseManager.Instance != null)
            BaseManager.Instance.UnregisterBase(this);
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(baseName))
            baseName = "New Base";
    }

    // ============ Building Management ============
    public void AddBuilding(GameObject building)
    {
        if (building == null) return;

        if (!_buildings.Contains(building))
        {
            _buildings.Add(building);

            // Parent to building root
            if (buildingRoot != null)
                building.transform.SetParent(buildingRoot);

            // Link ProducerBuilding to this base's inventory
            var producer = building.GetComponent<ProducerBuilding>();
            if (producer != null)
                producer.inventoryComponent = inventory;

            // Link DockYard to this base's inventory
            var dockyard = building.GetComponent<DockYard>();
            if (dockyard != null)
                dockyard.inventoryComponent = inventory;

            OnBuildingAdded?.Invoke(building);
        }
    }

    public void RemoveBuilding(GameObject building)
    {
        if (_buildings.Remove(building))
        {
            OnBuildingRemoved?.Invoke(building);
        }
    }

    public List<GameObject> GetAllBuildings()
    {
        _buildings.RemoveAll(b => b == null);
        return new List<GameObject>(_buildings);
    }

    public T[] GetBuildingsOfType<T>() where T : Component
    {
        _buildings.RemoveAll(b => b == null);

        var results = new List<T>();
        foreach (var building in _buildings)
        {
            var component = building.GetComponent<T>();
            if (component != null)
                results.Add(component);
        }

        return results.ToArray();
    }

    // ============ Query Methods ============
    public float GetDistanceTo(BaseInstance otherBase)
    {
        if (otherBase == null) return float.MaxValue;
        return Vector3.Distance(Position, otherBase.Position);
    }

    public float GetDistanceTo(Vector3 position)
    {
        return Vector3.Distance(Position, position);
    }

    // ============ Debug ============
#if UNITY_EDITOR
    [ContextMenu("Debug: Print Base Info")]
    private void DebugPrintInfo()
    {
        Debug.Log($"[BaseInstance] {baseName} (ID: {baseId})");
        Debug.Log($"  Position: {Position}");
        Debug.Log($"  Buildings: {BuildingCount}");
        if (inventory != null)
        {
            Debug.Log($"  Money: {inventory.Money:F2}");
            Debug.Log($"  Capacity: {inventory.UsedCapacity}/{inventory.TotalCapacity}");
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 pos = Position;

        // Draw base center
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(pos, 2f);

        // Draw base name
        UnityEditor.Handles.Label(pos + Vector3.up * 3f, baseName);

        // Draw grid bounds if available
        if (grid != null)
        {
            Gizmos.color = new Color(0, 1, 1, 0.3f);
            Vector3 size = new Vector3(grid.width * grid.cellSize, 0.1f, grid.height * grid.cellSize);
            Vector3 center = grid.origin + size * 0.5f;
            center.y = pos.y;
            Gizmos.DrawWireCube(center, size);
        }
    }
#endif
}