using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 战斗UI - 显示阶段文字、回合数、结束回合按钮、单位信息、战斗结果
/// 需要在 Canvas 上手动搭建并拖拽引用
/// </summary>
public class BattleUI : MonoBehaviour
{
    public static BattleUI Instance { get; private set; }

    // ============ UI References ============

    [Header("Phase Display")]
    [Tooltip("阶段文字（如 'Player Phase'）")]
    public TextMeshProUGUI phaseText;

    [Tooltip("回合数文字（如 'Turn 3'）")]
    public TextMeshProUGUI turnText;

    [Header("Actions")]
    [Tooltip("结束回合按钮")]
    public Button endTurnButton;

    [Header("Unit Info Panel")]
    [Tooltip("单位信息面板（整体 GO，隐藏/显示）")]
    public GameObject unitInfoPanel;

    [Tooltip("单位名称")]
    public TextMeshProUGUI unitNameText;

    [Tooltip("单位HP滑动条")]
    public Slider unitHPSlider;

    [Tooltip("HP 数值文字 (如 '45/50')")]
    public TextMeshProUGUI unitHPText;

    [Tooltip("攻击力文字")]
    public TextMeshProUGUI unitATKText;

    [Tooltip("防御力文字")]
    public TextMeshProUGUI unitDEFText;

    [Header("Result Panel")]
    [Tooltip("战斗结果面板（整体 GO）")]
    public GameObject resultPanel;

    [Tooltip("结果文字（Victory! / Defeat）")]
    public TextMeshProUGUI resultText;

    [Tooltip("返回按钮")]
    public Button returnButton;

    [Header("Reward Display")]
    [Tooltip("奖励列表父物体（在 ResultPanel 内部）")]
    public Transform rewardListParent;

    [Tooltip("奖励行预制体（Icon + NameText + CountText）")]
    public GameObject rewardItemPrefab;

    [Tooltip("金币奖励文字")]
    public TextMeshProUGUI moneyRewardText;

    // ============ Runtime ============

    private readonly List<GameObject> _spawnedRewardRows = new();

    // ============ Lifecycle ============

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // 绑定按钮
        if (endTurnButton != null)
            endTurnButton.onClick.AddListener(OnEndTurnClicked);

        if (returnButton != null)
            returnButton.onClick.AddListener(OnReturnClicked);

        // 初始隐藏
        if (unitInfoPanel != null)
            unitInfoPanel.SetActive(false);
        if (resultPanel != null)
            resultPanel.SetActive(false);

