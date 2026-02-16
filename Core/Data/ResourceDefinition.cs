using UnityEngine;

[CreateAssetMenu(menuName = "Game/Resource Definition", fileName = "Res_")]
public class ResourceDefinition : ScriptableObject
{
    [Header("ID")]
    public string id;                 // 例如 "water" / "food"
    public string displayName = "New Resource";

    [Header("Optional")]
    public Sprite icon;

    [Header("Resource Type")]
    [Tooltip("资源类型：Material=普通物资, Vehicle=载具, Special=特殊")]
    public ResourceType resourceType = ResourceType.Material;

    [Header("Vehicle Properties（仅 Vehicle 类型有效）")]
    [Tooltip("载具容量（每辆可运输的货物单位数）")]
    public int vehicleCapacity = 100;

    [Tooltip("载具移动速度倍率")]
    [Range(0.5f, 3f)]
    public float vehicleSpeedMultiplier = 1f;

    [Header("Quality System")]
    [Tooltip("资源品质等级：1.0=普通，1.5=优质，2.0=极品")]
    [Range(0.5f, 3f)]
    public float quality = 1.0f;

    [Tooltip("资源类别（用于判断是否为同类资源的不同品质）")]
    public string category;

    [Tooltip("如果是优质资源，对应的普通版本")]
    public ResourceDefinition normalVariant;

    [Tooltip("如果是普通资源，对应的优质版本")]
    public ResourceDefinition qualityVariant;

    [Tooltip("加工此资源时可能产出的副产品")]
    public ResourceDefinition[] possibleByproducts;

    [Tooltip("副产品的基础产出概率（会受资源区加成影响）")]
    [Range(0f, 1f)]
    public float byproductBaseChance = 0f;

    /// <summary>
    /// 是否为载具类型资源
    /// </summary>
    public bool IsVehicle => resourceType == ResourceType.Vehicle;

    /// <summary>
    /// 是否为优质资源
    /// </summary>
    public bool IsQualityResource => quality > 1.0f;

    /// <summary>
    /// 是否有副产品
    /// </summary>
    public bool HasByproducts => possibleByproducts != null && possibleByproducts.Length > 0;

    /// <summary>
    /// 获取加工效率倍率（基于品质）
    /// </summary>
    public float GetProcessingEfficiencyMultiplier()
    {
        // 优质资源加工更快
        return quality;
    }

    /// <summary>
    /// 判断是否与另一资源属于同一类别
    /// </summary>
    public bool IsSameCategory(ResourceDefinition other)
    {
        if (other == null) return false;
        if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(other.category))
            return false;
        return category == other.category;
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(id))
            id = name;

        // 自动设置类别（如果未设置）
        if (string.IsNullOrWhiteSpace(category))
        {
            // 从id推断类别，例如 "ore_quality" -> "ore"
            if (id.Contains("_"))
            {
                category = id.Split('_')[0];
            }
            else
            {
                category = id;
            }
        }
    }
}

/// <summary>
/// 资源类型枚举
/// </summary>
public enum ResourceType
{
    Material,   // 普通可堆叠物资（木材、矿石等）
    Vehicle,    // 载具（可用于运输）
    Special     // 特殊资源
}
