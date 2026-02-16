using System.Collections.Generic;

/// <summary>
/// IInventorySystem - 库存系统接口
/// 统一 GlobalInventory 和 BaseInventory 的API
/// </summary>
public interface IInventorySystem
{
    // Currency
    float Money { get; }
    bool CanAfford(float cost);
    bool TrySpendMoney(float amount);
    void AddMoney(float amount);

    // Resources - Single
    float GetAmount(ResourceDefinition res);
    void Add(ResourceDefinition res, float amount);
    bool TryConsume(ResourceDefinition res, float amount);
    bool CanAdd(ResourceDefinition res, float amount);

    // Resources - Batch
    void AddBatch(List<ResourceAmount> items, float multiplier = 1f);
    bool TryConsumeBatch(List<ResourceAmount> items, float multiplier = 1f);
    bool TryAddBatch(List<ResourceAmount> items, float multiplier = 1f);
    bool HasResources(List<ResourceAmount> items, float multiplier = 1f);
    bool CanAffordBatch(List<ResourceAmount> items, float multiplier = 1f);
    bool CanAddBatch(List<ResourceAmount> items, float multiplier = 1f);

    // Capacity
    int TotalCapacity { get; }
    int UsedCapacity { get; }
    int FreeCapacity { get; }
    bool UseCapacity { get; }  // For checking if capacity limits are enabled

    // Storage Provider
    void RegisterStorageProvider(IStorageProvider provider);
    void UnregisterStorageProvider(IStorageProvider provider);
    void MarkCapacityDirty();
}

/// <summary>
/// 仓库容量提供者接口（兼容 GlobalInventory）
/// </summary>
public interface IStorageProvider
{
    int ProvidedCapacity { get; }
}