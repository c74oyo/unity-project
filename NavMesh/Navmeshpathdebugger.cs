using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// NavMesh 路径调试工具
/// 挂载到场景中任意物体上，用于诊断工人寻路问题
/// </summary>
public class NavMeshPathDebugger : MonoBehaviour
{
    [Header("Test Points")]
    [Tooltip("起点（拖入宿舍的 SpawnPoint 或工人位置）")]
    public Transform startPoint;

    [Tooltip("终点（拖入 Worksite 的 EntrancePoint）")]
    public Transform endPoint;

    [Header("NavMesh Settings")]
    [Tooltip("Agent Type ID（-1 = 自动检测）")]
    public int agentTypeID = -1;

    [Tooltip("Area 名称（如 'Walkway'），留空用 AllAreas")]
    public string areaName = "Walkway";

    [Header("Sample Settings")]
    public float sampleRadius = 10f;

    [Header("Results (Runtime)")]
    [SerializeField] private bool _startOnNavMesh;
    [SerializeField] private bool _endOnNavMesh;
    [SerializeField] private bool _pathExists;
    [SerializeField] private NavMeshPathStatus _pathStatus;
    [SerializeField] private string _diagnosis = "";

    [Header("Auto Test")]
    public bool autoTestEveryFrame = false;

    private NavMeshPath _path;
    private Vector3 _sampledStart;
    private Vector3 _sampledEnd;

    private void Start()
    {
        _path = new NavMeshPath();
        TestPath();
    }

    private void Update()
    {
        if (autoTestEveryFrame)
            TestPath();
    }

    [ContextMenu("Test Path Now")]
    public void TestPath()
    {
        if (startPoint == null || endPoint == null)
        {
            _diagnosis = "Please assign startPoint and endPoint";
            return;
        }

        _path ??= new NavMeshPath();

        // 解析 Area
        int areaMask = NavMesh.AllAreas;
        if (!string.IsNullOrEmpty(areaName))
        {
            int areaIndex = NavMesh.GetAreaFromName(areaName);
            if (areaIndex >= 0)
                areaMask = 1 << areaIndex;
            else
                Debug.LogWarning($"[PathDebug] Area '{areaName}' not found, using AllAreas");
        }

        var filter = new NavMeshQueryFilter
        {
            agentTypeID = agentTypeID >= 0 ? agentTypeID : 0,
            areaMask = areaMask
        };

        // 测试起点
        _startOnNavMesh = NavMesh.SamplePosition(startPoint.position, out var startHit, sampleRadius, filter);
        _sampledStart = _startOnNavMesh ? startHit.position : startPoint.position;

        // 测试终点
        _endOnNavMesh = NavMesh.SamplePosition(endPoint.position, out var endHit, sampleRadius, filter);
        _sampledEnd = _endOnNavMesh ? endHit.position : endPoint.position;

        // 生成诊断信息
        System.Text.StringBuilder sb = new();
        sb.AppendLine($"=== NavMesh Path Diagnosis ===");
        sb.AppendLine($"AgentTypeID: {filter.agentTypeID}");
        sb.AppendLine($"AreaMask: {filter.areaMask} ({areaName})");
        sb.AppendLine($"Sample Radius: {sampleRadius}");
        sb.AppendLine();

        sb.AppendLine($"[Start Point]");
        sb.AppendLine($"  Original: {startPoint.position}");
        sb.AppendLine($"  On NavMesh: {_startOnNavMesh}");
        if (_startOnNavMesh)
            sb.AppendLine($"  Sampled: {_sampledStart} (dist: {Vector3.Distance(startPoint.position, _sampledStart):F2}m)");

        sb.AppendLine();
        sb.AppendLine($"[End Point]");
        sb.AppendLine($"  Original: {endPoint.position}");
        sb.AppendLine($"  On NavMesh: {_endOnNavMesh}");
        if (_endOnNavMesh)
            sb.AppendLine($"  Sampled: {_sampledEnd} (dist: {Vector3.Distance(endPoint.position, _sampledEnd):F2}m)");

        // 测试路径
        _pathExists = false;
        _pathStatus = NavMeshPathStatus.PathInvalid;

        if (_startOnNavMesh && _endOnNavMesh)
        {
            _pathExists = NavMesh.CalculatePath(_sampledStart, _sampledEnd, filter, _path);
            _pathStatus = _path.status;

            sb.AppendLine();
            sb.AppendLine($"[Path Result]");
            sb.AppendLine($"  CalculatePath returned: {_pathExists}");
            sb.AppendLine($"  Path Status: {_pathStatus}");
            sb.AppendLine($"  Corner Count: {_path.corners.Length}");
        }

        sb.AppendLine();
        sb.AppendLine("=== Diagnosis ===");

        if (!_startOnNavMesh && !_endOnNavMesh)
            sb.AppendLine("❌ BOTH points are NOT on NavMesh!");
        else if (!_startOnNavMesh)
            sb.AppendLine("❌ START point is NOT on NavMesh!");
        else if (!_endOnNavMesh)
            sb.AppendLine("❌ END point is NOT on NavMesh!");
        else if (!_pathExists || _pathStatus != NavMeshPathStatus.PathComplete)
            sb.AppendLine("❌ Points are on NavMesh but NO PATH between them!\n   → NavMesh may be disconnected (missing walkway tiles?)");
        else
            sb.AppendLine("✓ Path is VALID!");

        // 额外诊断：用 AllAreas 测试
        if (!_startOnNavMesh || !_endOnNavMesh)
        {
            sb.AppendLine();
            sb.AppendLine("[Additional Check with AllAreas]");

            bool startOnAny = NavMesh.SamplePosition(startPoint.position, out var anyStart, sampleRadius, NavMesh.AllAreas);
            bool endOnAny = NavMesh.SamplePosition(endPoint.position, out var anyEnd, sampleRadius, NavMesh.AllAreas);

            if (!_startOnNavMesh && startOnAny)
                sb.AppendLine($"  → Start IS on NavMesh, but wrong Area! Found at {anyStart.position}");
            if (!_endOnNavMesh && endOnAny)
                sb.AppendLine($"  → End IS on NavMesh, but wrong Area! Found at {anyEnd.position}");

            if (startOnAny && endOnAny)
            {
                var anyPath = new NavMeshPath();
                bool anyPathExists = NavMesh.CalculatePath(anyStart.position, anyEnd.position, NavMesh.AllAreas, anyPath);
                sb.AppendLine($"  → Path with AllAreas: {anyPathExists}, Status: {anyPath.status}");

                if (anyPathExists && anyPath.status == NavMeshPathStatus.PathComplete)
                    sb.AppendLine("  → Path EXISTS with AllAreas but not with your Area filter!");
            }
        }

        _diagnosis = sb.ToString();
        Debug.Log(_diagnosis);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (startPoint == null || endPoint == null) return;

        // 起点
        Gizmos.color = _startOnNavMesh ? Color.green : Color.red;
        Gizmos.DrawWireSphere(startPoint.position, 0.5f);
        Gizmos.DrawLine(startPoint.position, startPoint.position + Vector3.up * 2);

        if (_startOnNavMesh && Vector3.Distance(startPoint.position, _sampledStart) > 0.1f)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(_sampledStart, 0.3f);
            Gizmos.DrawLine(startPoint.position, _sampledStart);
        }

