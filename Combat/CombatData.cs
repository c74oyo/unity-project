using System;
using System.Collections.Generic;
using UnityEngine;

// ============ Enums ============

/// <summary>
/// 战斗阵营
/// </summary>
public enum CombatTeam
{
    Player,
    Enemy
}

/// <summary>
/// 单位行动状态
/// </summary>
public enum UnitState
{
    Idle,                    // 等待操作
    Selected,                // 已被选中（显示移动范围）
    Moving,                  // 正在移动中
    WaitingForAttackTarget,  // 移动完成，等待选择攻击目标
    Attacking,               // 正在执行攻击动画
    Done                     // 本回合行动结束
}

/// <summary>
/// 战斗阶段
/// </summary>
public enum BattlePhase
{
    Setup,        // 初始化
    PlayerPhase,  // 玩家回合
    EnemyPhase,   // 敌方回合
    Victory,      // 胜利
    Defeat        // 失败
}

/// <summary>
/// 网格单元状态
/// </summary>
public enum CellState
{
    Empty,
    Blocked,
    OccupiedByPlayer,
    OccupiedByEnemy
}

// ============ Data Classes ============

/// <summary>
/// 单位部署条目 — 引用 CharacterCard + 出生坐标
/// 在 BattleConfig 的 Inspector 里拖拽 CharacterCard 资源并设置 spawnCell
/// </summary>
[Serializable]
public class UnitPlacement
{
    [Tooltip("拖入 CharacterCard 资源")]
    public CharacterCard definition;

    [Tooltip("出生格子坐标")]
    public Vector2Int spawnCell;
}

/// <summary>
/// 战斗配置（Inspector 可配置，用于独立测试场景）
/// </summary>
[Serializable]
public class BattleConfig
{
    [Tooltip("网格尺寸")]
    public Vector2Int gridSize = new(30, 30);

    [Tooltip("每格世界单位大小")]
    public float cellSize = 1f;

    [Tooltip("玩家单位部署（拖入 CharacterCard + 设置出生坐标）")]
    public List<UnitPlacement> playerUnits = new();

    [Tooltip("敌方单位部署（拖入 CharacterCard + 设置出生坐标）")]
    public List<UnitPlacement> enemyUnits = new();

    [Tooltip("障碍物坐标列表")]
    public List<Vector2Int> obstacles = new();
}
