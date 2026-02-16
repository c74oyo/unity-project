#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// 编辑器工具 - 快速创建大地图相关的ScriptableObject资源
/// </summary>
public class WorldMapAssetCreator : Editor
{
    private const string BASE_PATH = "Assets/building/Data/WorldMap";
    private const string RESOURCE_ZONES_PATH = BASE_PATH + "/ResourceZones";
    private const string ROAD_TYPES_PATH = BASE_PATH + "/RoadTypes";

    [MenuItem("Tools/World Map/Create Default Assets")]
    public static void CreateDefaultAssets()
    {
        CreateDirectories();
        CreateResourceZoneTypes();
        CreateRoadTypes();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[WorldMapAssetCreator] Default assets created successfully!");
    }

    [MenuItem("Tools/World Map/Create Resource Zone Types")]
    public static void CreateResourceZoneTypes()
    {
        CreateDirectories();

        // 矿产区
        CreateResourceZoneType("mineral_zone", "矿产区", "富含矿石的区域",
            new Color(0.5f, 0.5f, 0.6f, 0.5f), 1.5f, 0.25f);

        // 森林区
        CreateResourceZoneType("forest_zone", "森林区", "茂密森林覆盖的区域",
            new Color(0.2f, 0.6f, 0.2f, 0.5f), 1.4f, 0.20f);

        // 肥沃区
        CreateResourceZoneType("fertile_zone", "肥沃区", "土壤肥沃的区域",
            new Color(0.6f, 0.4f, 0.1f, 0.5f), 1.6f, 0.30f);

        // 水源区
        CreateResourceZoneType("water_zone", "水源区", "靠近水源的区域",
            new Color(0.1f, 0.4f, 0.8f, 0.5f), 1.3f, 0.15f);

        Debug.Log("[WorldMapAssetCreator] Resource zone types created!");
    }

    [MenuItem("Tools/World Map/Create Road Types")]
    public static void CreateRoadTypes()
    {
        CreateDirectories();

        // 土路
        var dirtRoad = CreateRoadType("road_dirt", "土路", "简单的土路，建造便宜但效率较低",
            1, 50f, 1.0f, 1.0f, new Color(0.6f, 0.4f, 0.2f), null);

        // 石板路
        var stoneRoad = CreateRoadType("road_stone", "石板路", "铺设石板的道路，效率中等",
            2, 150f, 1.5f, 1.3f, new Color(0.5f, 0.5f, 0.5f), null);

        // 沥青路
        var asphaltRoad = CreateRoadType("road_asphalt", "沥青路", "现代沥青路，效率最高",
            3, 300f, 2.0f, 1.6f, new Color(0.2f, 0.2f, 0.2f), null);

        // 设置升级链
        if (dirtRoad != null && stoneRoad != null)
        {
            dirtRoad.upgradeTo = stoneRoad;
            EditorUtility.SetDirty(dirtRoad);
        }

        if (stoneRoad != null && asphaltRoad != null)
        {
            stoneRoad.upgradeTo = asphaltRoad;
            EditorUtility.SetDirty(stoneRoad);
        }

        Debug.Log("[WorldMapAssetCreator] Road types created!");
    }

    // ============ Helper Methods ============

    private static void CreateDirectories()
    {
        if (!Directory.Exists(RESOURCE_ZONES_PATH))
        {
            Directory.CreateDirectory(RESOURCE_ZONES_PATH);
        }

        if (!Directory.Exists(ROAD_TYPES_PATH))
        {
            Directory.CreateDirectory(ROAD_TYPES_PATH);
        }
    }

    private static ResourceZoneType CreateResourceZoneType(
        string zoneId, string displayName, string description,
        Color mapColor, float efficiencyBonus, float qualityChance)
    {
        string path = $"{RESOURCE_ZONES_PATH}/{zoneId}.asset";

        // 检查是否已存在
        var existing = AssetDatabase.LoadAssetAtPath<ResourceZoneType>(path);
        if (existing != null)
        {
            Debug.Log($"[WorldMapAssetCreator] Resource zone already exists: {zoneId}");
            return existing;
        }

        var zone = ScriptableObject.CreateInstance<ResourceZoneType>();
        zone.zoneId = zoneId;
        zone.displayName = displayName;
        zone.description = description;
        zone.mapColor = mapColor;
        zone.efficiencyBonus = efficiencyBonus;
        zone.qualityResourceChance = qualityChance;
        // compatibleBuildings 需要在 Inspector 中手动拖入 BuildableDefinition 资产

        AssetDatabase.CreateAsset(zone, path);
        return zone;
    }

    private static RoadType CreateRoadType(
        string roadTypeId, string displayName, string description,
        int level, float moneyCost, float speedMult, float capacityMult,
        Color roadColor, RoadType upgradeTo)
    {
        string path = $"{ROAD_TYPES_PATH}/{roadTypeId}.asset";

        // 检查是否已存在
        var existing = AssetDatabase.LoadAssetAtPath<RoadType>(path);
        if (existing != null)
        {
            Debug.Log($"[WorldMapAssetCreator] Road type already exists: {roadTypeId}");
            return existing;
        }

        var road = ScriptableObject.CreateInstance<RoadType>();
        road.roadTypeId = roadTypeId;
        road.displayName = displayName;
        road.description = description;
        road.level = level;
        road.moneyCost = moneyCost;
        road.speedMultiplier = speedMult;
        road.capacityMultiplier = capacityMult;
        road.roadColor = roadColor;
        road.upgradeTo = upgradeTo;

        AssetDatabase.CreateAsset(road, path);
        return road;
    }

    // ============ Validation ============

    [MenuItem("Tools/World Map/Validate Assets")]
    public static void ValidateAssets()
    {
        int errors = 0;

        // 检查资源区类型
        var zoneGuids = AssetDatabase.FindAssets("t:ResourceZoneType", new[] { RESOURCE_ZONES_PATH });
        Debug.Log($"[Validate] Found {zoneGuids.Length} resource zone types");

        foreach (var guid in zoneGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var zone = AssetDatabase.LoadAssetAtPath<ResourceZoneType>(path);

            if (string.IsNullOrEmpty(zone.zoneId))
            {
                Debug.LogError($"[Validate] Zone at {path} has empty zoneId!");
                errors++;
            }
        }

        // 检查道路类型
        var roadGuids = AssetDatabase.FindAssets("t:RoadType", new[] { ROAD_TYPES_PATH });
        Debug.Log($"[Validate] Found {roadGuids.Length} road types");

        foreach (var guid in roadGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var road = AssetDatabase.LoadAssetAtPath<RoadType>(path);

            if (string.IsNullOrEmpty(road.roadTypeId))
            {
                Debug.LogError($"[Validate] Road at {path} has empty roadTypeId!");
                errors++;
            }

            if (road.upgradeTo != null && road.upgradeTo.level <= road.level)
            {
                Debug.LogWarning($"[Validate] Road {road.roadTypeId} upgrades to lower/equal level road!");
            }
        }

        if (errors == 0)
        {
            Debug.Log("[Validate] All assets validated successfully!");
        }
        else
        {
            Debug.LogError($"[Validate] Found {errors} errors!");
        }
    }
}
#endif
