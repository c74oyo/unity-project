using UnityEngine;

/// <summary>
/// 修复版 WalkwayTile
/// 新增：NavMesh 重建后通知 WorkforceManager 刷新生成点验证
/// </summary>
public class WalkwayTile : MonoBehaviour
{
    [Tooltip("If true, will request navmesh rebuild on Start (useful when spawned at runtime).")]
    public bool rebuildOnStart = true;

    void Start()
    {
        if (!rebuildOnStart) return;

        RequestNavMeshRebuild();
    }

    void OnDestroy()
    {
        // When removed (e.g., demolished), rebuild as well
        RequestNavMeshRebuild();
    }

    private void RequestNavMeshRebuild()
    {
        if (NavMeshRebuildScheduler.Instance != null)
        {
            NavMeshRebuildScheduler.Instance.RequestWorkerRebuild();
        }

        // 【新增】NavMesh 重建后，通知 WorkforceManager 刷新生成点
        // 使用延迟调用，确保 NavMesh 重建完成后再刷新
        if (WorkforceManager.Instance != null)
        {
            // NavMesh 重建有 debounce，所以这里也延迟一下
            StartCoroutine(DelayedRefreshWorkforce());
        }
    }

    private System.Collections.IEnumerator DelayedRefreshWorkforce()
    {
        // 等待 NavMesh 重建完成（稍微比 debounce 时间长一点）
        float waitTime = 0.5f;
        if (NavMeshRebuildScheduler.Instance != null)
            waitTime = NavMeshRebuildScheduler.Instance.debounceSeconds + 0.2f;

        yield return new WaitForSeconds(waitTime);

        if (WorkforceManager.Instance != null)
            WorkforceManager.Instance.RefreshSpawnPointValidation();
    }
}