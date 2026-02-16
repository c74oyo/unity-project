using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战术单位组件 - 管理单位的属性、移动、攻击、血条、状态
/// 使用工厂方法 SpawnUnit 创建（Capsule + 世界空间血条）
/// </summary>
public class TacticalUnit : MonoBehaviour
{
    // ============ Stats ============

    [Header("Identity")]
    public string unitName;
    public CombatTeam Team { get; private set; }

    [Header("Stats")]
    public int maxHP;
    public int currentHP;
    public int attack;
    public int defense;
    public int moveRange;
    public int attackRange = 1;

    // ============ State ============

    public UnitState State { get; set; } = UnitState.Idle;
    public Vector2Int CellPosition { get; private set; }
    public bool IsAlive => currentHP > 0;
    public bool HasMoved { get; set; }
    public bool HasAttacked { get; set; }

    // ============ References ============

    private TacticalGrid _grid;
    private Renderer _bodyRenderer;
    private Color _originalColor;
    private Color _doneColor = new(0.4f, 0.4f, 0.4f, 1f);

    // HP Bar
    private Canvas _hpCanvas;
    private Slider _hpSlider;
    private Image _hpFillImage;

    // Movement
    private List<Vector2Int> _movePath;
    private int _moveIndex;
    private float _moveSpeed = 8f;
    private Vector3 _moveTarget;
    private Action _onMoveComplete;

    // Attack visual
    private TacticalUnit _attackTarget;
    private float _attackTimer;
    private const float ATTACK_DURATION = 0.3f;
    private Vector3 _attackStartPos;
    private Action _onAttackComplete;

    // Events
    public event Action<TacticalUnit> OnDeath;

    // ============ Factory ============

    /// <summary>
    /// 工厂方法：根据 CharacterCard + 部署信息创建战术单位
    /// </summary>
    public static TacticalUnit SpawnUnit(CharacterCard def, Vector2Int spawnCell,
                                           CombatTeam team, TacticalGrid grid)
    {
        GameObject go;
        Renderer bodyRenderer;
        Color unitColor = def.fallbackColor;

        if (def.modelPrefab != null)
        {
            // 使用自定义模型
            go = Instantiate(def.modelPrefab);
            go.name = $"Unit_{team}_{def.displayName}";
            bodyRenderer = go.GetComponentInChildren<Renderer>();
        }
        else
        {
            // 无自定义模型 → 默认 Capsule
            go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = $"Unit_{team}_{def.displayName}";
            go.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);

            bodyRenderer = go.GetComponent<Renderer>();
            bodyRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            bodyRenderer.material.color = unitColor;
        }

        // 设置位置
        Vector3 worldPos = grid.CellToWorldCenter(spawnCell);
        worldPos.y = def.modelPrefab != null ? 0f : 0.5f; // 自定义模型贴地，Capsule 半高偏移
        go.transform.position = worldPos;

        // 添加 TacticalUnit 组件
        var unit = go.AddComponent<TacticalUnit>();
        unit.unitName = def.displayName;
        unit.Team = team;

        // 基础属性
        int finalMaxHP = def.maxHP;
        int finalAttack = def.attack;
        int finalDefense = def.defense;
        int finalMoveRange = def.moveRange;
        int finalAttackRange = def.attackRange;

        // 叠加养成加成（仅限玩家单位）
        if (team == CombatTeam.Player && CharacterProgressionManager.Instance != null)
        {
            CombatStatBonus bonus = CharacterProgressionManager.Instance.GetTotalBonus(def.name);
            finalMaxHP += bonus.hp;
            finalAttack += bonus.attack;
            finalDefense += bonus.defense;
            finalMoveRange += bonus.moveRange;
            finalAttackRange += bonus.attackRange;
        }

        unit.maxHP = finalMaxHP;
        unit.currentHP = finalMaxHP;
        unit.attack = finalAttack;
        unit.defense = finalDefense;
        unit.moveRange = finalMoveRange;
        unit.attackRange = finalAttackRange;
        unit.CellPosition = spawnCell;
        unit._grid = grid;
        unit._bodyRenderer = bodyRenderer;
        unit._originalColor = unitColor;

        // 注册到网格
        grid.PlaceUnit(spawnCell, unit);

        // 创建血条
        unit.CreateHPBar();

