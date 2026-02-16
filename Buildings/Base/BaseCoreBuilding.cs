using UnityEngine;

/// <summary>
/// BaseCoreBuilding - 基地核心建筑
/// 玩家放置此建筑后，会在该位置创建一个新的基地实例
/// </summary>
public class BaseCoreBuilding : MonoBehaviour
{
    [Header("Base Settings")]
    public string newBaseName = "New Base";

    [Header("Grid Settings")]
    [Min(10)] public int gridWidth = 30;
    [Min(10)] public int gridHeight = 30;
    [Min(0.1f)] public float cellSize = 1f;

    [Header("Starting Resources")]
    [Min(0)] public int startingCapacity = 100;
    [Min(0)] public float startingMoney = 1000f;

    [Header("Runtime")]
    [SerializeField] private BaseInstance _createdBase;
    [SerializeField] private bool _hasCreatedBase = false;

    private BuildableInstance _buildableInstance;

    // ============ Public Properties ============
    public BaseInstance CreatedBase => _createdBase;
    public bool HasCreatedBase => _hasCreatedBase;

    // ============ Lifecycle ============
    private void Awake()
    {
        _buildableInstance = GetComponent<BuildableInstance>();
    }

    private void Start()
    {
        // Create base on first frame
        if (!_hasCreatedBase)
            CreateBase();
    }

    // ============ Base Creation ============
    private void CreateBase()
    {
        if (_hasCreatedBase)
        {
            Debug.LogWarning("[BaseCoreBuilding] Base already created!", this);
            return;
        }

        if (BaseManager.Instance == null)
        {
            Debug.LogError("[BaseCoreBuilding] BaseManager not found in scene!", this);
            return;
        }

        // Determine base position
        Vector3 basePosition = transform.position;

        // If this is a placed building, use the grid anchor
        if (_buildableInstance != null)
        {
            var grid = FindObjectOfType<GridSystem>();
            if (grid != null)
            {
                basePosition = grid.CellToWorldCenter(_buildableInstance.anchor);
            }
        }

        // Create base data in BaseManager
        BaseSaveData baseSaveData = BaseManager.Instance.CreateNewBase(basePosition, newBaseName);

        if (baseSaveData == null)
        {
            Debug.LogError("[BaseCoreBuilding] Failed to create base data!", this);
            return;
        }

        // Create BaseInstance GameObject in scene
        GameObject baseGO = new GameObject($"BaseInstance_{baseSaveData.baseName}");
        baseGO.transform.position = basePosition;
        _createdBase = baseGO.AddComponent<BaseInstance>();

        // Configure BaseInstance
        _createdBase.baseId = baseSaveData.baseId;
        _createdBase.baseName = baseSaveData.baseName;

        // Grid is auto-created in BaseInstance.Awake(), just configure it
        if (_createdBase.grid != null)
        {
            _createdBase.grid.origin = basePosition;
            _createdBase.grid.width = gridWidth;
            _createdBase.grid.height = gridHeight;
            _createdBase.grid.cellSize = cellSize;
        }

        // Inventory is auto-created in BaseInstance.Awake(), just configure it
        if (_createdBase.inventory != null)
        {
            _createdBase.inventory.baseCapacity = startingCapacity;
            _createdBase.inventory.money = startingMoney;
        }

        // Parent this building to the new base
        transform.SetParent(_createdBase.buildingRoot);
        _createdBase.AddBuilding(gameObject);

        _hasCreatedBase = true;

        Debug.Log($"[BaseCoreBuilding] Created base '{_createdBase.baseName}' at {basePosition}");

        // Set as active base
        BaseManager.Instance.SetActiveBase(_createdBase.baseId);
    }

    private void OnDestroy()
    {
        // Destroy the entire base when core building is destroyed
        if (_createdBase != null)
        {
            Debug.LogWarning($"[BaseCoreBuilding] Core building destroyed, destroying base '{_createdBase.baseName}'");
            Destroy(_createdBase.gameObject);
        }
    }

    // ============ Debug ============
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!_hasCreatedBase)
        {
            // Preview grid area
            Gizmos.color = new Color(0, 1, 0, 0.2f);
            Vector3 size = new Vector3(gridWidth * cellSize, 0.1f, gridHeight * cellSize);
            Vector3 center = transform.position + size * 0.5f;
            center.y = transform.position.y;
            Gizmos.DrawWireCube(center, size);

            // Draw label
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, $"Base: {newBaseName}");
        }
        else if (_createdBase != null)
        {
            // Draw connection to base center
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, _createdBase.Position);
        }
    }

    [ContextMenu("Force Create Base")]
    private void ForceCreateBase()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Can only create base in play mode");
            return;
        }

        _hasCreatedBase = false;
        CreateBase();
    }
#endif
}