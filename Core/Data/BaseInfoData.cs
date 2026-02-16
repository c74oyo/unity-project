using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 基地信息数据 - 用于UI显示
/// 包含资源库存、建筑列表、每分钟资源消耗/增加等信息
/// </summary>
public class BaseInfoData
{
    public string baseId;
    public string baseName;
    public Vector3 worldPosition;

    // 资源库存信息
    public float money;
    public List<ResourceStockInfo> resources = new();

    // 建筑信息
    public List<BuildingInfo> buildings = new();

    // 每分钟资源流动（消耗和生产的净值）
    public List<ResourceFlowInfo> resourceFlows = new();

    // 资源区信息
    public string resourceZoneTypeId;
    public string resourceZoneName;
    public ResourceZoneInfo zoneInfo;

    /// <summary>
    /// 资源库存信息
    /// </summary>
    [System.Serializable]
    public class ResourceStockInfo
    {
        public ResourceDefinition resource;
        public int current;        // 当前库存
        public int capacity;       // 容量上限
        public float percentage;   // 百分比 0-1

        public ResourceStockInfo(ResourceDefinition res, float cur, int cap)
        {
            resource = res;
            current = Mathf.RoundToInt(cur);
            capacity = cap;
            percentage = cap > 0 ? Mathf.Clamp01(cur / cap) : 0f;
        }
    }

    /// <summary>
    /// 建筑信息
    /// </summary>
    [System.Serializable]
    public class BuildingInfo
    {
        public string buildingName;
        public string buildingType;  // 建筑类型（Producer, PowerGenerator等）
        public int count;            // 同类建筑数量
        public string status;        // 状态：Working, Idle, NoPower, NoResource
        public GameObject gameObject;

        /// <summary>
        /// 获取显示用的格式化文本
        /// </summary>
        public string GetDisplayText()
        {
            string countStr = count > 1 ? $" x{count}" : "";
            string statusStr = !string.IsNullOrEmpty(status) ? $" [{status}]" : "";
            return $"{buildingName}{countStr}{statusStr}";
        }

        /// <summary>
        /// 获取建筑类型的图标（用于UI显示）
        /// </summary>
        public string GetTypeIcon()
        {
            return buildingType switch
            {
                "Producer" => "⚙",
                "PowerGenerator" => "⚡",
                "Warehouse" => "📦",
                "BaseCore" => "🏠",
                "DockYard" => "🚢",
                _ => "🏗"
            };
        }
    }

    /// <summary>
    /// 资源流动信息（每分钟）
    /// </summary>
    [System.Serializable]
    public class ResourceFlowInfo
    {
        public ResourceDefinition resource;
        public float consumption;  // 每分钟消耗
        public float production;   // 每分钟生产
        public float net;          // 净值 = production - consumption

        public ResourceFlowInfo(ResourceDefinition res, float consume, float produce)
        {
            resource = res;
            consumption = consume;
            production = produce;
            net = produce - consume;
        }
    }

    /// <summary>
    /// 资源区加成信息
    /// </summary>
    [System.Serializable]
    public class ResourceZoneInfo
    {
        public string zoneId;
        public string displayName;
        public float efficiencyBonus;
        public float qualityResourceChance;
        public float byproductChance;
        public Sprite icon;  // 可选

        public ResourceZoneInfo(ResourceZoneType zoneType)
        {
            if (zoneType == null)
            {
                zoneId = "";
                displayName = "None";
                efficiencyBonus = 1f;
                qualityResourceChance = 0f;
                byproductChance = 0f;
                icon = null;
                return;
            }

            zoneId = zoneType.zoneId;
            displayName = zoneType.displayName;
            efficiencyBonus = zoneType.efficiencyBonus;
            qualityResourceChance = zoneType.qualityResourceChance;
            byproductChance = zoneType.byproductChance;
            icon = null;  // TODO: 如果ResourceZoneType添加icon字段，这里可以设置
        }
    }

