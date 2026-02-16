using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌方AI - 简单的逐个行动AI
/// 策略：找最近的玩家单位 → 移动靠近 → 攻击
/// 使用协程顺序执行，每个单位之间有延迟（便于观察）
/// </summary>
public class EnemyAI : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("每个敌方单位行动之间的延迟（秒）")]
    public float delayBetweenUnits = 0.8f;

    [Tooltip("移动到攻击之间的延迟（秒）")]
    public float delayBeforeAttack = 0.3f;

    // ============ Public API ============

    /// <summary>
    /// 执行敌方回合（由 BattleManager 调用）
    /// </summary>
    public void ExecuteEnemyTurn(List<TacticalUnit> enemies, List<TacticalUnit> players,
                                   TacticalGrid grid, Action onComplete)
    {
        StartCoroutine(EnemyTurnCoroutine(enemies, players, grid, onComplete));
    }

    // ============ Coroutine ============

    private IEnumerator EnemyTurnCoroutine(List<TacticalUnit> enemies, List<TacticalUnit> players,
                                             TacticalGrid grid, Action onComplete)
    {
        yield return new WaitForSeconds(0.5f); // 回合开始时的短暂停顿

        // 复制列表防止迭代中修改
        var enemyList = new List<TacticalUnit>(enemies);

        foreach (var enemy in enemyList)
        {
            if (enemy == null || !enemy.IsAlive || !enemy.CanAct)
                continue;

            // 刷新存活玩家列表
            var alivePlayers = new List<TacticalUnit>();
            foreach (var p in players)
            {
                if (p != null && p.IsAlive)
                    alivePlayers.Add(p);
            }

            if (alivePlayers.Count == 0)
                break;

            yield return StartCoroutine(ProcessSingleEnemy(enemy, alivePlayers, grid));
            yield return new WaitForSeconds(delayBetweenUnits);
        }

        onComplete?.Invoke();
    }

    /// <summary>
    /// 处理单个敌方单位的行动
    /// </summary>
    private IEnumerator ProcessSingleEnemy(TacticalUnit enemy, List<TacticalUnit> players, TacticalGrid grid)
    {
        // 1. 找最近的玩家单位
        TacticalUnit nearestPlayer = FindNearestTarget(enemy, players);
        if (nearestPlayer == null)
        {
            enemy.MarkDone();
            yield break;
        }

        // 2. 检查是否已在攻击范围内
        int distToTarget = TacticalGrid.ManhattanDistance(enemy.CellPosition, nearestPlayer.CellPosition);
        if (distToTarget <= enemy.attackRange)
        {
            // 直接攻击，不需要移动
            yield return StartCoroutine(DoAttack(enemy, nearestPlayer));
            enemy.MarkDone();
            yield break;
        }

        // 3. 移动向目标靠近
        yield return StartCoroutine(DoMove(enemy, nearestPlayer, grid));

        // 4. 移动后再次检查攻击
        // 重新找最近目标（可能因为移动后距离变化）
        TacticalUnit targetAfterMove = FindNearestTarget(enemy, players);
        if (targetAfterMove != null)
        {
            int dist = TacticalGrid.ManhattanDistance(enemy.CellPosition, targetAfterMove.CellPosition);
            if (dist <= enemy.attackRange)
            {
                yield return new WaitForSeconds(delayBeforeAttack);
                yield return StartCoroutine(DoAttack(enemy, targetAfterMove));
            }
        }

        enemy.MarkDone();
    }

    // ============ AI Actions ============

    /// <summary>
    /// AI移动：BFS寻路 → 移向目标最近的可达格子
    /// </summary>
    private IEnumerator DoMove(TacticalUnit enemy, TacticalUnit target, TacticalGrid grid)
    {
        var reachable = grid.GetReachableCells(enemy.CellPosition, enemy.moveRange);

        // 找到可达范围内离目标最近的格子
        Vector2Int bestCell = enemy.CellPosition;
        int bestDist = TacticalGrid.ManhattanDistance(enemy.CellPosition, target.CellPosition);

        foreach (var cell in reachable.Keys)
        {
            if (cell == enemy.CellPosition) continue;

            int dist = TacticalGrid.ManhattanDistance(cell, target.CellPosition);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestCell = cell;
            }
        }

        // 如果找到更好的位置，移动过去
        if (bestCell != enemy.CellPosition)
        {
            var path = grid.ReconstructPath(reachable, enemy.CellPosition, bestCell);
            if (path.Count >= 2)
            {
                bool moveComplete = false;
                enemy.StartMovement(path, () => moveComplete = true);

                // 等待移动完成
                while (!moveComplete)
                    yield return null;
            }
        }
    }

    /// <summary>
    /// AI攻击：执行攻击并等待动画完成
    /// </summary>
    private IEnumerator DoAttack(TacticalUnit attacker, TacticalUnit target)
    {
        if (target == null || !target.IsAlive) yield break;

        bool attackComplete = false;
        attacker.PerformAttack(target, () => attackComplete = true);

        while (!attackComplete)
            yield return null;
    }

    // ============ Targeting ============

    /// <summary>
    /// 找到距离最近的玩家单位（曼哈顿距离）
    /// </summary>
    private TacticalUnit FindNearestTarget(TacticalUnit enemy, List<TacticalUnit> players)
    {
        TacticalUnit nearest = null;
        int minDist = int.MaxValue;

        foreach (var player in players)
        {
            if (player == null || !player.IsAlive) continue;

            int dist = TacticalGrid.ManhattanDistance(enemy.CellPosition, player.CellPosition);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = player;
            }
        }

        return nearest;
    }
}
