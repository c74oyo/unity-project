using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================
// 养成系统存档数据类（全部 [Serializable]，用于 JsonUtility 序列化）
// ============================================================

// ============ 玩家背包存档 ============

/// <summary>
/// 玩家背包存档数据
/// </summary>
[Serializable]
public class PlayerInventorySaveData
{
    public List<ItemStackSaveData> items = new();
    public List<OwnedEquipmentSaveData> equipment = new();
}

/// <summary>
/// 道具堆叠数据
/// </summary>
[Serializable]
public class ItemStackSaveData
{
    public string itemId;
    public int count;

    public ItemStackSaveData() { }
    public ItemStackSaveData(string id, int c) { itemId = id; count = c; }
}

/// <summary>
/// 拥有的装备实例数据
/// 同一个 EquipmentDefinition 可拥有多件，各自有独立的实例ID和强化等级
/// </summary>
[Serializable]
public class OwnedEquipmentSaveData
{
    [Tooltip("唯一实例ID（GUID）")]
    public string instanceId;

    [Tooltip("装备定义ID (EquipmentDefinition.equipId)")]
    public string equipDefId;

    [Tooltip("当前强化等级")]
    public int level;

    [Tooltip("装备给哪个角色 (CharacterCard.name)，空串=未装备")]
    public string equippedToUnitId = "";

    public OwnedEquipmentSaveData() { }
}

// ============ 角色养成存档 ============

/// <summary>
/// 角色养成存档数据
/// </summary>
[Serializable]
public class CharacterProgressionSaveData
{
    [Tooltip("拥有的角色ID列表 (CharacterCard.name)")]
    public List<string> ownedUnitIds = new();

    [Tooltip("各角色的等级数据")]
    public List<CharacterLevelData> characterLevels = new();
}

/// <summary>
/// 单个角色的等级数据
/// </summary>
[Serializable]
public class CharacterLevelData
{
    public string unitId;
    public int level;
    public int totalExp;

    public CharacterLevelData() { }
    public CharacterLevelData(string id, int lvl, int exp)
    {
        unitId = id;
        level = lvl;
        totalExp = exp;
    }
}

// ============ 战斗奖励配置 ============

/// <summary>
/// 战斗奖励配置（在 BattleRewardDistributor 的 Inspector 中设置）
/// </summary>
[Serializable]
public class BattleRewardConfig
{
    [Tooltip("金币奖励")]
    public float moneyReward = 100f;

    [Tooltip("道具掉落列表")]
    public List<ItemDropEntry> itemDrops = new();
}

/// <summary>
/// 单条掉落配置
/// </summary>
[Serializable]
public class ItemDropEntry
{
    [Tooltip("道具ID (ItemDefinition.itemId)")]
    public string itemId;

    [Tooltip("最小掉落数量")]
    public int minAmount = 1;

    [Tooltip("最大掉落数量")]
    public int maxAmount = 3;

    [Tooltip("掉落概率 (0-1)")]
    [Range(0f, 1f)]
    public float dropChance = 1f;
}

/// <summary>
/// 战斗奖励结算结果（运行时生成）
/// </summary>
[Serializable]
public class BattleRewardResult
{
    public float moneyEarned;
    public List<ItemStackSaveData> itemsObtained = new();
}
