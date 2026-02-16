using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战术输入处理器 - 处理玩家在战斗中的点击操作
/// 流程：选中单位 → 显示移动范围 → 点击移动 → 显示攻击范围 → 点击攻击/跳过
/// </summary>
public class TacticalInputHandler : MonoBehaviour
{
    // ============ References ============

    [Header("References")]
    public TacticalGrid grid;
    public TacticalGridRenderer gridRenderer;
    public BattleManager battleManager;

    [Header("Raycast")]
    [Tooltip("地面层（用于射线检测）")]
    public LayerMask groundLayer;

    // ============ Runtime ============

    private bool _enabled;
    private TacticalUnit _selectedUnit;
    private Dictionary<Vector2Int, Vector2Int> _currentReachable; // BFS parent map
    private HashSet<Vector2Int> _currentAttackCells;
    private List<TacticalUnit> _currentAttackableEnemies;

    // ============ Properties ============

    public TacticalUnit SelectedUnit => _selectedUnit;

    // ============ Enable / Disable ============

    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        if (!enabled)
        {
            DeselectUnit();
        }
    }

    // ============ Update ============

    private void Awake()
    {
        if (grid == null) grid = GetComponent<TacticalGrid>();
        if (gridRenderer == null) gridRenderer = GetComponent<TacticalGridRenderer>();
        if (battleManager == null) battleManager = GetComponent<BattleManager>();
    }

    private void Update()
    {
        if (!_enabled) return;
        if (battleManager == null || battleManager.CurrentPhase != BattlePhase.PlayerPhase) return;

        // 更新鼠标悬停高亮
        UpdateHover();

        // 左键点击
        if (Input.GetMouseButtonDown(0))
        {
            HandleLeftClick();
        }

        // 右键 / ESC 取消
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            HandleCancel();
        }

        // 自动检查：所有单位行动完毕
        if (battleManager.AreAllPlayerUnitsDone())
        {
            battleManager.EndPlayerTurn();
        }
    }

    // ============ Hover ============

    private void UpdateHover()
    {
        if (gridRenderer == null) return;

        Vector2Int cell = GetCellUnderMouse();
        gridRenderer.SetHoverCell(cell);
    }

    // ============ Left Click ============

    private void HandleLeftClick()
    {
        Vector2Int cell = GetCellUnderMouse();
        if (!grid.IsInBounds(cell)) return;

        // ---- 状态分支 ----

        if (_selectedUnit == null)
        {
            // 没有选中单位 → 尝试选中
            TrySelectUnit(cell);
        }
        else if (_selectedUnit.State == UnitState.Selected)
        {
            // 已选中、还没移动 → 尝试移动到目标格子 / 或重新选择
            HandleMoveOrReselect(cell);
        }
        else if (_selectedUnit.State == UnitState.WaitingForAttackTarget)
        {
            // 等待攻击目标 → 尝试攻击
            HandleAttackTarget(cell);
        }
    }

    // ============ Cancel ============

    private void HandleCancel()
    {
        if (_selectedUnit == null) return;

        if (_selectedUnit.State == UnitState.WaitingForAttackTarget)
        {
            // 跳过攻击，直接结束行动
            _selectedUnit.MarkDone();
            DeselectUnit();
        }
        else
        {
            // 取消选择
            DeselectUnit();
        }
    }

    // ============ Selection ============

    private void TrySelectUnit(Vector2Int cell)
    {
        var unit = grid.GetUnitAt(cell);
        if (unit == null) return;
        if (unit.Team != CombatTeam.Player) return;
        if (!unit.CanAct) return;

        SelectUnit(unit);
    }

    private void SelectUnit(TacticalUnit unit)
    {
        // 取消之前的选中
        if (_selectedUnit != null)
            _selectedUnit.SetSelected(false);

        _selectedUnit = unit;
        _selectedUnit.SetSelected(true);

        // 显示移动范围
        ShowMoveRange(unit);

        // 通知 UI
        BattleUI.Instance?.ShowUnitInfo(unit);
    }

    /// <summary>
    /// 取消选中
    /// </summary>
    public void DeselectUnit()
    {
        if (_selectedUnit != null)
        {
            _selectedUnit.SetSelected(false);
            _selectedUnit = null;
        }

        _currentReachable = null;
        _currentAttackCells = null;
        _currentAttackableEnemies = null;

        if (gridRenderer != null)
            gridRenderer.ClearAllHighlights();

        BattleUI.Instance?.HideUnitInfo();
    }

    // ============ Movement ============

    private void ShowMoveRange(TacticalUnit unit)
    {
        if (gridRenderer == null) return;

        _currentReachable = grid.GetReachableCells(unit.CellPosition, unit.moveRange);

        gridRenderer.ClearAllHighlights();
        gridRenderer.SetWalkableHighlight(_currentReachable.Keys);
        gridRenderer.SetSelectedUnitCell(unit.CellPosition);
    }

    private void HandleMoveOrReselect(Vector2Int cell)
    {
        // 点击自己 → 原地不动，直接进入攻击阶段
        if (cell == _selectedUnit.CellPosition)
        {
            _selectedUnit.HasMoved = true;
            _selectedUnit.State = UnitState.WaitingForAttackTarget;
            ShowAttackRange(_selectedUnit);
            return;
        }

        // 点击其他己方单位 → 切换选择
        var unitAtCell = grid.GetUnitAt(cell);
        if (unitAtCell != null && unitAtCell.Team == CombatTeam.Player && unitAtCell.CanAct)
        {
            SelectUnit(unitAtCell);
            return;
        }

        // 点击可移动范围内的格子 → 移动
        if (_currentReachable != null && _currentReachable.ContainsKey(cell))
        {
            var path = grid.ReconstructPath(_currentReachable, _selectedUnit.CellPosition, cell);
            if (path.Count >= 2)
            {
                gridRenderer.ClearAllHighlights();

                var unit = _selectedUnit;
                unit.StartMovement(path, () =>
                {
                    // 移动完成后显示攻击范围
                    if (unit.IsAlive)
                    {
                        ShowAttackRange(unit);
                    }
                });
            }
        }
    }

    // ============ Attack ============

    private void ShowAttackRange(TacticalUnit unit)
    {
        if (gridRenderer == null) return;

        _currentAttackCells = grid.GetAttackRangeCells(unit.CellPosition, unit.attackRange);
        _currentAttackableEnemies = grid.GetAttackableEnemies(unit.CellPosition, unit.attackRange, unit.Team);

        gridRenderer.ClearAllHighlights();
        gridRenderer.SetAttackHighlight(_currentAttackCells);
        gridRenderer.SetSelectedUnitCell(unit.CellPosition);

        // 如果没有可攻击目标，直接结束
        if (_currentAttackableEnemies.Count == 0)
        {
            unit.MarkDone();
            DeselectUnit();
        }
    }

    private void HandleAttackTarget(Vector2Int cell)
    {
        var target = grid.GetUnitAt(cell);

        // 点击了攻击范围内的敌方单位
        if (target != null && target.IsAlive && target.Team != CombatTeam.Player
            && _currentAttackCells != null && _currentAttackCells.Contains(cell))
        {
            gridRenderer.ClearAllHighlights();

            var attacker = _selectedUnit;
            attacker.PerformAttack(target, () =>
            {
                // 攻击完成
                DeselectUnit();

                // 检查战斗是否结束
                if (battleManager != null)
                {
                    // 自动检查在 Update 中也会触发
                }
            });
        }
    }

    // ============ Raycast Utility ============

    /// <summary>
    /// 获取鼠标下方的网格坐标
    /// </summary>
    private Vector2Int GetCellUnderMouse()
    {
        if (Camera.main == null) return new Vector2Int(-1, -1);

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 200f, groundLayer))
        {
            return grid.WorldToCell(hit.point);
        }

        // 备用：如果没有碰到地面，用 Y=0 平面
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        if (groundPlane.Raycast(ray, out float enter))
        {
            Vector3 point = ray.GetPoint(enter);
            return grid.WorldToCell(point);
        }

        return new Vector2Int(-1, -1);
    }
}
