using UnityEngine;

namespace Game.Building
{
    [DisallowMultipleComponent]
    public class HighwayManager : MonoBehaviour
    {
        public static HighwayManager Instance { get; private set; }

        [Header("Global Highway Points (Scene)")]
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Transform despawnPoint;

        public Transform SpawnPoint => spawnPoint;
        public Transform DespawnPoint => despawnPoint;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            if (spawnPoint) Gizmos.DrawSphere(spawnPoint.position, 0.4f);

            Gizmos.color = Color.magenta;
            if (despawnPoint) Gizmos.DrawSphere(despawnPoint.position, 0.4f);
        }
#endif
    }
}
