using System;
using UnityEngine;

/// <summary>
/// 战斗任务处理接口（预留）
/// 战斗系统实现后，创建一个实现此接口的类并注册到 QuestManager
/// </summary>
public interface ICombatQuestHandler
{
    /// <summary>
    /// 检查是否可以在目标位置开战
    /// </summary>
    /// <param name="targetCell">目标格子坐标</param>
    /// <param name="threatLevel">威胁等级</param>
    /// <returns>是否可以开始战斗</returns>
    bool CanStartCombat(Vector2Int targetCell, int threatLevel);

    /// <summary>
    /// 开始战斗
    /// </summary>
    /// <param name="questInstanceId">关联的任务实例ID</param>
    /// <param name="targetCell">目标格子坐标</param>
    /// <param name="threatLevel">威胁等级</param>
    /// <param name="onComplete">战斗完成回调（true=胜利, false=失败）</param>
    void StartCombat(string questInstanceId, Vector2Int targetCell,
                     int threatLevel, Action<bool> onComplete);

    /// <summary>
    /// 检查指定任务的战斗是否正在进行中
    /// </summary>
    bool IsCombatInProgress(string questInstanceId);
}
