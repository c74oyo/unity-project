using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单个建筑的保存数据
/// </summary>
[Serializable]
public class BuildingSaveData
{
    public string buildingDefName;  // BuildableDefinition 的名称
    public Vector2Int anchor;       // 网格锚点
    public int rotation;            // 旋转（0-3）
    public float worldY;            // 建筑世界坐标 Y（高度）

    // 扩展数据（可选）
    public int assignedWorkers;
    public int desiredWorkers;
    public List<string> cardSlotData;  // 卡片槽位数据

    public BuildingSaveData() { }

    public BuildingSaveData(string defName, Vector2Int anchor, int rot, float y = 0f)
    {
        this.buildingDefName = defName;
        this.anchor = anchor;
        this.rotation = rot;
        this.worldY = y;
        this.cardSlotData = new List<string>();
    }
}

/// <summary>
/// 资源保存数据
/// </summary>
[Serializable]
public class ResourceSaveData
{
    public string resourceName;  // ResourceDefinition 的名称
    public float amount;

    public ResourceSaveData() { }

    public ResourceSaveData(string name, float amt)
    {
        this.resourceName = name;
        this.amount = amt;
    }
}

/// <summary>
/// 资源流动保存数据（每分钟消耗/生产）
/// </summary>
[Serializable]
public class ResourceFlowSaveData
{
    public string resourceName;
    public float consumptionPerMinute;
    public float productionPerMinute;

    public ResourceFlowSaveData() { }

    public ResourceFlowSaveData(string name, float consume, float produce)
    {
        this.resourceName = name;
        this.consumptionPerMinute = consume;
        this.productionPerMinute = produce;
    }
}

/// <summary>
/// 单个基地的完整保存数据
/// </summary>
[Serializable]
public class BaseSaveData
{
    // 基地基本信息
    public string baseId;
    public string baseName;
    public Vector3 worldPosition;  // 在大地图上的位置

    // 资源区信息（从大地图带入基地场景）
    public string resourceZoneTypeId;  // 该基地所在的资源区类型ID，空表示无资源区

    // 网格设置
    public Vector3 gridOrigin;
    public int gridWidth;
    public int gridHeight;
    public float gridCellSize;

    // 经济数据
    public float money;
    public int baseCapacity;
    public List<ResourceSaveData> resources;

    // 建筑数据
    public List<BuildingSaveData> buildings;

    // 资源流动数据（用于离线计算）
    public List<ResourceFlowSaveData> resourceFlows;

    // 运输系统
    public bool hasDockYard;  // 基地是否建有装卸码头（大地图运输必需）
    public float dockLoadingSeconds = 4f;  // 装卸码头的实际装卸时间（受卡片加速影响），大地图运输使用

    // 时间戳
    public string lastSaveTime;
    public long lastSaveTimeTicks;  // 用于精确时间计算

    public BaseSaveData()
    {
        resources = new List<ResourceSaveData>();
        buildings = new List<BuildingSaveData>();
        resourceFlows = new List<ResourceFlowSaveData>();
        UpdateTimestamp();
    }

    public BaseSaveData(string id, string name, Vector3 worldPos)
    {
        this.baseId = id;
        this.baseName = name;
        this.worldPosition = worldPos;
        this.resources = new List<ResourceSaveData>();
        this.buildings = new List<BuildingSaveData>();
        this.resourceFlows = new List<ResourceFlowSaveData>();
        UpdateTimestamp();
    }

    public void UpdateTimestamp()
    {
        var now = DateTime.Now;
        lastSaveTime = now.ToString("yyyy-MM-dd HH:mm:ss");
        lastSaveTimeTicks = now.Ticks;
    }

    // ============ Resource Helpers (for world map transport) ============

    /// <summary>
    /// 获取指定资源的当前数量
    /// </summary>
    public float GetResourceAmount(string resName)
    {
        if (resources == null) return 0f;
        var entry = resources.Find(r => r.resourceName == resName);
        return entry != null ? entry.amount : 0f;
    }

