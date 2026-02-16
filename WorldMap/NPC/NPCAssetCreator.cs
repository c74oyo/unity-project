#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// 编辑器工具 - 快速创建NPC相关的ScriptableObject资源
/// </summary>
public class NPCAssetCreator : Editor
{
    private const string BASE_PATH = "Assets/building/Data/WorldMap";
    private const string NPC_FACTIONS_PATH = BASE_PATH + "/NPCFactions";

    [MenuItem("Tools/World Map/Create NPC Factions")]
    public static void CreateNPCFactions()
    {
        CreateDirectories();

        // 商人势力
        CreateFaction("merchant_guild", "商人公会", "专注于贸易的商人组织，提供稀有商品",
            NPCFaction.FactionType.Merchant, Color.yellow,
            initialRep: 20, baseTax: 0.05f, minRepForTrade: -80);

        // 中立势力
        CreateFaction("neutral_settlers", "中立定居者", "普通的定居者，对外来者持中立态度",
            NPCFaction.FactionType.Neutral, Color.gray,
            initialRep: 0, baseTax: 0.1f, minRepForTrade: -30);

        // 友好势力
        CreateFaction("allied_faction", "盟友势力", "与玩家有良好关系的友好势力",
            NPCFaction.FactionType.Friendly, Color.green,
            initialRep: 50, baseTax: 0.05f, minRepForTrade: -100);

        // 敌对势力
        CreateFaction("hostile_raiders", "掠夺者", "敌对的掠夺者势力，需要努力改善关系",
            NPCFaction.FactionType.Hostile, Color.red,
            initialRep: -30, baseTax: 0.2f, minRepForTrade: 0);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[NPCAssetCreator] NPC factions created successfully!");
    }

    private static void CreateDirectories()
    {
        if (!Directory.Exists(NPC_FACTIONS_PATH))
        {
            Directory.CreateDirectory(NPC_FACTIONS_PATH);
        }
    }

    private static NPCFaction CreateFaction(
        string factionId, string displayName, string description,
        NPCFaction.FactionType type, Color color,
        int initialRep, float baseTax, int minRepForTrade)
    {
        string path = $"{NPC_FACTIONS_PATH}/{factionId}.asset";

        // 检查是否已存在
        var existing = AssetDatabase.LoadAssetAtPath<NPCFaction>(path);
        if (existing != null)
        {
            Debug.Log($"[NPCAssetCreator] Faction already exists: {factionId}");
            return existing;
        }

        var faction = ScriptableObject.CreateInstance<NPCFaction>();
        faction.factionId = factionId;
        faction.displayName = displayName;
        faction.description = description;
        faction.factionType = type;
        faction.factionColor = color;
        faction.initialReputation = initialRep;
        faction.baseTaxRate = baseTax;
        faction.minReputationForTrade = minRepForTrade;
        faction.allowTrade = true;
        faction.defaultTerritoryRadius = 3;

        AssetDatabase.CreateAsset(faction, path);
        Debug.Log($"[NPCAssetCreator] Created faction: {displayName}");

        return faction;
    }

    [MenuItem("Tools/World Map/Validate NPC Assets")]
    public static void ValidateNPCAssets()
    {
        int errors = 0;

        // 检查NPC势力
        var factionGuids = AssetDatabase.FindAssets("t:NPCFaction", new[] { NPC_FACTIONS_PATH });
        Debug.Log($"[Validate] Found {factionGuids.Length} NPC factions");

        foreach (var guid in factionGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var faction = AssetDatabase.LoadAssetAtPath<NPCFaction>(path);

            if (string.IsNullOrEmpty(faction.factionId))
            {
                Debug.LogError($"[Validate] Faction at {path} has empty factionId!");
                errors++;
            }

            if (faction.baseTaxRate < 0 || faction.baseTaxRate > 1)
            {
                Debug.LogWarning($"[Validate] Faction {faction.factionId} has unusual tax rate: {faction.baseTaxRate}");
            }
        }

        if (errors == 0)
        {
            Debug.Log("[Validate] All NPC assets validated successfully!");
        }
        else
        {
            Debug.LogError($"[Validate] Found {errors} errors!");
        }
    }
}
#endif
