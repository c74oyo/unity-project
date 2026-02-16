using UnityEngine;

[DisallowMultipleComponent]
public class PowerGenerator : MonoBehaviour
{
    [Header("Base Supply")]
    [Min(0f)] public float supply = 10f;

    [Header("Base Range")]
    [Min(0f)] public float range = 30f;

    [Tooltip("距离衰减（x=0是近处，x=1是range边缘；y是保留比例）")]
    public AnimationCurve falloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [Header("Workers Boost (optional)")]
    public bool useWorkerBoost = true;

    [Min(0)] public int assignedWorkers = 0;
    [Min(0)] public int maxWorkers = 5;

    [Range(0f, 5f)] public float supplyMulWithoutWorkers = 1f;
    [Range(0f, 5f)] public float supplyMulAtFullWorkers = 1f;
    [Range(0f, 5f)] public float rangeMulWithoutWorkers = 1f;
    [Range(0f, 5f)] public float rangeMulAtFullWorkers = 1f;

    [Header("External Modifiers (optional)")]
    [Min(0f)] public float externalSupplyMul = 1f;
    [Min(0f)] public float externalRangeMul = 1f;

    [Header("Cards (via unified modifiers)")]
    public bool useCardRadiusFromWorksite = true;
    public Worksite worksite;
    public BuildingModifiers modifiers;

    public void SetWorkersFromWorksite(int presentWorkers)
    {
        assignedWorkers = Mathf.Max(0, presentWorkers);
        ClampWorkers();
    }

    public float BaseSupply => Mathf.Max(0f, supply);
    public float BaseRange => Mathf.Max(0f, range);

    public float Worker01
    {
        get
        {
            if (!useWorkerBoost) return 0f;
            if (maxWorkers <= 0) return 0f;
            return Mathf.Clamp01(assignedWorkers / (float)maxWorkers);
        }
    }

    public float WorkerSupplyMul
        => !useWorkerBoost ? 1f : Mathf.Max(0f, Mathf.Lerp(supplyMulWithoutWorkers, supplyMulAtFullWorkers, Worker01));

    public float WorkerRangeMul
        => !useWorkerBoost ? 1f : Mathf.Max(0f, Mathf.Lerp(rangeMulWithoutWorkers, rangeMulAtFullWorkers, Worker01));

    public float CardRangeMul
    {
        get
        {
            if (!useCardRadiusFromWorksite) return 1f;

            if (modifiers == null)
                modifiers = GetComponentInParent<BuildingModifiers>() ?? GetComponentInChildren<BuildingModifiers>(true);

            if (modifiers == null)
            {
                Debug.LogWarning($"[PowerGenerator] BuildingModifiers not found on {name}. Initialize in Awake/Start instead.", this);
                return 1f;
            }

            modifiers.useCardSlots = true;
            return modifiers.GetMul(CardModifier.ModifierType.Power_RadiusMul, 0.05f);
        }
    }

    public float Supply
        => BaseSupply * WorkerSupplyMul * Mathf.Max(0f, externalSupplyMul);

    public float EffectiveRange
        => BaseRange * WorkerRangeMul * Mathf.Max(0f, externalRangeMul) * CardRangeMul;

    public float GetEffectiveSupplyAt(Vector3 worldPos)
    {
        float s = Supply;
        float r = EffectiveRange;

        if (s <= 0f) return 0f;
        if (r <= 0f) return 0f;

        float d = Vector3.Distance(transform.position, worldPos);
        if (d > r) return 0f;

        float t = Mathf.Clamp01(d / r);
        float k = Mathf.Clamp01(falloff != null ? falloff.Evaluate(t) : 1f);
        return s * k;
    }

    private void Awake()
    {
        ClampWorkers();
        if (worksite == null) worksite = GetComponentInParent<Worksite>();

        if (modifiers == null)
            modifiers = GetComponentInParent<BuildingModifiers>() ?? GetComponentInChildren<BuildingModifiers>(true);
    }

    private void OnEnable()
    {
        if (PowerManager.Instance != null)
            PowerManager.Instance.RegisterGenerator(this);
    }

    private void OnDisable()
    {
        if (PowerManager.Instance != null)
            PowerManager.Instance.UnregisterGenerator(this);
    }

    private void OnValidate()
    {
        ClampWorkers();
        if (worksite == null) worksite = GetComponentInParent<Worksite>();
    }

    private void ClampWorkers()
    {
        if (maxWorkers < 0) maxWorkers = 0;
        if (assignedWorkers < 0) assignedWorkers = 0;
        if (maxWorkers > 0 && assignedWorkers > maxWorkers) assignedWorkers = maxWorkers;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, EffectiveRange);
    }
#endif
}