    /// <summary>
    /// 从BaseInstance收集信息
    /// </summary>
    public static BaseInfoData FromBaseInstance(BaseInstance baseInstance)
    {
        if (baseInstance == null) return null;

        var data = new BaseInfoData
        {
            baseId = baseInstance.baseId,
            baseName = baseInstance.baseName,
            worldPosition = baseInstance.Position
        };

        // 收集资源库存信息
        if (baseInstance.inventory != null)
        {
            data.money = baseInstance.inventory.Money;

            var resources = baseInstance.inventory.GetAllResources();
            if (resources != null)
            {
                int totalCapacity = baseInstance.inventory.TotalCapacity;

                foreach (var item in resources)
                {
                    if (item.res == null) continue;

                    data.resources.Add(new ResourceStockInfo(
                        item.res,
                        item.amount,
                        totalCapacity
                    ));
                }
            }
        }

        // 收集建筑信息
        var allBuildings = baseInstance.GetAllBuildings();
        foreach (var building in allBuildings)
        {
            if (building == null) continue;

            string buildingType = "Building";

            // 检测建筑类型
            if (building.GetComponent<ProducerBuilding>() != null)
                buildingType = "Producer";
            else if (building.GetComponent<PowerGenerator>() != null)
                buildingType = "PowerGenerator";
            else if (building.GetComponent<WarehouseBuilding>() != null)
                buildingType = "Warehouse";
            else if (building.GetComponent<DockYard>() != null)
                buildingType = "DockYard";
            else if (building.GetComponent<BaseCoreBuilding>() != null)
                buildingType = "BaseCore";

            // 优先从 BuildableDefinition 获取 displayName
            string buildingName = building.name;
            var buildableInstance = building.GetComponent<BuildableInstance>();
            if (buildableInstance != null && buildableInstance.def != null)
            {
                buildingName = buildableInstance.def.displayName;
            }

            data.buildings.Add(new BuildingInfo
            {
                buildingName = buildingName,
                buildingType = buildingType,
                gameObject = building
            });
        }

        // 计算资源流动（每分钟）
        data.resourceFlows = CalculateResourceFlows(baseInstance);

        // 收集资源区信息
        if (baseInstance.ResourceZone != null)
        {
            data.resourceZoneTypeId = baseInstance.ResourceZone.zoneId;
            data.resourceZoneName = baseInstance.ResourceZone.displayName;
            data.zoneInfo = new ResourceZoneInfo(baseInstance.ResourceZone);
        }

        return data;
    }

    /// <summary>
    /// 从BaseSaveData收集信息（用于大地图显示）
    /// </summary>
    public static BaseInfoData FromBaseSaveData(BaseSaveData saveData)
    {
        if (saveData == null) return null;

        var data = new BaseInfoData
        {
            baseId = saveData.baseId,
            baseName = saveData.baseName,
            worldPosition = saveData.worldPosition,
            money = saveData.money,
            resourceZoneTypeId = saveData.resourceZoneTypeId
        };

        // 从保存数据中获取资源信息
        int totalCapacity = saveData.baseCapacity;
        if (saveData.resources != null)
        {
            foreach (var resSave in saveData.resources)
            {
                ResourceDefinition resDef = FindResourceDefinition(resSave.resourceName);
                if (resDef != null)
                {
                    data.resources.Add(new ResourceStockInfo(
                        resDef,
                        resSave.amount,
                        totalCapacity
                    ));
                }
            }
        }

        // 从保存数据加载建筑信息
        if (saveData.buildings != null && saveData.buildings.Count > 0)
        {
            // 统计每种建筑的数量和类型
            var buildingStats = new Dictionary<string, (int count, string defName)>();
            foreach (var buildingSave in saveData.buildings)
            {
                // 加载 BuildableDefinition 获取 displayName
                var buildDef = FindBuildableDefinition(buildingSave.buildingDefName);
                string displayName = buildDef != null ? buildDef.displayName : buildingSave.buildingDefName;

                if (!buildingStats.ContainsKey(displayName))
                    buildingStats[displayName] = (0, buildingSave.buildingDefName);

                var current = buildingStats[displayName];
                buildingStats[displayName] = (current.count + 1, current.defName);
            }

            // 创建建筑信息列表
            foreach (var kvp in buildingStats)
            {
                data.buildings.Add(new BuildingInfo
                {
                    buildingName = kvp.Key,
                    buildingType = "Building",
                    count = kvp.Value.count,
                    status = "",  // 离线状态下无法获取实时状态
                    gameObject = null
                });
            }
        }

        // 从保存数据加载资源流动信息
        if (saveData.resourceFlows != null && saveData.resourceFlows.Count > 0)
        {
            Debug.Log($"[BaseInfoData] Loading {saveData.resourceFlows.Count} resource flows from save data");
            foreach (var flowSave in saveData.resourceFlows)
            {
                ResourceDefinition resDef = FindResourceDefinition(flowSave.resourceName);
                if (resDef != null)
                {
                    Debug.Log($"[BaseInfoData] Flow: {flowSave.resourceName}, consume={flowSave.consumptionPerMinute}, produce={flowSave.productionPerMinute}");
                    data.resourceFlows.Add(new ResourceFlowInfo(
                        resDef,
                        flowSave.consumptionPerMinute,
                        flowSave.productionPerMinute
                    ));
                }
                else
                {
                    Debug.LogWarning($"[BaseInfoData] Could not find ResourceDefinition for: {flowSave.resourceName}");
                }
            }
        }

        // 查找并设置资源区信息
        if (!string.IsNullOrEmpty(saveData.resourceZoneTypeId))
        {
            var zoneType = FindResourceZoneType(saveData.resourceZoneTypeId);
            if (zoneType != null)
            {
                data.resourceZoneName = zoneType.displayName;
                data.zoneInfo = new ResourceZoneInfo(zoneType);
            }
        }

        return data;
    }