        return unit;
    }

    // ============ HP Bar ============

    private void CreateHPBar()
    {
        // 世界空间 Canvas
        var hpGo = new GameObject("HPBar");
        hpGo.transform.SetParent(transform);
        hpGo.transform.localPosition = new Vector3(0f, 1.5f, 0f);

        _hpCanvas = hpGo.AddComponent<Canvas>();
        _hpCanvas.renderMode = RenderMode.WorldSpace;
        _hpCanvas.GetComponent<RectTransform>().sizeDelta = new Vector2(1f, 0.15f);

        var scaler = hpGo.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 100;

        // 背景
        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(hpGo.transform, false);
        var bgRect = bgGo.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

        // Slider
        var sliderGo = new GameObject("Slider");
        sliderGo.transform.SetParent(hpGo.transform, false);
        var sliderRect = sliderGo.AddComponent<RectTransform>();
        sliderRect.anchorMin = Vector2.zero;
        sliderRect.anchorMax = Vector2.one;
        sliderRect.offsetMin = new Vector2(2f, 2f);
        sliderRect.offsetMax = new Vector2(-2f, -2f);

        _hpSlider = sliderGo.AddComponent<Slider>();
        _hpSlider.minValue = 0;
        _hpSlider.maxValue = 1;
        _hpSlider.value = 1;
        _hpSlider.interactable = false;

        // Fill Area
        var fillAreaGo = new GameObject("FillArea");
        fillAreaGo.transform.SetParent(sliderGo.transform, false);
        var fillAreaRect = fillAreaGo.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = Vector2.zero;
        fillAreaRect.offsetMax = Vector2.zero;

        // Fill
        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(fillAreaGo.transform, false);
        var fillRect = fillGo.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        _hpFillImage = fillGo.AddComponent<Image>();
        _hpFillImage.color = Team == CombatTeam.Player
            ? new Color(0.2f, 0.8f, 0.2f)
            : new Color(0.9f, 0.2f, 0.2f);

        _hpSlider.fillRect = fillRect;

        // 缩放 canvas 使其在世界空间中合适大小
        hpGo.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
    }

    private void UpdateHPBar()
    {
        if (_hpSlider != null)
        {
            float ratio = maxHP > 0 ? (float)currentHP / maxHP : 0f;
            _hpSlider.value = ratio;

            // 低血量变红
            if (_hpFillImage != null)
            {
                _hpFillImage.color = ratio > 0.3f
                    ? (Team == CombatTeam.Player ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.9f, 0.2f, 0.2f))
                    : new Color(1f, 0.3f, 0.1f);
            }
        }

        // 血条始终面向摄像机
        if (_hpCanvas != null && Camera.main != null)
        {
            _hpCanvas.transform.rotation = Camera.main.transform.rotation;
        }
    }

    // ============ Update ============

    private void Update()
    {
        UpdateHPBar();

        if (State == UnitState.Moving)
            UpdateMovement();

        if (State == UnitState.Attacking)
            UpdateAttackAnimation();
    }

    // ============ Movement ============

    /// <summary>
    /// 开始沿路径移动
    /// </summary>
    public void StartMovement(List<Vector2Int> path, Action onComplete = null)
    {
        if (path == null || path.Count < 2)
        {
            onComplete?.Invoke();
            return;
        }

        _movePath = path;
        _moveIndex = 1; // 第一个是当前位置
        _onMoveComplete = onComplete;

        // 更新网格占位（先移除旧位置）
        var destination = path[path.Count - 1];
        _grid.MoveUnit(CellPosition, destination, this);
        CellPosition = destination;

        // 设置第一个移动目标
        _moveTarget = _grid.CellToWorldCenter(_movePath[_moveIndex]);
        _moveTarget.y = transform.position.y;

        State = UnitState.Moving;
        HasMoved = true;
    }

    private void UpdateMovement()
    {
        if (_movePath == null) return;

        // 平滑移动到目标
        transform.position = Vector3.MoveTowards(transform.position, _moveTarget, _moveSpeed * Time.deltaTime);

        // 到达当前目标点
        if (Vector3.Distance(transform.position, _moveTarget) < 0.01f)
        {
            transform.position = _moveTarget;
            _moveIndex++;

            if (_moveIndex >= _movePath.Count)
            {
                // 移动完成
                State = UnitState.WaitingForAttackTarget;
                _movePath = null;
                _onMoveComplete?.Invoke();
                _onMoveComplete = null;
            }
            else
            {
                _moveTarget = _grid.CellToWorldCenter(_movePath[_moveIndex]);
                _moveTarget.y = transform.position.y;
            }
        }
    }

    // ============ Attack ============

    /// <summary>
    /// 对目标发起攻击
    /// </summary>
    public void PerformAttack(TacticalUnit target, Action onComplete = null)
    {
        if (target == null || !target.IsAlive)
        {
            onComplete?.Invoke();
            return;
        }

        _attackTarget = target;
        _attackTimer = 0f;
        _attackStartPos = transform.position;
        _onAttackComplete = onComplete;
        State = UnitState.Attacking;
        HasAttacked = true;
    }

    private void UpdateAttackAnimation()
    {
        _attackTimer += Time.deltaTime;

        if (_attackTarget != null)
        {
            // 简单的冲击动画：向目标方向移动一小段再返回
            float t = _attackTimer / ATTACK_DURATION;

            if (t < 0.5f)
            {
                // 前半段：向目标冲
                float lerpT = t * 2f;
                Vector3 targetPos = _attackTarget.transform.position;
                Vector3 dir = (targetPos - _attackStartPos).normalized;
                transform.position = _attackStartPos + dir * (lerpT * 0.5f);
            }
            else
            {
                // 后半段：返回
                float lerpT = (t - 0.5f) * 2f;
                Vector3 targetPos = _attackTarget.transform.position;
                Vector3 dir = (targetPos - _attackStartPos).normalized;
                transform.position = _attackStartPos + dir * ((1f - lerpT) * 0.5f);
            }
        }

        if (_attackTimer >= ATTACK_DURATION)
        {
            // 动画结束，执行伤害
            transform.position = _attackStartPos;

            if (_attackTarget != null && _attackTarget.IsAlive)
            {
                int damage = Mathf.Max(1, attack - _attackTarget.defense);
                _attackTarget.TakeDamage(damage);

                Debug.Log($"[Combat] {unitName} attacks {_attackTarget.unitName} for {damage} damage " +
                          $"(HP: {_attackTarget.currentHP}/{_attackTarget.maxHP})");
            }

            State = UnitState.Done;
            ApplyDoneVisual();
            _attackTarget = null;
            _onAttackComplete?.Invoke();
            _onAttackComplete = null;
        }
    }

    // ============ Damage ============

    /// <summary>
    /// 受到伤害
    /// </summary>
    public void TakeDamage(int amount)
    {
        currentHP = Mathf.Max(0, currentHP - amount);

        if (currentHP <= 0)
            Die();
    }

    private void Die()
    {
        Debug.Log($"[Combat] {unitName} ({Team}) has been defeated!");

        // 从网格移除
        _grid.RemoveUnit(CellPosition);

        // 触发事件
        OnDeath?.Invoke(this);

        // 简单的死亡效果：缩小后销毁
        State = UnitState.Done;
        Destroy(gameObject, 0.5f);

        // 立即缩小表示死亡
        transform.localScale *= 0.3f;
        if (_bodyRenderer != null)
            _bodyRenderer.material.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
    }

    // ============ Turn Management ============

    /// <summary>
    /// 新回合重置（可以再次行动）
    /// </summary>
    public void ResetForNewTurn()
    {
        if (!IsAlive) return;

        HasMoved = false;
        HasAttacked = false;
        State = UnitState.Idle;

        // 恢复原始颜色
        if (_bodyRenderer != null)
            _bodyRenderer.material.color = _originalColor;
    }

    /// <summary>
    /// 标记本回合行动结束
    /// </summary>
    public void MarkDone()
    {
        State = UnitState.Done;
        ApplyDoneVisual();
    }

    /// <summary>
    /// 设置选中状态
    /// </summary>
    public void SetSelected(bool selected)
    {
        if (!IsAlive) return;

        if (selected)
        {
            State = UnitState.Selected;
            // 选中高亮：稍微变亮
            if (_bodyRenderer != null)
                _bodyRenderer.material.color = _originalColor * 1.3f;
        }
        else
        {
            if (State == UnitState.Selected)
                State = UnitState.Idle;
            if (_bodyRenderer != null && State != UnitState.Done)
                _bodyRenderer.material.color = _originalColor;
        }
    }

    private void ApplyDoneVisual()
    {
        if (_bodyRenderer != null)
            _bodyRenderer.material.color = _doneColor;
    }

    // ============ Queries ============

    /// <summary>
    /// 本回合是否还能行动（未移动且未完成）
    /// </summary>
    public bool CanAct => IsAlive && State != UnitState.Done
                          && State != UnitState.Moving && State != UnitState.Attacking;
}