        // 终点
        Gizmos.color = _endOnNavMesh ? Color.green : Color.red;
        Gizmos.DrawWireSphere(endPoint.position, 0.5f);
        Gizmos.DrawLine(endPoint.position, endPoint.position + Vector3.up * 2);

        if (_endOnNavMesh && Vector3.Distance(endPoint.position, _sampledEnd) > 0.1f)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(_sampledEnd, 0.3f);
            Gizmos.DrawLine(endPoint.position, _sampledEnd);
        }

        // 路径
        if (_path != null && _path.corners.Length > 0)
        {
            Gizmos.color = _pathStatus == NavMeshPathStatus.PathComplete ? Color.green : Color.yellow;
            for (int i = 0; i < _path.corners.Length - 1; i++)
            {
                Gizmos.DrawLine(_path.corners[i], _path.corners[i + 1]);
                Gizmos.DrawSphere(_path.corners[i], 0.15f);
            }
            if (_path.corners.Length > 0)
                Gizmos.DrawSphere(_path.corners[^1], 0.15f);
        }

        // 采样半径
        Gizmos.color = new Color(1, 1, 0, 0.1f);
        Gizmos.DrawWireSphere(startPoint.position, sampleRadius);
        Gizmos.DrawWireSphere(endPoint.position, sampleRadius);

        // 标签
        UnityEditor.Handles.Label(startPoint.position + Vector3.up * 2.2f,
            _startOnNavMesh ? "START ✓" : "START ✗");
        UnityEditor.Handles.Label(endPoint.position + Vector3.up * 2.2f,
            _endOnNavMesh ? "END ✓" : "END ✗");
    }
#endif
}