    // ========== 缓存系统 ==========
    private static Dictionary<string, ResourceDefinition> _resourceCache = new Dictionary<string, ResourceDefinition>();
    private static Dictionary<string, BuildableDefinition> _buildableCache = new Dictionary<string, BuildableDefinition>();
    private static Dictionary<string, ResourceZoneType> _zoneTypeCache = new Dictionary<string, ResourceZoneType>();
    private static bool _cachesPreloaded = false;

    /// <summary>
    /// 预加载所有ScriptableObject到缓存（避免编辑器空闲时的LINQ查询）
    /// </summary>
    public static void PreloadAllCaches()
    {
        if (_cachesPreloaded) return;

        // 预加载所有ResourceDefinition
        var allResources = Resources.LoadAll<ResourceDefinition>("");
        foreach (var res in allResources)
        {
            if (res != null && !string.IsNullOrEmpty(res.name))
                _resourceCache[res.name] = res;
        }

        // 预加载所有BuildableDefinition
        var allBuildables = Resources.LoadAll<BuildableDefinition>("");
        foreach (var build in allBuildables)
        {
            if (build != null && !string.IsNullOrEmpty(build.name))
                _buildableCache[build.name] = build;
        }

        // 预加载所有ResourceZoneType
        var allZoneTypes = Resources.FindObjectsOfTypeAll<ResourceZoneType>();
        foreach (var zone in allZoneTypes)
        {
            if (zone != null && !string.IsNullOrEmpty(zone.zoneId))
                _zoneTypeCache[zone.zoneId] = zone;
        }

        _cachesPreloaded = true;
        Debug.Log($"[BaseInfoData] Caches preloaded: {_resourceCache.Count} resources, {_buildableCache.Count} buildables, {_zoneTypeCache.Count} zones");
    }

    /// <summary>
    /// 清空所有缓存（可选，在编辑器模式下资源更新时调用）
    /// </summary>
    public static void ClearAllCaches()
    {
        _resourceCache.Clear();
        _buildableCache.Clear();
        _zoneTypeCache.Clear();
        _cachesPreloaded = false;
    }

    /// <summary>
    /// 查找 ResourceDefinition（带缓存）
    /// </summary>
    private static ResourceDefinition FindResourceDefinition(string resourceName)
    {
        if (string.IsNullOrEmpty(resourceName)) return null;

        // 首次调用时预加载所有缓存
        if (!_cachesPreloaded)
            PreloadAllCaches();

        // 从缓存查找
        if (_resourceCache.TryGetValue(resourceName, out var cached))
            return cached;

        // 缓存未命中，尝试直接加载并添加到缓存
        var resDef = Resources.Load<ResourceDefinition>($"Resources/{resourceName}");
        if (resDef != null)
        {
            _resourceCache[resourceName] = resDef;
        }
        return resDef;
    }

