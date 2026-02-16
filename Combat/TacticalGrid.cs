using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战术网格 - 管理30x30战斗地图的网格状态、BFS寻路、单位占位
/// </summary>
public class TacticalGrid : MonoBehaviour
{
    // ============ Grid Data ============

    private int _width;
    private int _height;
    private float _cellSize = 1f;
    private CellState[,] _cells;

    // 单位占位映射：cell -> unit
    private Dictionary<Vector2Int, TacticalUnit> _unitMap = new();

    // ============ Properties ============

    public int Width => _width;
    public int Height => _height;
    public float CellSize => _cellSize;

    // ============ Initialization ============

    /// <summary>
    /// 根据 BattleConfig 初始化网格
    /// </summary>
    public void Initialize(BattleConfig config)
    {
        _width = config.gridSize.x;
        _height = config.gridSize.y;
        _cellSize = config.cellSize;

        _cells = new CellState[_width, _height];
        _unitMap.Clear();

        // 所有格子初始化为 Empty
        for (int x = 0; x < _width; x++)
            for (int y = 0; y < _height; y++)
                _cells[x, y] = CellState.Empty;

        // 放置障碍物
        if (config.obstacles != null)
        {
            foreach (var obs in config.obstacles)
            {
                if (IsInBounds(obs))
                    _cells[obs.x, obs.y] = CellState.Blocked;
            }
        }
    }

    // ============ Coordinate Conversion ============

    /// <summary>
    /// 世界坐标 → 网格坐标
    /// </summary>
    public Vector2Int WorldToCell(Vector3 worldPos)
    {
        int x = Mathf.FloorToInt(worldPos.x / _cellSize);
        int y = Mathf.FloorToInt(worldPos.z / _cellSize);
        return new Vector2Int(x, y);
    }

    /// <summary>
    /// 网格坐标 → 世界坐标（格子中心）
    /// </summary>
    public Vector3 CellToWorldCenter(Vector2Int cell)
    {
        float x = (cell.x + 0.5f) * _cellSize;
        float z = (cell.y + 0.5f) * _cellSize;
        return new Vector3(x, 0f, z);
    }