    /// <summary>
    /// 尝试消耗指定数量的资源（不足则失败）
    /// </summary>
    public bool TryConsumeResource(string resName, float amount)
    {
        if (resources == null) return false;
        var entry = resources.Find(r => r.resourceName == resName);
        if (entry == null || entry.amount < amount) return false;

        entry.amount -= amount;
        return true;
    }

    /// <summary>
    /// 添加资源（已有则累加，没有则新建条目）
    /// </summary>
    public void AddResource(string resName, float amount)
    {
        if (resources == null)
            resources = new List<ResourceSaveData>();

        var entry = resources.Find(r => r.resourceName == resName);
        if (entry != null)
        {
            entry.amount += amount;
        }
        else
        {
            resources.Add(new ResourceSaveData(resName, amount));
        }
    }

    // ============ Serialization Helpers ============
    public string ToJson(bool prettyPrint = true)
    {
        UpdateTimestamp();
        return JsonUtility.ToJson(this, prettyPrint);
    }

    public static BaseSaveData FromJson(string json)
    {
        return JsonUtility.FromJson<BaseSaveData>(json);
    }

    public void SaveToFile(string filePath)
    {
        try
        {
            string json = ToJson(true);
            System.IO.File.WriteAllText(filePath, json);
            Debug.Log($"[BaseSaveData] Saved to {filePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[BaseSaveData] Failed to save: {e.Message}");
        }
    }

    public static BaseSaveData LoadFromFile(string filePath)
    {
        try
        {
            if (!System.IO.File.Exists(filePath))
            {
                Debug.LogWarning($"[BaseSaveData] File not found: {filePath}");
                return null;
            }

            string json = System.IO.File.ReadAllText(filePath);
            BaseSaveData data = FromJson(json);
            Debug.Log($"[BaseSaveData] Loaded from {filePath}");
            return data;
        }
        catch (Exception e)
        {
            Debug.LogError($"[BaseSaveData] Failed to load: {e.Message}");
            return null;
        }
    }
}

/// <summary>
/// 全局游戏存档数据（包含所有基地）
/// </summary>
[Serializable]
public class GameSaveData
{
    public List<BaseSaveData> bases;
    public string currentActiveBaseId;
    public string saveVersion = "1.0";
    public string lastSaveTime;

    // Trade & Transport (Phase 2)
    public List<TradeRoute> tradeRoutes;
    public List<WorldMapTransportOrder> activeTransportOrders;

    // World Map State
    public List<RoadSegment> roads;
    public List<WorldMapCellData> worldMapCells;
    public NPCManagerSaveData npcData;

    // Quest System
    public QuestManagerSaveData questData;

    // Vehicle Transport
    public VehicleTransportSaveData vehicleTransportData;

    // Character Progression (养成系统)
    public PlayerInventorySaveData playerInventoryData;
    public CharacterProgressionSaveData characterProgressionData;

    public GameSaveData()
    {
        bases = new List<BaseSaveData>();
        tradeRoutes = new List<TradeRoute>();
        activeTransportOrders = new List<WorldMapTransportOrder>();
        roads = new List<RoadSegment>();
        worldMapCells = new List<WorldMapCellData>();
        lastSaveTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public string ToJson(bool prettyPrint = true)
    {
        lastSaveTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        return JsonUtility.ToJson(this, prettyPrint);
    }

    public static GameSaveData FromJson(string json)
    {
        return JsonUtility.FromJson<GameSaveData>(json);
    }

    public void SaveToFile(string filePath)
    {
        try
        {
            string json = ToJson(true);
            System.IO.File.WriteAllText(filePath, json);
            Debug.Log($"[GameSaveData] Saved to {filePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameSaveData] Failed to save: {e.Message}");
        }
    }

    public static GameSaveData LoadFromFile(string filePath)
    {
        try
        {
            if (!System.IO.File.Exists(filePath))
            {
                Debug.LogWarning($"[GameSaveData] File not found: {filePath}");
                return null;
            }

            string json = System.IO.File.ReadAllText(filePath);
            GameSaveData data = FromJson(json);
            Debug.Log($"[GameSaveData] Loaded from {filePath}");
            return data;
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameSaveData] Failed to load: {e.Message}");
            return null;
        }
    }
}
