using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// WorldMapGrid - 大地图网格系统
/// 用于管理基地位置、战斗区域、资源点等的网格对齐
/// </summary>
public class WorldMapGrid : MonoBehaviour
{
    [Header("Grid Settings")]
    public Vector3 origin = Vector3.zero;
    [Min(0.1f)] public float cellSize = 10f;  // 大地图每格 10 米
    [Min(1)] public int width = 100;
    [Min(1)] public int height = 100;

    [Header("Visualization")]
    public bool showGrid = true;
    public Color gridColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
    public Color occupiedCellColor = new Color(1f, 0f, 0f, 0.5f);
    public Color baseCellColor = new Color(0f, 1f, 0f, 0.5f);

    // 区域类型
    public enum CellType
    {
        Empty,
        Base,           // 基地
        CombatZone,     // 战斗区域
        ResourceNode,   // 资源点
        Threat,         // 威胁区域
        Cleared         // 已清理区域
    }

    // 网格数据
    [Serializable]
    public class CellData
    {
        public Vector2Int cell;
        public CellType type;
        public string dataId;  // 基地ID、战斗区域ID等

        public CellData(Vector2Int cell, CellType type, string dataId = null)
        {
            this.cell = cell;
            this.type = type;
            this.dataId = dataId;
        }
    }

    [Header("Runtime Data")]
    [SerializeField] private List<CellData> _occupiedCells = new();

    private readonly Dictionary<Vector2Int, CellData> _cellMap = new();

    // Events
    public event Action<Vector2Int, CellType> OnCellOccupied;
    public event Action<Vector2Int> OnCellCleared;

    // ============ Grid Operations ============

