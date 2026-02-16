using System.Collections.Generic;
using UnityEngine;

public class WarehouseBuilding : MonoBehaviour, GlobalInventory.IStorageProvider
{
    [Header("Refs")]
    public GlobalInventory inventory;

    [Header("Capacity Bonus")]
    [Tooltip("对所有资源统一增加的容量。")]
    public int capacityAll = 50;

    [Tooltip("对特定资源增加的额外容量（amount 代表容量加成）。")]
    public List<ResourceAmount> capacitySpecific = new();

    private void OnEnable()
    {
        if (inventory == null) inventory = GlobalInventory.Instance;
        inventory?.RegisterStorage(this);
    }

    private void OnDisable()
    {
        inventory?.UnregisterStorage(this);
    }

    public int GetExtraCapacity(ResourceDefinition res)
    {
        if (res == null) return 0;

        int extra = Mathf.Max(0, capacityAll);

        if (capacitySpecific != null)
        {
            for (int i = 0; i < capacitySpecific.Count; i++)
            {
                var ra = capacitySpecific[i];
                if (ra.res == res)
                {
                    extra += Mathf.Max(0, ra.amount);
                }
            }
        }

        return extra;
    }
}
