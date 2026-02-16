using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class TradeTruck : MonoBehaviour
{
    private enum State
    {
        ToEnterYard,
        ToDock,
        Loading,
        ToExitYard,
        ToHighwayDespawn,
    }

    private DockYard _yard;
    private NavMeshAgent _agent;

    private Transform _enterYard;
    private Transform _dockPoint;
    private Transform _exitYard;
    private Transform _highwayDespawn;

    private float _loadingSeconds;
    private State _state;

    [Header("Arrival")]
    public float arriveDistance = 0.4f;

    [Header("NavMesh Sample")]
    public float sampleRadius = 8f; // 你之前用 8 比较稳

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
    }

    public void Init(
        DockYard yard,
        Transform waypointEnterYard,
        Transform dockPoint,
        Transform waypointExitYard,
        Transform highwayDespawnPoint,
        float loadingSeconds
    )
    {
        _yard = yard;
        _enterYard = waypointEnterYard;
        _dockPoint = dockPoint;
        _exitYard = waypointExitYard;
        _highwayDespawn = highwayDespawnPoint;
        _loadingSeconds = loadingSeconds;

        // 基本保护：点位没填，直接不跑
        if (_enterYard == null || _dockPoint == null || _exitYard == null || _highwayDespawn == null)
        {
            Debug.LogError("[TradeTruck] Init failed: one or more waypoint Transforms are null.");
            enabled = false;
            return;
        }

        // 关键：确保车本体在 NavMesh 上，否则 isOnNavMesh=false 会导致“锁死”
        if (!_agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out var hit, sampleRadius, NavMesh.AllAreas))
            {
                _agent.Warp(hit.position);
            }
            else
            {
                Debug.LogError($"[TradeTruck] Spawn not on NavMesh and SamplePosition failed. pos={transform.position}, r={sampleRadius}");
                enabled = false;
                return;
            }
        }

        // 起步：去码头入口
        _state = State.ToEnterYard;
        GoTo(_enterYard.position, "EnterYard");
    }

    private void Update()
    {
        if (_agent == null) return;

        // 只有在 NavMesh 上才能移动
        if (!_agent.isOnNavMesh) return;

        if (_agent.pathPending) return;

        if (_agent.remainingDistance > Mathf.Max(arriveDistance, _agent.stoppingDistance))
            return;

        // 到达当前目标
        switch (_state)
        {
            case State.ToEnterYard:
                _state = State.ToDock;
                GoTo(_dockPoint.position, "DockPoint");
                break;

            case State.ToDock:
                _yard?.NotifyTruckArrived(this);
                _state = State.Loading;
                StartCoroutine(CoLoading());
                break;

            case State.Loading:
                // 等协程结束
                break;

            case State.ToExitYard:
                _state = State.ToHighwayDespawn;
                GoTo(_highwayDespawn.position, "HighwayDespawn");
                break;

            case State.ToHighwayDespawn:
                _yard?.NotifyTruckExited(this);
                Destroy(gameObject);
                break;
        }
    }

    private IEnumerator CoLoading()
    {
        yield return new WaitForSeconds(_loadingSeconds);

        _yard?.NotifyTruckFinishedLoading(this);

        _state = State.ToExitYard;
        GoTo(_exitYard.position, "ExitYard");
    }

    // 给 label 默认值：以后你想写 GoTo(pos) 也不会再报 CS7036
    private void GoTo(Vector3 worldPos, string label = "GoTo")
    {
        if (!_agent || !_agent.isOnNavMesh) return;

        if (NavMesh.SamplePosition(worldPos, out var hit, sampleRadius, NavMesh.AllAreas))
        {
            _agent.SetDestination(hit.position);
        }
        else
        {
            Debug.LogWarning($"[TradeTruck] SamplePosition failed: {label}, pos={worldPos}, radius={sampleRadius}");
            _agent.SetDestination(worldPos); // 兜底（但如果不在 NavMesh 上依然可能失败）
        }
    }
}
