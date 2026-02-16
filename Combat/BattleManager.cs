using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战斗管理器 - 回合制战旗战斗的核心控制器
/// 管理战斗阶段、单位生成、胜负判定
/// 场景级单例（不使用 DontDestroyOnLoad）
/// </summary>
public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    // ============ References ============

    [Header("References")]
    public TacticalGrid grid;
    public TacticalGridRenderer gridRenderer;
    public TacticalInputHandler inputHandler;
    public EnemyAI enemyAI;

    // ============ Test Config ============

    [Header("Standalone Test Config")]
    [Tooltip("勾选后 Start() 自动以 testConfig 开始战斗")]
    public bool autoStartTestBattle = true;

    [Tooltip("测试用战斗配置（Inspector 中配置）")]
    public BattleConfig testConfig;

    // ============ Runtime ============

    private BattlePhase _currentPhase = BattlePhase.Setup;
    private int _turnNumber;
    private List<TacticalUnit> _playerUnits = new();
    private List<TacticalUnit> _enemyUnits = new();

    // ============ Properties ============

    public BattlePhase CurrentPhase => _currentPhase;
    public int TurnNumber => _turnNumber;
    public List<TacticalUnit> PlayerUnits => _playerUnits;
    public List<TacticalUnit> EnemyUnits => _enemyUnits;

    // ============ Events ============

    public event Action<BattlePhase> OnPhaseChanged;
    public event Action<TacticalUnit> OnUnitDied;
    public event Action<bool> OnBattleEnded; // true=victory, false=defeat

    // ============ Lifecycle ============

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 自动获取同 GameObject 上的组件
        if (grid == null) grid = GetComponent<TacticalGrid>();
        if (gridRenderer == null) gridRenderer = GetComponent<TacticalGridRenderer>();
        if (inputHandler == null) inputHandler = GetComponent<TacticalInputHandler>();
        if (enemyAI == null) enemyAI = GetComponent<EnemyAI>();
    }

    private void Start()
    {
        if (autoStartTestBattle && testConfig != null)
        {
            StartBattle(testConfig);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ============ Battle Flow ============

    /// <summary>
    /// 开始战斗
    /// </summary>
    public void StartBattle(BattleConfig config)
    {
        if (config == null)
        {
            Debug.LogError("[BattleManager] BattleConfig is null!");
            return;
        }

        Debug.Log("[BattleManager] Starting battle...");

        // 初始化网格
        grid.Initialize(config);

        // 关联 renderer
        if (gridRenderer != null)
            gridRenderer.grid = grid;

        _turnNumber = 0;
        _playerUnits.Clear();
        _enemyUnits.Clear();

        // 生成玩家单位
        foreach (var placement in config.playerUnits)
        {
            if (placement.definition == null)
            {
                Debug.LogWarning("[BattleManager] Player unit placement missing CharacterCard, skipped.");
                continue;
            }
            var unit = TacticalUnit.SpawnUnit(placement.definition, placement.spawnCell,
                                                CombatTeam.Player, grid);
            unit.OnDeath += HandleUnitDeath;
            _playerUnits.Add(unit);
        }

        // 生成敌方单位
        foreach (var placement in config.enemyUnits)
        {
            if (placement.definition == null)
            {
                Debug.LogWarning("[BattleManager] Enemy unit placement missing CharacterCard, skipped.");
                continue;
            }
            var unit = TacticalUnit.SpawnUnit(placement.definition, placement.spawnCell,
                                                CombatTeam.Enemy, grid);
            unit.OnDeath += HandleUnitDeath;
            _enemyUnits.Add(unit);
        }

        Debug.Log($"[BattleManager] Spawned {_playerUnits.Count} player units, {_enemyUnits.Count} enemy units");

        // 进入玩家阶段
        StartPlayerPhase();
    }

    // ============ Phase Management ============

    private void SetPhase(BattlePhase phase)
    {
        _currentPhase = phase;
        OnPhaseChanged?.Invoke(phase);
        Debug.Log($"[BattleManager] Phase: {phase} (Turn {_turnNumber})");
    }

    /// <summary>
    /// 开始玩家阶段
    /// </summary>
    private void StartPlayerPhase()
    {
        _turnNumber++;
        SetPhase(BattlePhase.PlayerPhase);

        // 重置所有玩家单位
        foreach (var unit in _playerUnits)
        {
            if (unit != null && unit.IsAlive)
                unit.ResetForNewTurn();
        }

        // 启用玩家输入
        if (inputHandler != null)
            inputHandler.SetEnabled(true);
    }

    /// <summary>
    /// 开始敌方阶段
    /// </summary>
    private void StartEnemyPhase()
    {
        SetPhase(BattlePhase.EnemyPhase);

        // 禁用玩家输入
        if (inputHandler != null)
            inputHandler.SetEnabled(false);

        // 重置所有敌方单位
        foreach (var unit in _enemyUnits)
        {
            if (unit != null && unit.IsAlive)
                unit.ResetForNewTurn();
        }

        // 启动 AI
        if (enemyAI != null)
        {
            enemyAI.ExecuteEnemyTurn(_enemyUnits, _playerUnits, grid, OnEnemyTurnFinished);
        }
        else
        {
            OnEnemyTurnFinished();
        }
    }

    /// <summary>
    /// 结束玩家回合（由 EndTurn 按钮或所有单位行动完毕触发）
    /// </summary>
    public void EndPlayerTurn()
    {
        if (_currentPhase != BattlePhase.PlayerPhase) return;

        // 禁用输入 & 清除高亮
        if (inputHandler != null)
        {
            inputHandler.SetEnabled(false);
            inputHandler.DeselectUnit();
        }

        // 检查胜负
        if (CheckBattleEnd()) return;

        StartEnemyPhase();
    }

    /// <summary>
    /// 敌方回合结束回调
    /// </summary>
    private void OnEnemyTurnFinished()
    {
        if (CheckBattleEnd()) return;
        StartPlayerPhase();
    }

    // ============ Victory / Defeat ============

    /// <summary>
    /// 检查战斗是否结束
    /// </summary>
    private bool CheckBattleEnd()
    {
        // 清理已死亡的引用
        _playerUnits.RemoveAll(u => u == null || !u.IsAlive);
        _enemyUnits.RemoveAll(u => u == null || !u.IsAlive);

        if (_enemyUnits.Count == 0)
        {
            SetPhase(BattlePhase.Victory);
            OnBattleEnded?.Invoke(true);
            Debug.Log("[BattleManager] VICTORY!");
            return true;
        }

        if (_playerUnits.Count == 0)
        {
            SetPhase(BattlePhase.Defeat);
            OnBattleEnded?.Invoke(false);
            Debug.Log("[BattleManager] DEFEAT!");
            return true;
        }

        return false;
    }

    // ============ Unit Death ============

    private void HandleUnitDeath(TacticalUnit unit)
    {
        OnUnitDied?.Invoke(unit);

        // 延迟检查（等待死亡动画）
        Invoke(nameof(DelayedBattleEndCheck), 0.6f);
    }

    private void DelayedBattleEndCheck()
    {
        if (_currentPhase == BattlePhase.Victory || _currentPhase == BattlePhase.Defeat)
            return;

        CheckBattleEnd();
    }

    // ============ Queries ============

    /// <summary>
    /// 检查所有玩家单位是否都已行动完毕
    /// </summary>
    public bool AreAllPlayerUnitsDone()
    {
        foreach (var unit in _playerUnits)
        {
            if (unit != null && unit.IsAlive && unit.State != UnitState.Done)
                return false;
        }
        return true;
    }

    /// <summary>
    /// 获取指定阵营的存活单位列表
    /// </summary>
    public List<TacticalUnit> GetAliveUnits(CombatTeam team)
    {
        var source = team == CombatTeam.Player ? _playerUnits : _enemyUnits;
        var result = new List<TacticalUnit>();
        foreach (var unit in source)
        {
            if (unit != null && unit.IsAlive)
                result.Add(unit);
        }
        return result;
    }
}
