using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 统一建筑属性修正接口：把 Worksite 卡槽里的 CardModifier 汇总成 “Add / Mul / ApplyMul / ApplyDiv”
/// - Add：sum(value)
/// - Mul：max(minMul, 1 + Add)
/// - ApplyMul：base * Mul
/// - ApplyDiv：base / Mul（常用于“速度加成 -> 时间变短”）
/// </summary>
[DisallowMultipleComponent]
public class BuildingModifiers : MonoBehaviour
{
    [Header("Source")]
    public Worksite worksite;
    public bool useCardSlots = true;

    [Header("Cache")]
    public bool useCache = true;

    private bool _dirty = true;
    private readonly Dictionary<CardModifier.ModifierType, float> _sumCache = new();

    private void Awake()
    {
        if (worksite == null)
            worksite = GetComponentInChildren<Worksite>(true);

        HookWorksite();
        MarkDirty();
    }

    private void OnEnable()
    {
        HookWorksite();
    }

    private void OnDisable()
    {
        UnhookWorksite();
    }

    private void HookWorksite()
    {
        if (worksite == null) return;
        worksite.OnSlotsChanged -= MarkDirty;
        worksite.OnSlotsChanged += MarkDirty;
    }

    private void UnhookWorksite()
    {
        if (worksite == null) return;
        worksite.OnSlotsChanged -= MarkDirty;
    }

    private void MarkDirty()
    {
        _dirty = true;
    }

    private void EnsureCache()
    {
        if (!useCache) return;
        if (!_dirty) return;

        _sumCache.Clear();

        if (!useCardSlots || worksite == null)
        {
            _dirty = false;
            return;
        }

        // 只缓存“被请求过的类型”也行；这里用“按需写入”的策略：
        // 当 GetAdd(type) 调用时，如果 cache miss，会立刻向 worksite 取一次并写入缓存。
        _dirty = false;
    }

    /// <summary>获得某个 ModifierType 的加法总和（例如 +0.25 + -0.1 = +0.15）</summary>
    public float GetAdd(CardModifier.ModifierType type)
    {
        if (!useCardSlots || worksite == null) return 0f;

        if (!useCache)
            return worksite.GetModifierSum(type);

        EnsureCache();

        if (_sumCache.TryGetValue(type, out var v))
            return v;

        // cache miss：按需读取并缓存
        v = worksite.GetModifierSum(type);
        _sumCache[type] = v;
        return v;
    }

    /// <summary>获得倍率：Mul = max(minMul, 1 + Add)</summary>
    public float GetMul(CardModifier.ModifierType type, float minMul = 0.1f)
    {
        float add = GetAdd(type);
        return Mathf.Max(minMul, 1f + add);
    }

    public float ApplyMul(float baseValue, CardModifier.ModifierType type, float minMul = 0.1f)
    {
        return baseValue * GetMul(type, minMul);
    }

    public float ApplyDiv(float baseValue, CardModifier.ModifierType type, float minMul = 0.1f)
    {
        return baseValue / GetMul(type, minMul);
    }
}
