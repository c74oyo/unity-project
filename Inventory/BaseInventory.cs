using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BaseInventory - 单个基地的独立库存系统
/// 替代 GlobalInventory，每个基地有自己的资源存储
/// </summary>
public class BaseInventory : MonoBehaviour, IInventorySystem
{
    [Header("Owner")]
    public BaseInstance ownerBase;

    [Header("Capacity")]
    [Min(0)] public int baseCapacity = 100;

    [Header("Currency")]
    [Min(0)] public float money = 1000f;

    [Header("Runtime Storage")]
    [SerializeField] private List<ResourceAmount> _storage = new();

    private readonly List<IStorageProvider> _storageProviders = new();
    private int _cachedTotalCapacity = 0;
    private bool _capacityDirty = true;

    // Events
    public event Action OnChanged;
    public event Action<float> OnMoneyChanged;

    // ============ Public Properties ============
    public int TotalCapacity
    {
        get
        {
            if (_capacityDirty)
            {
                RecalculateCapacity();
                _capacityDirty = false;
            }
            return _cachedTotalCapacity;
        }
    }

    public int UsedCapacity
    {
        get
        {
            float sum = 0f;
            foreach (var ra in _storage)
                if (ra.res != null) sum += ra.amount;
            return Mathf.CeilToInt(sum);
        }
    }

    public int FreeCapacity => Mathf.Max(0, TotalCapacity - UsedCapacity);
    public bool UseCapacity => true;  // BaseInventory always uses capacity
    public float Money => money;

    // ============ Lifecycle ============
    private void OnValidate()
    {
        baseCapacity = Mathf.Max(0, baseCapacity);
        money = Mathf.Max(0f, money);
    }

    // ============ Storage Provider System ============
    public void RegisterStorageProvider(IStorageProvider provider)
    {
        if (provider != null && !_storageProviders.Contains(provider))
        {
            _storageProviders.Add(provider);
            MarkCapacityDirty();
        }
    }

    public void UnregisterStorageProvider(IStorageProvider provider)
    {
        if (_storageProviders.Remove(provider))
            MarkCapacityDirty();
    }

    public void MarkCapacityDirty()
    {
        _capacityDirty = true;
    }

    private void RecalculateCapacity()
    {
        _storageProviders.RemoveAll(p => p == null);

        int total = baseCapacity;
        foreach (var p in _storageProviders)
        {
            if (p != null)
                total += p.ProvidedCapacity;
        }

        _cachedTotalCapacity = total;
    }

    // ============ Currency Operations ============
    public bool CanAfford(float cost)
    {
        return money >= cost;
    }

    public bool TrySpendMoney(float amount)
    {
        if (amount < 0f)
        {
            Debug.LogError($"[BaseInventory] Cannot spend negative money: {amount}", this);
            return false;
        }

        if (!CanAfford(amount))
            return false;

        money -= amount;
        OnMoneyChanged?.Invoke(money);
        OnChanged?.Invoke();
        return true;
    }

    public void AddMoney(float amount)
    {
        if (amount < 0f)
        {
            Debug.LogError($"[BaseInventory] Cannot add negative money: {amount}", this);
            return;
        }

        money += amount;
        OnMoneyChanged?.Invoke(money);
        OnChanged?.Invoke();
    }

    // ============ Resource Operations ============
    public float GetAmount(ResourceDefinition res)
    {
        if (res == null) return 0f;

        foreach (var ra in _storage)
            if (ra.res == res) return ra.amount;

        return 0f;
    }

    public bool CanAdd(ResourceDefinition res, float amount)
    {
        if (res == null || amount <= 0f) return true;

        // Check if adding this amount would exceed capacity
        int neededSpace = Mathf.CeilToInt(amount);
        return FreeCapacity >= neededSpace;
    }

