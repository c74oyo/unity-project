using UnityEngine;

public class DormBuilding : MonoBehaviour
{
    [Header("Spawn/Return")]
    public Transform spawnPoint;

    [Header("Capacity")]
    [Min(0)] public int capacity = 10;

    public Vector3 GetSpawnPos()
    {
        return spawnPoint != null ? spawnPoint.position : transform.position;
    }
}
