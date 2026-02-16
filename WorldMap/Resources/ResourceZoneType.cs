using UnityEngine;

/// <summary>
/// 资源区类型定义 - ScriptableObject
/// 定义不同类型的资源区及其属性
/// </summary>
[CreateAssetMenu(fileName = "NewResourceZone", menuName = "Game/World Map/Resource Zone Type")]
public class ResourceZoneType : ScriptableObject
{
    [Header("基本信息")]
    [Tooltip("资源区ID，用于保存和加载")]
    public string zoneId;

    [Tooltip("显示名称")]
    public string displayName;

    [Tooltip("资源区描述")]
    [TextArea(2, 4)]
    public string description;

    [Tooltip("资源区图标")]
    public Sprite icon;

    [Tooltip("地图上的显示颜色")]
    public Color mapColor = Color.green;

    [Header("关联资源")]
    [Tooltip("该区域产出的普通资源")]
    public ResourceDefinition normalResource;

    [Tooltip("该区域产出的优质资源")]
    public ResourceDefinition qualityResource;

    [Header("加成效果")]
    [Tooltip("在此区域建造对应建筑的效率加成（1.0 = 无加成，1.5 = 50%加成）")]
    [Range(1f, 3f)]
    public float efficiencyBonus = 1.5f;

    [Tooltip("优质资源产出的概率（0-1）")]
    [Range(0f, 1f)]
    public float qualityResourceChance = 0.3f;

    [Tooltip("副产品产出概率（使用优质资源加工时）")]
    [Range(0f, 1f)]
    public float byproductChance = 0.1f;

    [Header("适用建筑")]
    [Tooltip("在此资源区可获得加成的建筑定义列表（直接拖入 BuildableDefinition 资产）")]
    public BuildableDefinition[] compatibleBuildings;

    /// <summary>
    /// 检查建筑定义是否与此资源区兼容（可获得加成）
    /// </summary>
    public bool IsBuildingCompatible(BuildableDefinition buildingDef)
    {
        if (buildingDef == null || compatibleBuildings == null || compatibleBuildings.Length == 0)
            return false;

        foreach (var def in compatibleBuildings)
        {
            if (def == buildingDef)
                return true;
        }
        return false;
    }
}