    /// <summary>
    /// 查找 BuildableDefinition（带缓存）
    /// </summary>
    private static BuildableDefinition FindBuildableDefinition(string defName)
    {
        if (string.IsNullOrEmpty(defName)) return null;

        // 首次调用时预加载所有缓存
        if (!_cachesPreloaded)
            PreloadAllCaches();

        // 从缓存查找
        if (_buildableCache.TryGetValue(defName, out var cached))
            return cached;

        // 缓存未命中，尝试直接加载并添加到缓存
        var buildDef = Resources.Load<BuildableDefinition>($"Buildings/{defName}");
        if (buildDef != null)
        {
            _buildableCache[defName] = buildDef;
        }
        return buildDef;
    }

    /// <summary>
    /// 查找 ResourceZoneType（带缓存）
    /// </summary>
    private static ResourceZoneType FindResourceZoneType(string zoneId)
    {
        if (string.IsNullOrEmpty(zoneId)) return null;

        // 首次调用时预加载所有缓存
        if (!_cachesPreloaded)
            PreloadAllCaches();

        // 从缓存查找
        if (_zoneTypeCache.TryGetValue(zoneId, out var cached))
            return cached;

        // 缓存未命中，尝试从 BaseManager 获取
        if (BaseManager.Instance != null)
        {
            var zoneType = BaseManager.Instance.FindResourceZoneType(zoneId);
            if (zoneType != null)
            {
                _zoneTypeCache[zoneId] = zoneType;
                return zoneType;
            }
        }

        return null;
    }

    /// <summary>
    /// 计算资源流动（每分钟）
    /// </summary>
    private static List<ResourceFlowInfo> CalculateResourceFlows(BaseInstance baseInstance)
    {
        var flowDict = new Dictionary<ResourceDefinition, (float consume, float produce)>();

        // 收集所有ProducerBuilding的输入输出
        var producers = baseInstance.GetBuildingsOfType<ProducerBuilding>();
        foreach (var producer in producers)
        {
            if (producer == null) continue;

            // 计算效率
            float efficiency = 1f;  // 简化计算，实际应该考虑工人效率和卡牌加成

            // 如果有方法获取实际效率，使用它
            if (producer.LastEfficiency > 0)
                efficiency = producer.LastEfficiency;

            // 输入（消耗）- 每秒 -> 每分钟
            if (producer.inputsPerSecond != null)
            {
                foreach (var input in producer.inputsPerSecond)
                {
                    if (input.res == null) continue;

                    if (!flowDict.ContainsKey(input.res))
                        flowDict[input.res] = (0f, 0f);

                    var current = flowDict[input.res];
                    current.consume += input.amount * efficiency * 60f;  // 每秒 -> 每分钟
                    flowDict[input.res] = current;
                }
            }

            // 输出（生产）- 每秒 -> 每分钟
            if (producer.outputsPerSecond != null)
            {
                foreach (var output in producer.outputsPerSecond)
                {
                    if (output.res == null) continue;

                    if (!flowDict.ContainsKey(output.res))
                        flowDict[output.res] = (0f, 0f);

                    var current = flowDict[output.res];
                    current.produce += output.amount * efficiency * 60f;  // 每秒 -> 每分钟
                    flowDict[output.res] = current;
                }
            }
        }

        // 转换为列表
        var flows = new List<ResourceFlowInfo>();
        foreach (var kvp in flowDict)
        {
            flows.Add(new ResourceFlowInfo(
                kvp.Key,
                kvp.Value.consume,
                kvp.Value.produce
            ));
        }

        return flows;
    }

    /// <summary>
    /// 获取格式化的资源流动文本
    /// 例如: "铁矿: -10 + 20 = +10 /min"
    /// </summary>
    public string GetResourceFlowText(ResourceDefinition resource)
    {
        var flow = resourceFlows.FirstOrDefault(f => f.resource == resource);
        if (flow == null) return $"{resource.displayName}: 0/min";

        string netSign = flow.net >= 0 ? "+" : "";
        return $"{resource.displayName}: -{flow.consumption:F1} + {flow.production:F1} = {netSign}{flow.net:F1}/min";
    }

    /// <summary>
    /// 获取所有资源流动的摘要文本
    /// </summary>
    public string GetResourceFlowsSummary()
    {
        if (resourceFlows.Count == 0)
            return "No resource flows";

        var lines = new List<string>();
        foreach (var flow in resourceFlows)
        {
            if (flow.resource == null) continue;

            string netSign = flow.net >= 0 ? "+" : "";
            lines.Add($"{flow.resource.displayName}: {netSign}{flow.net:F1}/min");
        }

        return string.Join("\n", lines);
    }
}