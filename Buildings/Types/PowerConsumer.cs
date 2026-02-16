using UnityEngine;

public class PowerConsumer : MonoBehaviour
{
    [Min(0f)] public float demand = 2f;

    [Tooltip("数字越小优先级越高（1最高）")]
    [Min(1)] public int priority = 5;

    [Header("Runtime (read only)")]
    public bool hasPower;
    public float allocated;

    public float Demand => Mathf.Max(0f, demand);

    private void OnEnable()
    {
        if (PowerManager.Instance != null)
            PowerManager.Instance.RegisterConsumer(this);
    }

    private void OnDisable()
    {
        if (PowerManager.Instance != null)
            PowerManager.Instance.UnregisterConsumer(this);
    }

    // 兼容：有些脚本用 HasPower
    public bool HasPower => hasPower;

    // 兼容：有些脚本用 Allocated
    public float Allocated => allocated;
}