    public void Add(ResourceDefinition res, float amount)
    {
        if (res == null || amount <= 0f) return;

        int amountInt = Mathf.RoundToInt(amount);
        if (amountInt <= 0) return;

        int index = _storage.FindIndex(ra => ra.res == res);
        if (index >= 0)
        {
            // 修改已存在的资源
            ResourceAmount existing = _storage[index];
            existing.amount += amountInt;
            _storage[index] = existing;
        }
        else
        {
            // 添加新资源
            _storage.Add(new ResourceAmount { res = res, amount = amountInt });
        }

        OnChanged?.Invoke();
    }

    public void AddBatch(List<ResourceAmount> items, float multiplier = 1f)
    {
        if (items == null || items.Count == 0) return;

        foreach (var item in items)
        {
            if (item.res != null && item.amount > 0f)
                Add(item.res, item.amount * multiplier);
        }
    }

    public bool TryConsume(ResourceDefinition res, float amount)
    {
        if (res == null || amount <= 0f) return false;

        int amountInt = Mathf.RoundToInt(amount);
        if (amountInt <= 0) return false;

        float current = GetAmount(res);
        if (current < amountInt) return false;

        int index = _storage.FindIndex(ra => ra.res == res);
        if (index >= 0)
        {
            ResourceAmount existing = _storage[index];
            existing.amount -= amountInt;

            // 保留数量为0的资源，这样玩家可以看到资源被消耗完了
            if (existing.amount < 0)
                existing.amount = 0;

            _storage[index] = existing;

            OnChanged?.Invoke();
            return true;
        }

        return false;
    }

    public bool TryConsumeBatch(List<ResourceAmount> items, float multiplier = 1f)
    {
        if (items == null || items.Count == 0) return true;

        // Check all items first
        foreach (var item in items)
        {
            if (item.res == null) continue;
            float needed = item.amount * multiplier;
            if (needed <= 0f) continue;

            if (GetAmount(item.res) < needed)
                return false;
        }

        // Consume all
        foreach (var item in items)
        {
            if (item.res != null && item.amount > 0f)
                TryConsume(item.res, item.amount * multiplier);
        }

        return true;
    }

    // ============ Query Methods ============
    public List<ResourceAmount> GetAllResources()
    {
        return new List<ResourceAmount>(_storage);
    }

    public bool HasResources(List<ResourceAmount> items, float multiplier = 1f)
    {
        if (items == null || items.Count == 0) return true;

        foreach (var item in items)
        {
            if (item.res == null) continue;
            float needed = item.amount * multiplier;
            if (needed <= 0f) continue;

            if (GetAmount(item.res) < needed)
                return false;
        }

        return true;
    }

    public bool TryAddBatch(List<ResourceAmount> items, float multiplier = 1f)
    {
        if (items == null || items.Count == 0) return true;

        // Check capacity first
        if (!CanAddBatch(items, multiplier))
            return false;

        // Add all
        AddBatch(items, multiplier);
        return true;
    }

    public bool CanAffordBatch(List<ResourceAmount> items, float multiplier = 1f)
    {
        // Alias for HasResources - same logic
        return HasResources(items, multiplier);
    }

    public bool CanAddBatch(List<ResourceAmount> items, float multiplier = 1f)
    {
        if (items == null || items.Count == 0) return true;

        // Calculate total space needed
        float totalNeeded = 0f;
        foreach (var item in items)
        {
            if (item.res != null && item.amount > 0f)
                totalNeeded += item.amount * multiplier;
        }

        int spaceNeeded = Mathf.CeilToInt(totalNeeded);
        return FreeCapacity >= spaceNeeded;
    }

    // ============ Debug ============
#if UNITY_EDITOR
    [ContextMenu("Debug: Print Inventory")]
    private void DebugPrintInventory()
    {
        Debug.Log($"[BaseInventory] Base: {(ownerBase != null ? ownerBase.baseName : "None")}");
        Debug.Log($"  Money: {money:F2}");
        Debug.Log($"  Capacity: {UsedCapacity}/{TotalCapacity}");
        Debug.Log($"  Resources:");
        foreach (var ra in _storage)
        {
            if (ra.res != null)
                Debug.Log($"    {ra.res.displayName}: {ra.amount:F2}");
        }
    }
#endif
}