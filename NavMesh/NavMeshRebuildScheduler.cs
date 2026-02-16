using System.Collections;
using UnityEngine;
using Unity.AI.Navigation;


public class NavMeshRebuildScheduler : MonoBehaviour
{
    public static NavMeshRebuildScheduler Instance { get; private set; }

    [Header("Surfaces to rebuild")]
    public NavMeshSurface workerSurface;

    [Header("Rebuild Settings")]
    [Tooltip("Multiple requests within this time window will be merged into a single rebuild.")]
    public float debounceSeconds = 0.3f;

    private bool pending;
    private Coroutine routine;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>Request a worker navmesh rebuild. Calls are merged by debounceSeconds.</summary>
    public void RequestWorkerRebuild()
    {
        if (workerSurface == null)
        {
            Debug.LogError("[NavMeshRebuildScheduler] workerSurface is not assigned.");
            return;
        }

        pending = true;

        if (routine == null)
            routine = StartCoroutine(RebuildRoutine());
    }

    private IEnumerator RebuildRoutine()
    {
        while (true)
        {
            // wait for merge window
            yield return new WaitForSeconds(debounceSeconds);

            if (!pending)
            {
                routine = null;
                yield break;
            }

            pending = false;

            // Rebuild worker navmesh (runtime-safe)
            workerSurface.RemoveData();
            workerSurface.BuildNavMesh();

            // Continue loop in case new requests came in during build
        }
    }
}
