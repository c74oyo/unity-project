using System.Collections.Generic;
using UnityEngine;

public class PowerManager : MonoBehaviour
{
    public static PowerManager Instance { get; private set; }

    [Header("Tick")]
    [Min(0.05f)] public float tickInterval = 0.5f;
    private float _timer;

    private readonly List<PowerGenerator> _generators = new();
    private readonly List<PowerConsumer> _consumers = new();
    private readonly Dictionary<PowerConsumer, float> _maxAvailable = new();

    // Registration methods for performance optimization
    public void RegisterGenerator(PowerGenerator generator)
    {
        if (generator != null && !_generators.Contains(generator))
            _generators.Add(generator);
    }

    public void UnregisterGenerator(PowerGenerator generator)
    {
        _generators.Remove(generator);
    }

    public void RegisterConsumer(PowerConsumer consumer)
    {
        if (consumer != null && !_consumers.Contains(consumer))
            _consumers.Add(consumer);
    }

    public void UnregisterConsumer(PowerConsumer consumer)
    {
        _consumers.Remove(consumer);
    }

    /// <summary>
    /// Get all registered generators (for heatmap overlay)
    /// </summary>
    public List<PowerGenerator> GetAllGenerators()
    {
        // Remove null entries before returning
        _generators.RemoveAll(g => g == null);
        return _generators;
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < tickInterval) return;
        _timer = 0f;

        TickPower();
    }

    private void TickPower()
    {
        // Use cached lists instead of FindObjectsByType
        // Clean up null entries
        _generators.RemoveAll(g => g == null);
        _consumers.RemoveAll(c => c == null);

        // 1) 计算每个 consumer 在"距离衰减"下最多能拿到多少电（上限）
        _maxAvailable.Clear();
        for (int i = 0; i < _consumers.Count; i++)
        {
            var c = _consumers[i];
            if (c == null) continue;

            c.hasPower = false;
            c.allocated = 0f;

            float sum = 0f;
            Vector3 p = c.transform.position;

            for (int g = 0; g < _generators.Count; g++)
            {
                var gen = _generators[g];
                if (gen == null) continue;
                sum += gen.GetEffectiveSupplyAt(p);
            }

            _maxAvailable[c] = sum;
        }

        // 2) 全局供给 = 所有发电站 supply 之和（这是"总发电"，不考虑距离）
        float totalSupply = 0f;
        for (int g = 0; g < _generators.Count; g++)
            if (_generators[g] != null) totalSupply += _generators[g].Supply;

        // 3) 按优先级排序分配
        _consumers.Sort((a, b) => a.priority.CompareTo(b.priority));

        float remain = totalSupply;

        for (int i = 0; i < _consumers.Count; i++)
        {
            var c = _consumers[i];
            float need = c.Demand;

            if (need <= 0f)
            {
                c.hasPower = true;
                c.allocated = 0f;
                continue;
            }

            float maxAtPos = _maxAvailable.TryGetValue(c, out var m) ? m : 0f;

            // 如果连最大可达都不够，那么必定断电
            if (maxAtPos <= 0.0001f)
            {
                c.hasPower = false;
                c.allocated = 0f;
                continue;
            }

            // 分配不能超过：剩余总电、当前位置最大可达电
            float canGive = Mathf.Min(remain, maxAtPos);

            if (canGive >= need)
            {
                c.hasPower = true;
                c.allocated = need;
                remain -= need;
            }
            else
            {
                c.hasPower = false;
                c.allocated = Mathf.Max(0f, canGive);
                remain -= canGive;
                if (remain < 0f) remain = 0f;
            }
        }
    }
}