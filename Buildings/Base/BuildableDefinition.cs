using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Building/Buildable Definition")]
public class BuildableDefinition : ScriptableObject
{
    public string displayName;
    public GameObject prefab;
    public Vector2Int size = Vector2Int.one;

    [Header("Economy")]
    public List<ResourceAmount> buildCost = new();

    [Range(0f, 1f)]
    public float refundPercent = 0.5f; // 拆除返还比例：默认 50%

    // 可选：以后你要区分“道路/建筑”对 NavMesh 的影响
    public bool affectsNavmesh = false;
}
