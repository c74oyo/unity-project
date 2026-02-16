using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 修复版 WorkerUnit
/// 主要修复：增加可配置的 NavMesh 采样半径
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class WorkerUnit : MonoBehaviour
{
    public enum WorkerState
    {
        Idle,
        WalkingToWork,
        Working,
        StuckNoPath,
    }

    [Header("Refs")]
    public NavMeshAgent agent;
    public Animator animator;

    [Header("State (runtime)")]
    public WorkerState state = WorkerState.Idle;

    [Header("NavMesh Settings")]
    [Tooltip("目标点采样半径（由 WorkforceManager 设置，或手动配置）")]
    public float navSampleRadius = 5f;  // 【修复】从固定的 2f 改为可配置，默认 5f

    private DormBuilding _home;
    private Worksite _targetWorksite;
    private float _arriveDistance = 0.6f;

    private Renderer[] _renderers;
    private Collider[] _colliders;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        _renderers = GetComponentsInChildren<Renderer>(true);
        _colliders = GetComponentsInChildren<Collider>(true);
    }

    public void InitHome(DormBuilding dorm)
    {
        _home = dorm;
    }

    public void Unhide()
    {
        if (_renderers != null) 
            foreach (var r in _renderers) 
                if (r) r.enabled = true;
        
        if (_colliders != null) 
            foreach (var c in _colliders) 
                if (c) c.enabled = true;
        
        if (agent != null) 
            agent.enabled = true;
    }

    public void HideForWork()
    {
        if (_renderers != null) 
            foreach (var r in _renderers) 
                if (r) r.enabled = false;
        
        if (_colliders != null) 
            foreach (var c in _colliders) 
                if (c) c.enabled = false;

        if (agent != null) 
            agent.ResetPath();
    }

    public bool AssignToWorksite(Worksite ws)
    {
        if (ws == null || !ws.HasEntrance) 
        {
            Debug.LogWarning($"[WorkerUnit] AssignToWorksite failed: ws={ws}, HasEntrance={ws?.HasEntrance}");
            return false;
        }

        _targetWorksite = ws;
        _arriveDistance = Mathf.Max(0.1f, ws.arriveDistance);

        if (agent == null) 
        {
            Debug.LogWarning("[WorkerUnit] AssignToWorksite failed: agent is null");
            return false;
        }

        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning($"[WorkerUnit] AssignToWorksite failed: agent not on NavMesh. " +
                           $"Position={transform.position}, AgentTypeID={agent.agentTypeID}, AreaMask={agent.areaMask}");
            state = WorkerState.StuckNoPath;
            return false;
        }

        // 构建过滤器
        var filter = new NavMeshQueryFilter
        {
            agentTypeID = agent.agentTypeID,
            areaMask = agent.areaMask
        };

        Vector3 entrancePos = ws.EntrancePos;

        // 【修复】使用可配置的采样半径
        float sampleRadius = Mathf.Max(navSampleRadius, 2f);

        if (!NavMesh.SamplePosition(entrancePos, out var hit, sampleRadius, filter))
        {
            Debug.LogWarning($"[WorkerUnit] Cannot sample entrance position.\n" +
                           $"  Entrance: {entrancePos}\n" +
                           $"  Sample radius: {sampleRadius}\n" +
                           $"  AgentTypeID: {filter.agentTypeID}\n" +
                           $"  AreaMask: {filter.areaMask}\n" +
                           $"  Worksite: {ws.name}");

            // 【调试】尝试用 AllAreas 看看
            if (NavMesh.SamplePosition(entrancePos, out var anyHit, sampleRadius, NavMesh.AllAreas))
            {
                Debug.LogWarning($"[WorkerUnit] Found NavMesh at {anyHit.position} with AllAreas. " +
                               "The entrance may be on wrong NavMesh Area.");
            }

            state = WorkerState.StuckNoPath;
            return false;
        }

        // 计算路径
        var path = new NavMeshPath();
        bool pathValid = agent.CalculatePath(hit.position, path) && 
                        path.status == NavMeshPathStatus.PathComplete;

        if (!pathValid)
        {
            Debug.LogWarning($"[WorkerUnit] Path calculation failed.\n" +
                           $"  From: {transform.position}\n" +
                           $"  To: {hit.position}\n" +
                           $"  Path status: {path.status}\n" +
                           $"  Worksite: {ws.name}");
            state = WorkerState.StuckNoPath;
            return false;
        }

        Unhide();
        state = WorkerState.WalkingToWork;
        agent.SetDestination(hit.position);

        return true;
    }

    private void Update()
    {
        UpdateAnimator();

        if (_targetWorksite == null) return;
        if (agent == null) return;
        if (!agent.isOnNavMesh) return;
        if (agent.pathPending) return;

        if (agent.remainingDistance > Mathf.Max(_arriveDistance, agent.stoppingDistance))
            return;

        // 到岗
        state = WorkerState.Working;
        agent.ResetPath();

        _targetWorksite.NotifyArrived(this);
        _targetWorksite = null;
    }

    private void UpdateAnimator()
    {
        if (animator == null || agent == null) return;

        animator.SetBool("IsMoving", state == WorkerState.WalkingToWork);
        animator.SetBool("IsWorking", state == WorkerState.Working);
        animator.SetFloat("Speed", agent.velocity.magnitude);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // 显示当前位置和状态
        Gizmos.color = state switch
        {
            WorkerState.Idle => Color.gray,
            WorkerState.WalkingToWork => Color.green,
            WorkerState.Working => Color.blue,
            WorkerState.StuckNoPath => Color.red,
            _ => Color.white
        };
        Gizmos.DrawWireSphere(transform.position, 0.3f);

        // 显示目标
        if (_targetWorksite != null && _targetWorksite.HasEntrance)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, _targetWorksite.EntrancePos);
            Gizmos.DrawWireSphere(_targetWorksite.EntrancePos, 0.2f);
        }

        // 显示采样半径
        if (state == WorkerState.Idle)
        {
            Gizmos.color = new Color(0, 1, 1, 0.1f);
            Gizmos.DrawWireSphere(transform.position, navSampleRadius);
        }
    }
#endif
}