    /// <summary>
    /// 坐标是否在网格范围内
    /// </summary>
    public bool IsInBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < _width && cell.y >= 0 && cell.y < _height;
    }

    // ============ Cell State ============

    /// <summary>
    /// 获取格子状态
    /// </summary>
    public CellState GetCellState(Vector2Int cell)
    {
        if (!IsInBounds(cell)) return CellState.Blocked;
        return _cells[cell.x, cell.y];
    }

    /// <summary>
    /// 格子是否可通行（Empty 才可走）
    /// </summary>
    public bool IsWalkable(Vector2Int cell)
    {
        return IsInBounds(cell) && _cells[cell.x, cell.y] == CellState.Empty;
    }

    /// <summary>
    /// 获取格子上的单位
    /// </summary>
    public TacticalUnit GetUnitAt(Vector2Int cell)
    {
        _unitMap.TryGetValue(cell, out var unit);
        return unit;
    }

    // ============ Unit Placement ============

    /// <summary>
    /// 放置单位到指定格子
    /// </summary>
    public void PlaceUnit(Vector2Int cell, TacticalUnit unit)
    {
        if (!IsInBounds(cell)) return;

        _unitMap[cell] = unit;
        _cells[cell.x, cell.y] = unit.Team == CombatTeam.Player
            ? CellState.OccupiedByPlayer
            : CellState.OccupiedByEnemy;
    }

    /// <summary>
    /// 移除格子上的单位
    /// </summary>
    public void RemoveUnit(Vector2Int cell)
    {
        if (!IsInBounds(cell)) return;

        _unitMap.Remove(cell);
        if (_cells[cell.x, cell.y] != CellState.Blocked)
            _cells[cell.x, cell.y] = CellState.Empty;
    }

    /// <summary>
    /// 移动单位从一个格子到另一个格子
    /// </summary>
    public void MoveUnit(Vector2Int from, Vector2Int to, TacticalUnit unit)
    {
        RemoveUnit(from);
        PlaceUnit(to, unit);
    }

    // ============ BFS Pathfinding ============

    // 四方向偏移
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up,     // (0, 1)
        Vector2Int.down,   // (0, -1)
        Vector2Int.left,   // (-1, 0)
        Vector2Int.right   // (1, 0)
    };

    /// <summary>
    /// BFS 获取从 origin 出发在 range 步内可到达的所有格子
    /// 返回 parentMap：key=可达格子, value=来源格子（用于路径重建）
    /// origin 本身也包含在结果中（parent 指向自身）
    /// </summary>
    public Dictionary<Vector2Int, Vector2Int> GetReachableCells(Vector2Int origin, int range)
    {
        var parentMap = new Dictionary<Vector2Int, Vector2Int>();
        var distMap = new Dictionary<Vector2Int, int>();
        var queue = new Queue<Vector2Int>();

        parentMap[origin] = origin;
        distMap[origin] = 0;
        queue.Enqueue(origin);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            int currentDist = distMap[current];

            if (currentDist >= range) continue;

            foreach (var dir in Directions)
            {
                var neighbor = current + dir;

                // 已访问过
                if (parentMap.ContainsKey(neighbor)) continue;

                // 不可通行
                if (!IsInBounds(neighbor)) continue;
                var state = _cells[neighbor.x, neighbor.y];
                if (state == CellState.Blocked) continue;

                // 被单位占据的格子：可以寻路通过（计算路径），但不能停留
                // 这里先加入BFS（允许穿过友军），最后过滤掉被占据的格子
                if (state == CellState.OccupiedByPlayer || state == CellState.OccupiedByEnemy)
                {
                    // 允许经过但不允许停留 — 加入BFS扩展但标记
                    distMap[neighbor] = currentDist + 1;
                    parentMap[neighbor] = current;
                    queue.Enqueue(neighbor);
                    continue;
                }

                distMap[neighbor] = currentDist + 1;
                parentMap[neighbor] = current;
                queue.Enqueue(neighbor);
            }
        }

        // 移除被占据的格子（不能停留在有单位的格子上），但保留 origin
        var toRemove = new List<Vector2Int>();
        foreach (var kvp in parentMap)
        {
            if (kvp.Key == origin) continue;
            var state = _cells[kvp.Key.x, kvp.Key.y];
            if (state == CellState.OccupiedByPlayer || state == CellState.OccupiedByEnemy)
                toRemove.Add(kvp.Key);
        }
        foreach (var cell in toRemove)
            parentMap.Remove(cell);

        return parentMap;
    }

    /// <summary>
    /// 根据 parentMap 重建从 start 到 target 的路径
    /// 返回的路径包含 start 和 target
    /// </summary>
    public List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> parentMap,
                                              Vector2Int start, Vector2Int target)
    {
        var path = new List<Vector2Int>();

        if (!parentMap.ContainsKey(target)) return path;

        var current = target;
        while (current != start)
        {
            path.Add(current);
            current = parentMap[current];
        }
        path.Add(start);
        path.Reverse();
        return path;
    }

    // ============ Attack Range ============

    /// <summary>
    /// 获取从 origin 出发的攻击范围格子（曼哈顿距离）
    /// </summary>
    public HashSet<Vector2Int> GetAttackRangeCells(Vector2Int origin, int attackRange)
    {
        var result = new HashSet<Vector2Int>();

        for (int dx = -attackRange; dx <= attackRange; dx++)
        {
            for (int dy = -attackRange; dy <= attackRange; dy++)
            {
                if (Mathf.Abs(dx) + Mathf.Abs(dy) > attackRange) continue;
                if (dx == 0 && dy == 0) continue; // 不包含自身

                var cell = new Vector2Int(origin.x + dx, origin.y + dy);
                if (IsInBounds(cell))
                    result.Add(cell);
            }
        }

        return result;
    }

    /// <summary>
    /// 获取攻击范围内可攻击的敌方单位
    /// </summary>
    public List<TacticalUnit> GetAttackableEnemies(Vector2Int origin, int attackRange, CombatTeam attackerTeam)
    {
        var result = new List<TacticalUnit>();
        var cells = GetAttackRangeCells(origin, attackRange);

        foreach (var cell in cells)
        {
            if (_unitMap.TryGetValue(cell, out var unit))
            {
                if (unit != null && unit.IsAlive && unit.Team != attackerTeam)
                    result.Add(unit);
            }
        }

        return result;
    }

    // ============ Queries ============

    /// <summary>
    /// 获取所有指定阵营的存活单位格子
    /// </summary>
    public List<Vector2Int> GetAllUnitCells(CombatTeam team)
    {
        var result = new List<Vector2Int>();
        foreach (var kvp in _unitMap)
        {
            if (kvp.Value != null && kvp.Value.IsAlive && kvp.Value.Team == team)
                result.Add(kvp.Key);
        }
        return result;
    }

    /// <summary>
    /// 计算两点间的曼哈顿距离
    /// </summary>
    public static int ManhattanDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}