    public bool IsInBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height;
    }

    public Vector2Int WorldToCell(Vector3 worldPos)
    {
        Vector3 local = worldPos - origin;
        int x = Mathf.FloorToInt(local.x / cellSize);
        int z = Mathf.FloorToInt(local.z / cellSize);
        return new Vector2Int(x, z);
    }

    public Vector3 CellToWorldCenter(Vector2Int cell)
    {
        return origin + new Vector3(
            (cell.x + 0.5f) * cellSize,
            0f,
            (cell.y + 0.5f) * cellSize
        );
    }

    public Vector3 SnapToGrid(Vector3 worldPos)
    {
        Vector2Int cell = WorldToCell(worldPos);
        return CellToWorldCenter(cell);
    }

    // ============ Cell Management ============

    public bool IsCellOccupied(Vector2Int cell)
    {
        return _cellMap.ContainsKey(cell);
    }

    public CellData GetCellData(Vector2Int cell)
    {
        _cellMap.TryGetValue(cell, out var data);
        return data;
    }

    public bool TryOccupyCell(Vector2Int cell, CellType type, string dataId = null)
    {
        if (!IsInBounds(cell))
        {
            Debug.LogWarning($"[WorldMapGrid] Cell {cell} is out of bounds");
            return false;
        }

        if (IsCellOccupied(cell))
        {
            Debug.LogWarning($"[WorldMapGrid] Cell {cell} is already occupied");
            return false;
        }

        CellData data = new CellData(cell, type, dataId);
        _cellMap[cell] = data;
        _occupiedCells.Add(data);

        OnCellOccupied?.Invoke(cell, type);
        return true;
    }

    public bool TryOccupyArea(Vector2Int anchorCell, Vector2Int size, CellType type, string dataId = null)
    {
        // Check if all cells are available
        if (!CanOccupyArea(anchorCell, size))
            return false;

        // Occupy all cells
        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                Vector2Int cell = new Vector2Int(anchorCell.x + x, anchorCell.y + y);
                TryOccupyCell(cell, type, dataId);
            }
        }

        return true;
    }

    /// <summary>
    /// 检查区域是否可以被占用（不实际占用）
    /// </summary>
    public bool CanOccupyArea(Vector2Int anchorCell, Vector2Int size)
    {
        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                Vector2Int cell = new Vector2Int(anchorCell.x + x, anchorCell.y + y);
                if (!IsInBounds(cell) || IsCellOccupied(cell))
                    return false;
            }
        }
        return true;
    }

    public void ClearCell(Vector2Int cell)
    {
        if (_cellMap.Remove(cell, out var data))
        {
            _occupiedCells.Remove(data);
            OnCellCleared?.Invoke(cell);
        }
    }

    public void ClearAllCells()
    {
        _cellMap.Clear();
        _occupiedCells.Clear();
    }

    // ============ Query Methods ============

    public List<CellData> GetCellsByType(CellType type)
    {
        List<CellData> result = new();
        foreach (var data in _occupiedCells)
        {
            if (data.type == type)
                result.Add(data);
        }
        return result;
    }

    public List<CellData> GetCellsInRadius(Vector3 worldPos, float radius)
    {
        List<CellData> result = new();
        Vector2Int centerCell = WorldToCell(worldPos);
        int cellRadius = Mathf.CeilToInt(radius / cellSize);

        for (int dy = -cellRadius; dy <= cellRadius; dy++)
        {
            for (int dx = -cellRadius; dx <= cellRadius; dx++)
            {
                Vector2Int cell = new Vector2Int(centerCell.x + dx, centerCell.y + dy);
                if (_cellMap.TryGetValue(cell, out var data))
                {
                    Vector3 cellWorldPos = CellToWorldCenter(cell);
                    if (Vector3.Distance(worldPos, cellWorldPos) <= radius)
                    {
                        result.Add(data);
                    }
                }
            }
        }

        return result;
    }

    // ============ Visualization ============
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showGrid) return;

        // Draw grid lines
        Gizmos.color = gridColor;

        // Vertical lines
        for (int x = 0; x <= width; x++)
        {
            Vector3 start = origin + new Vector3(x * cellSize, 0, 0);
            Vector3 end = origin + new Vector3(x * cellSize, 0, height * cellSize);
            Gizmos.DrawLine(start, end);
        }

        // Horizontal lines
        for (int z = 0; z <= height; z++)
        {
            Vector3 start = origin + new Vector3(0, 0, z * cellSize);
            Vector3 end = origin + new Vector3(width * cellSize, 0, z * cellSize);
            Gizmos.DrawLine(start, end);
        }

        // Draw occupied cells
        foreach (var data in _occupiedCells)
        {
            Color cellColor = data.type switch
            {
                CellType.Base => baseCellColor,
                CellType.CombatZone => occupiedCellColor,
                CellType.Threat => new Color(1f, 0.5f, 0f, 0.5f),
                CellType.Cleared => new Color(0f, 0.5f, 1f, 0.3f),
                _ => occupiedCellColor
            };

            Gizmos.color = cellColor;
            Vector3 center = CellToWorldCenter(data.cell);
            Vector3 size = new Vector3(cellSize * 0.9f, 0.1f, cellSize * 0.9f);
            Gizmos.DrawCube(center, size);

            // Draw label
            UnityEditor.Handles.Label(center + Vector3.up * 0.5f, data.type.ToString());
        }
    }
#endif

    // ============ Debug ============
#if UNITY_EDITOR
    [ContextMenu("Debug: Print All Cells")]
    private void DebugPrintAllCells()
    {
        Debug.Log($"[WorldMapGrid] Total occupied cells: {_occupiedCells.Count}");
        foreach (var data in _occupiedCells)
        {
            Debug.Log($"  Cell {data.cell}: Type={data.type}, DataId={data.dataId}");
        }
    }

    [ContextMenu("Debug: Clear All Cells")]
    private void DebugClearAllCells()
    {
        if (!Application.isPlaying) return;
        ClearAllCells();
        Debug.Log("[WorldMapGrid] All cells cleared");
    }
#endif
}