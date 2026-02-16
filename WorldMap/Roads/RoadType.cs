using UnityEngine;

/// <summary>
/// 道路类型定义 - ScriptableObject
/// 定义不同等级道路的属性
/// </summary>
[CreateAssetMenu(fileName = "NewRoadType", menuName = "Game/World Map/Road Type")]
public class RoadType : ScriptableObject
{
    [Header("基本信息")]
    [Tooltip("道路类型ID")]
    public string roadTypeId;

    [Tooltip("显示名称")]
    public string displayName;

    [Tooltip("道路描述")]
    [TextArea(2, 4)]
    public string description;

    [Tooltip("道路等级（1=土路，2=石板路，3=沥青路）")]
    [Range(1, 5)]
    public int level = 1;

    [Header("建造成本")]
    [Tooltip("建造所需资源")]
    public ResourceAmount[] buildCost;

    [Tooltip("建造所需金钱")]
    public float moneyCost = 100f;

    [Header("升级")]
    [Tooltip("可升级到的道路类型（如果有）")]
    public RoadType upgradeTo;

    [Tooltip("升级所需额外资源")]
    public ResourceAmount[] upgradeCost;

    [Tooltip("升级所需额外金钱")]
    public float upgradeMoneyConst = 50f;

    [Header("运输属性")]
    [Tooltip("运输速度倍率（1.0=基础，2.0=两倍速）")]
    [Range(0.5f, 5f)]
    public float speedMultiplier = 1.0f;

    [Tooltip("单次运输容量倍率")]
    [Range(1f, 5f)]
    public float capacityMultiplier = 1.0f;

    [Tooltip("货物损失保护率（0=无保护，1=完全保护）")]
    [Range(0f, 1f)]
    public float cargoLossProtection = 0f;

    [Header("耐久度")]
    [Tooltip("免维护运输量阈值（低于此值不会损耗耐久度）")]
    [Min(0)]
    public int durabilityFreeThreshold = 1000;

    [Tooltip("超出阈值后，每多少单位运输量降低1%耐久度")]
    [Min(1)]
    public int durabilityDecayPerUnit = 500;

    [Header("维护成本")]
    [Tooltip("修复每1%耐久度所需金钱")]
    [Min(0)]
    public float repairCostPerPercent = 5f;

    [Tooltip("修复每1%耐久度所需资源（可选）")]
    public ResourceAmount[] repairCostResources;

    [Header("视觉")]
    [Tooltip("道路材质/颜色")]
    public Color roadColor = Color.gray;

    [Tooltip("道路宽度（视觉）")]
    [Range(0.5f, 3f)]
    public float visualWidth = 1f;

    [Tooltip("道路预制体（可选）")]
    public GameObject roadPrefab;

    /// <summary>
    /// 是否可以升级
    /// </summary>
    public bool CanUpgrade => upgradeTo != null;

    /// <summary>
    /// 计算实际建造成本（考虑地形倍率）
    /// </summary>
    public float CalculateTotalMoneyCost(float terrainMultiplier = 1f)
    {
        return moneyCost * terrainMultiplier;
    }
}