        // 订阅事件
        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.OnPhaseChanged += HandlePhaseChanged;
            BattleManager.Instance.OnBattleEnded += HandleBattleEnded;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
            BattleManager.Instance.OnBattleEnded -= HandleBattleEnded;
        }
    }

    // ============ Phase Display ============

    private void HandlePhaseChanged(BattlePhase phase)
    {
        // 阶段文字
        if (phaseText != null)
        {
            phaseText.text = phase switch
            {
                BattlePhase.PlayerPhase => "<color=#4488FF>Player Phase</color>",
                BattlePhase.EnemyPhase  => "<color=#FF4444>Enemy Phase</color>",
                BattlePhase.Victory     => "<color=#44FF44>Victory!</color>",
                BattlePhase.Defeat      => "<color=#FF4444>Defeat</color>",
                _ => ""
            };
        }

        // 回合数
        if (turnText != null && BattleManager.Instance != null)
        {
            turnText.text = $"Turn {BattleManager.Instance.TurnNumber}";
        }

        // End Turn 按钮只在玩家阶段可用
        if (endTurnButton != null)
        {
            endTurnButton.gameObject.SetActive(phase == BattlePhase.PlayerPhase);
        }
    }

    // ============ Unit Info ============

    /// <summary>
    /// 显示选中单位的信息面板
    /// </summary>
    public void ShowUnitInfo(TacticalUnit unit)
    {
        if (unitInfoPanel == null || unit == null) return;

        unitInfoPanel.SetActive(true);

        if (unitNameText != null)
        {
            string teamColor = unit.Team == CombatTeam.Player ? "#4488FF" : "#FF4444";
            unitNameText.text = $"<color={teamColor}>{unit.unitName}</color>";
        }

        if (unitHPSlider != null)
        {
            unitHPSlider.minValue = 0;
            unitHPSlider.maxValue = unit.maxHP;
            unitHPSlider.value = unit.currentHP;
        }

        if (unitHPText != null)
            unitHPText.text = $"HP: {unit.currentHP}/{unit.maxHP}";

        if (unitATKText != null)
            unitATKText.text = $"ATK: {unit.attack}";

        if (unitDEFText != null)
            unitDEFText.text = $"DEF: {unit.defense}";
    }

    /// <summary>
    /// 隐藏单位信息面板
    /// </summary>
    public void HideUnitInfo()
    {
        if (unitInfoPanel != null)
            unitInfoPanel.SetActive(false);
    }

    // ============ Battle Result ============

    private void HandleBattleEnded(bool victory)
    {
        if (resultPanel == null) return;

        resultPanel.SetActive(true);

        if (resultText != null)
        {
            if (victory)
            {
                resultText.text = "<color=#44FF44>Victory!</color>\n\nAll enemies defeated.";
            }
            else
            {
                resultText.text = "<color=#FF4444>Defeat</color>\n\nAll units have fallen.";
            }
        }

        // 隐藏 End Turn 按钮
        if (endTurnButton != null)
            endTurnButton.gameObject.SetActive(false);

        // 显示战斗奖励
        if (victory)
            DisplayRewards();
    }

    // ============ Reward Display ============

    /// <summary>
    /// 显示战斗奖励（从 BattleRewardDistributor 读取）
    /// </summary>
    private void DisplayRewards()
    {
        ClearRewardRows();

        var distributor = FindObjectOfType<BattleRewardDistributor>();
        if (distributor == null || distributor.LastResult == null) return;

        var result = distributor.LastResult;

        // 金币奖励
        if (moneyRewardText != null)
        {
            if (result.moneyEarned > 0)
            {
                moneyRewardText.text = $"<color=#FFD700>+${result.moneyEarned:N0}</color>";
                moneyRewardText.gameObject.SetActive(true);
            }
            else
            {
                moneyRewardText.gameObject.SetActive(false);
            }
        }

        // 道具奖励列表
        if (rewardItemPrefab != null && rewardListParent != null)
        {
            foreach (var stack in result.itemsObtained)
            {
                var row = Instantiate(rewardItemPrefab, rewardListParent);
                _spawnedRewardRows.Add(row);

                var inv = PlayerInventoryManager.Instance;
                var itemDef = inv != null ? inv.GetItemDef(stack.itemId) : null;

                var nameT = row.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
                var countT = row.transform.Find("CountText")?.GetComponent<TextMeshProUGUI>();
                var iconImg = row.transform.Find("Icon")?.GetComponent<Image>();

                if (nameT != null)
                {
                    nameT.text = itemDef != null ? itemDef.displayName : stack.itemId;
                    if (itemDef != null) nameT.color = itemDef.GetRarityColor();
                }
                if (countT != null)
                    countT.text = $"x{stack.count}";
                if (iconImg != null && itemDef != null && itemDef.icon != null)
                    iconImg.sprite = itemDef.icon;
            }
        }

        if (result.itemsObtained.Count == 0 && moneyRewardText == null)
        {
            Debug.Log("[BattleUI] No rewards to display");
        }
    }

    private void ClearRewardRows()
    {
        foreach (var go in _spawnedRewardRows)
            if (go != null) Destroy(go);
        _spawnedRewardRows.Clear();
    }

    // ============ Button Handlers ============

    private void OnEndTurnClicked()
    {
        if (BattleManager.Instance != null)
            BattleManager.Instance.EndPlayerTurn();
    }

    private void OnReturnClicked()
    {
        // 当前先返回世界地图场景（如果有 SceneTransitionManager）
        // 否则 reload 当前场景用于测试
        var stm = FindObjectOfType<SceneTransitionManager>();
        if (stm != null)
        {
            stm.ReturnToWorldMap();
        }
        else
        {
            // 独立测试模式：重新加载场景
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
    }
}
