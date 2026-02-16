using System.Collections.Generic;

public class Inventory
{
    private readonly Dictionary<ResourceDefinition, int> _map = new();

    public int Get(ResourceDefinition res)
    {
        if (res == null) return 0;
        return _map.TryGetValue(res, out var v) ? v : 0;
    }

    public void Add(ResourceDefinition res, int amount)
    {
        if (res == null || amount <= 0) return;
        _map[res] = Get(res) + amount;
    }

    public bool CanRemove(ResourceDefinition res, int amount)
    {
        if (res == null || amount <= 0) return true;
        return Get(res) >= amount;
    }

    public bool TryRemove(ResourceDefinition res, int amount)
    {
        if (res == null || amount <= 0) return true;
        int cur = Get(res);
        if (cur < amount) return false;
        _map[res] = cur - amount;
        return true;
    }

    /// <summary>
    /// Get all resources in the inventory
    /// </summary>
    public Dictionary<ResourceDefinition, int> GetAll()
    {
        return new Dictionary<ResourceDefinition, int>(_map);
    }
}