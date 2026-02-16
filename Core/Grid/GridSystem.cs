using System.Collections.Generic;
using UnityEngine;

public class GridSystem : MonoBehaviour
{
    [Header("Grid Area")]
    public Vector3 origin = Vector3.zero;   // 建造区域左下角（X/Z）
    public float cellSize = 1f;             // 1m per cell
    public int width = 100;                  // X 方向格子数
    public int height = 100;                 // Z 方向格子数

    // 每个格子 -> 占用根物体
    private readonly Dictionary<Vector2Int, GameObject> occupied = new();

    public bool IsInBounds(Vector2Int cell)
        => cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height;

    public Vector2Int WorldToCell(Vector3 world)
    {
        Vector3 local = world - origin;
        int x = Mathf.FloorToInt(local.x / cellSize);
        int z = Mathf.FloorToInt(local.z / cellSize);
        return new Vector2Int(x, z);
    }

    public Vector3 CellToWorldCenter(Vector2Int cell)
    {
        return origin + new Vector3((cell.x + 0.5f) * cellSize, 0f, (cell.y + 0.5f) * cellSize);
    }

    public IEnumerable<Vector2Int> GetFootprintCells(Vector2Int anchorCell, Vector2Int size, int rot90)
    {
        bool swap = (rot90 % 2) != 0;
        int w = swap ? size.y : size.x;
        int h = swap ? size.x : size.y;

        for (int dz = 0; dz < h; dz++)
        for (int dx = 0; dx < w; dx++)
            yield return new Vector2Int(anchorCell.x + dx, anchorCell.y + dz);
    }

    public bool CanPlace(Vector2Int anchorCell, Vector2Int size, int rot90)
    {
        foreach (var c in GetFootprintCells(anchorCell, size, rot90))
        {
            if (!IsInBounds(c)) return false;
            if (occupied.ContainsKey(c)) return false;
        }
        return true;
    }

    public void Occupy(Vector2Int anchorCell, Vector2Int size, int rot90, GameObject root)
    {
        foreach (var c in GetFootprintCells(anchorCell, size, rot90))
        {
            if (!IsInBounds(c))
            {
                Debug.LogWarning($"[GridSystem] Attempting to occupy out-of-bounds cell {c}. Skipping.", root);
                continue;
            }
            occupied[c] = root;
        }
    }

    /// <summary>释放一块占地（只释放仍指向同一 root 的格子，避免误删）。</summary>
    public void Release(Vector2Int anchorCell, Vector2Int size, int rot90, GameObject root)
    {
        foreach (var c in GetFootprintCells(anchorCell, size, rot90))
        {
            if (occupied.TryGetValue(c, out var cur) && cur == root)
                occupied.Remove(c);
        }
    }

    /// <summary>按格子查询占用根对象。</summary>
    public bool TryGetOccupiedRoot(Vector2Int cell, out GameObject root)
        => occupied.TryGetValue(cell, out root);

    /// <summary>
    /// 没有 BuildableInstance 时的兜底：释放所有指向该 root 的格子。
    /// </summary>
    public void ReleaseByRoot(GameObject root)
    {
        if (root == null) return;

        var toRemove = new List<Vector2Int>();
        foreach (var kv in occupied)
        {
            if (kv.Value == root)
                toRemove.Add(kv.Key);
        }

        for (int i = 0; i < toRemove.Count; i++)
            occupied.Remove(toRemove[i]);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.gray;
        for (int z = 0; z < height; z++)
        for (int x = 0; x < width; x++)
        {
            Vector3 p = CellToWorldCenter(new Vector2Int(x, z));
            p.y = origin.y;
            Gizmos.DrawWireCube(p, new Vector3(cellSize, 0.02f, cellSize));
        }
    }
#endif
}
