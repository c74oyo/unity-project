using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战斗奖励分配器 — 胜利后发放金钱和道具掉落
/// 场景级组件，挂在 BattleManager 同一 GameObject 上
/// </summary>
public class BattleRewardDistributor : MonoBehaviour
{
    [Header("奖励配置（Inspector 设置本场战斗的奖励）")]
    public BattleRewardConfig rewardConfig;

    [Header("References")]
    public BattleManager battleManager;

    // ============ Runtime ============

    private BattleRewardResult _lastResult;

    /// <summary>
    /// 最近一次战斗的奖励结果（供 BattleUI 读取显示）
    /// </summary>
    public BattleRewardResult LastResult => _lastResult;

    // ============ Events ============

    /// <summary>
    /// 奖励分配完成事件
    /// </summary>
    public event Action<BattleRewardResult> OnRewardsDistributed;

    // ============ Lifecycle ============

    private void Start()
    {
        if (battleManager == null)
            battleManager = GetComponent<BattleManager>();
        if (battleManager == null)
            battleManager = BattleManager.Instance;

        if (battleManager != null)
            battleManager.OnBattleEnded += HandleBattleEnded;
    }

    private void OnDestroy()
    {
        if (battleManager != null)
            battleManager.OnBattleEnded -= HandleBattleEnded;
    }

    // ============ Reward Logic ============

    private void HandleBattleEnded(bool victory)
    {
        _lastResult = null;

        if (!victory || rewardConfig == null) return;

        _lastResult = new BattleRewardResult();
        _lastResult.moneyEarned = rewardConfig.moneyReward;

        // Roll 道具掉落
        foreach (var drop in rewardConfig.itemDrops)
        {
            if (string.IsNullOrEmpty(drop.itemId)) continue;

            float roll = UnityEngine.Random.value;
            if (roll <= drop.dropChance)
            {
                int amount = UnityEngine.Random.Range(drop.minAmount, drop.maxAmount + 1);
                if (amount > 0)
                    _lastResult.itemsObtained.Add(new ItemStackSaveData(drop.itemId, amount));
            }
        }

        // 分发金币 → 当前活跃基地
        if (_lastResult.moneyEarned > 0 && BaseManager.Instance != null)
        {
            var baseSave = BaseManager.Instance.GetActiveBaseSaveData();
            if (baseSave != null)
            {
                baseSave.money += _lastResult.moneyEarned;
                BaseManager.Instance.UpdateBaseSaveData(baseSave);
                Debug.Log($"[BattleReward] +{_lastResult.moneyEarned} gold → base {baseSave.baseId}");
            }
        }

        // 分发道具 → 玩家背包
        if (PlayerInventoryManager.Instance != null)
        {
            foreach (var stack in _lastResult.itemsObtained)
            {
                PlayerInventoryManager.Instance.AddItem(stack.itemId, stack.count);
            }
        }

        OnRewardsDistributed?.Invoke(_lastResult);

        Debug.Log($"[BattleReward] Victory! Money: {_lastResult.moneyEarned}, " +
                  $"Items: {_lastResult.itemsObtained.Count} types dropped");
    }
}
