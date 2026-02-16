using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 道路管理UI - 显示选中道路的信息，提供升级/维护操作
/// </summary>
public class RoadManagementUI : MonoBehaviour
{
    [Header("References")]
    public RoadSelector roadSelector;
    public RoadNetwork roadNetwork;

    [Header("Main Panel")]
    [Tooltip("主面板（当有道路选中时显示）")]
    public GameObject mainPanel;

    [Header("Selection Info")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI selectionInfoText;
    public Transform roadTypeListParent;
    public GameObject roadTypeItemPrefab;

    [Header("Durability Display")]
    public Slider durabilitySlider;
    public TextMeshProUGUI durabilityText;
    public Image durabilityFillImage;

    [Header("Action Buttons")]
    public Button repairButton;
    public TextMeshProUGUI repairCostText;
    public Button upgradeButton;
    public Button deleteButton;

    [Header("Upgrade Panel")]
    public GameObject upgradePanel;
    public TMP_Dropdown upgradeTargetDropdown;
    public TextMeshProUGUI upgradeCostText;
    public Button confirmUpgradeButton;
    public Button cancelUpgradeButton;

    [Header("Colors")]
    public Color durabilityNormalColor = Color.green;
    public Color durabilityLightDamageColor = Color.yellow;
    public Color durabilitySevereDamageColor = new Color(1f, 0.5f, 0f);
    public Color durabilityBrokenColor = Color.red;

    // 缓存
    private List<GameObject> _roadTypeItems = new();
    private RoadSelectionInfo _currentInfo;
    private RoadType _selectedUpgradeTarget;

    // ============ Lifecycle ============

    private void Awake()
    {
        if (roadSelector == null)
            roadSelector = FindObjectOfType<RoadSelector>();
        if (roadNetwork == null)
            roadNetwork = FindObjectOfType<RoadNetwork>();
    }

    private void Start()
    {
        // 订阅选择变化事件
        if (roadSelector != null)
        {
            roadSelector.OnSelectionChanged += OnSelectionChanged;
        }

        // 绑定按钮事件
        if (repairButton != null)
            repairButton.onClick.AddListener(OnRepairClicked);
        if (upgradeButton != null)
            upgradeButton.onClick.AddListener(OnUpgradeClicked);
        if (deleteButton != null)
            deleteButton.onClick.AddListener(OnDeleteClicked);
        if (confirmUpgradeButton != null)
            confirmUpgradeButton.onClick.AddListener(OnConfirmUpgrade);
        if (cancelUpgradeButton != null)
            cancelUpgradeButton.onClick.AddListener(OnCancelUpgrade);

        // 初始隐藏面板
        if (mainPanel != null) mainPanel.SetActive(false);
        if (upgradePanel != null) upgradePanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (roadSelector != null)
        {
            roadSelector.OnSelectionChanged -= OnSelectionChanged;
        }
    }

    // ============ Event Handlers ============

    private void OnSelectionChanged(HashSet<Vector2Int> selectedCells)
    {
        if (selectedCells.Count == 0)
        {
            HidePanel();
            return;
        }

        ShowPanel();
        UpdateDisplay();
    }

    // ============ Display ============

    private void ShowPanel()
    {
        if (mainPanel != null) mainPanel.SetActive(true);
    }

    private void HidePanel()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (upgradePanel != null) upgradePanel.SetActive(false);
    }

    private void UpdateDisplay()
    {
        if (roadSelector == null || roadNetwork == null) return;

        _currentInfo = roadSelector.GetSelectionInfo();

        // 更新标题
        if (titleText != null)
        {
            titleText.text = $"已选择 {_currentInfo.totalCells} 段道路";
        }

        // 更新详细信息
        UpdateSelectionInfoText();

        // 更新道路类型列表
        UpdateRoadTypeList();

        // 更新耐久度显示
        UpdateDurabilityDisplay();

        // 更新按钮状态
        UpdateButtonStates();
    }

    private void UpdateSelectionInfoText()
    {
        if (selectionInfoText == null || _currentInfo == null) return;

        StringBuilder sb = new StringBuilder();

        // 损坏状态
        if (_currentInfo.HasBroken)
        {
            sb.AppendLine($"<color=red>损坏: {_currentInfo.brokenCount} 段不可通行</color>");
        }
        else if (_currentInfo.averageDurability < 50f)
        {
            sb.AppendLine("<color=orange>部分道路需要维护</color>");
        }

        selectionInfoText.text = sb.ToString();
    }

    private void UpdateRoadTypeList()
    {
        if (roadTypeListParent == null || roadTypeItemPrefab == null) return;

        // 清除旧项
        foreach (var item in _roadTypeItems)
        {
            Destroy(item);
        }
        _roadTypeItems.Clear();

        if (_currentInfo == null || roadNetwork == null) return;

        // 创建新项
        foreach (var kvp in _currentInfo.roadTypeCounts)
        {
            var roadType = roadNetwork.GetRoadType(kvp.Key);
            if (roadType == null) continue;

            var item = Instantiate(roadTypeItemPrefab, roadTypeListParent);
            _roadTypeItems.Add(item);

            // 设置文本
            var text = item.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = $"{roadType.displayName}: {kvp.Value}";
            }

            // 设置颜色指示
            var image = item.GetComponent<Image>();
            if (image != null)
            {
                image.color = roadType.roadColor;
            }
        }
    }

    private void UpdateDurabilityDisplay()
    {
        if (_currentInfo == null) return;

        float durability = _currentInfo.averageDurability;

        // 更新滑块
        if (durabilitySlider != null)
        {
            durabilitySlider.value = durability / 100f;
        }

        // 更新文本
        if (durabilityText != null)
        {
            durabilityText.text = $"{durability:F0}%";
        }

        // 更新颜色
        if (durabilityFillImage != null)
        {
            Color color;
            if (durability <= 0f)
                color = durabilityBrokenColor;
            else if (durability <= 25f)
                color = durabilitySevereDamageColor;
            else if (durability <= 50f)
                color = durabilityLightDamageColor;
            else
                color = durabilityNormalColor;

            durabilityFillImage.color = color;
        }

        // 更新修复成本
        if (repairCostText != null)
        {
            if (_currentInfo.totalRepairCost > 0f)
            {
                repairCostText.text = $"修复: ${_currentInfo.totalRepairCost:F0}";
            }
            else
            {
                repairCostText.text = "无需修复";
            }
        }
    }

    private void UpdateButtonStates()
    {
        if (_currentInfo == null) return;

        // 修复按钮：有损坏且有成本才可用
        if (repairButton != null)
        {
            repairButton.interactable = _currentInfo.totalRepairCost > 0f;
        }

        // 升级按钮：检查是否可升级
        if (upgradeButton != null)
        {
            bool canUpgrade = CanUpgradeSelection();
            upgradeButton.interactable = canUpgrade;
        }

        // 删除按钮：始终可用（如果有选择）
        if (deleteButton != null)
        {
            deleteButton.interactable = _currentInfo.totalCells > 0;
        }
    }

    private bool CanUpgradeSelection()
    {
        if (roadSelector == null || roadNetwork == null) return false;

        var selectedCells = roadSelector.SelectedCells;
        foreach (var cell in selectedCells)
        {
            if (roadNetwork.CanUpgradeRoad(cell))
                return true;
        }
        return false;
    }

    // ============ Button Callbacks ============

    public void OnRepairClicked()
    {
        if (roadSelector == null || roadNetwork == null || _currentInfo == null) return;

        // 检查金钱是否足够
        if (BaseManager.Instance != null)
        {
            var baseSave = BaseManager.Instance.GetActiveBaseSaveData();
            if (baseSave == null)
            {
                var allBases = BaseManager.Instance.AllBaseSaveData;
                if (allBases.Count > 0) baseSave = allBases[0];
            }

            if (baseSave != null)
            {
                if (baseSave.money < _currentInfo.totalRepairCost)
                {
                    Debug.LogWarning($"[RoadManagementUI] Not enough money! Need ${_currentInfo.totalRepairCost:F0}, have ${baseSave.money:F0}");
                    return;
                }

                // 扣除金钱
                baseSave.money -= _currentInfo.totalRepairCost;
                BaseManager.Instance.UpdateBaseSaveData(baseSave);
            }
        }

        // 修复所有选中的道路
        var selectedCells = roadSelector.SelectedCells;
        int repaired = 0;
        foreach (var cell in selectedCells)
        {
            if (roadNetwork.TryRepairRoad(cell, 0f)) // 0表示完全修复
                repaired++;
        }

        Debug.Log($"[RoadManagementUI] Repaired {repaired} road segments, cost ${_currentInfo.totalRepairCost:F0}");

        // 刷新显示
        UpdateDisplay();
    }

    public void OnUpgradeClicked()
    {
        if (upgradePanel == null) return;

        // 显示升级面板
        upgradePanel.SetActive(true);

        // 填充升级目标下拉框
        PopulateUpgradeDropdown();
    }

    private void PopulateUpgradeDropdown()
    {
        if (upgradeTargetDropdown == null || roadNetwork == null) return;

        upgradeTargetDropdown.ClearOptions();

        // 获取所有可升级到的道路类型
        var options = new List<TMP_Dropdown.OptionData>();
        var upgradeTargets = new List<RoadType>();

        foreach (var roadType in roadNetwork.roadTypes)
        {
            if (roadType == null) continue;

            // 检查是否有选中的道路可以升级到这个类型
            if (IsValidUpgradeTarget(roadType))
            {
                options.Add(new TMP_Dropdown.OptionData(roadType.displayName));
                upgradeTargets.Add(roadType);
            }
        }

        if (options.Count > 0)
        {
            upgradeTargetDropdown.AddOptions(options);
            upgradeTargetDropdown.value = 0;
            _selectedUpgradeTarget = upgradeTargets[0];

            // 监听下拉框变化
            upgradeTargetDropdown.onValueChanged.RemoveAllListeners();
            upgradeTargetDropdown.onValueChanged.AddListener((index) =>
            {
                if (index >= 0 && index < upgradeTargets.Count)
                {
                    _selectedUpgradeTarget = upgradeTargets[index];
                    UpdateUpgradeCost();
                }
            });

            UpdateUpgradeCost();
        }
    }

    private bool IsValidUpgradeTarget(RoadType targetType)
    {
        if (roadSelector == null || roadNetwork == null) return false;

        var selectedCells = roadSelector.SelectedCells;
        foreach (var cell in selectedCells)
        {
            var segment = roadNetwork.GetRoadAt(cell);
            if (segment == null) continue;

            var currentType = roadNetwork.GetRoadType(segment.roadTypeId);
            if (currentType == null) continue;

            // 目标等级必须高于当前等级
            if (targetType.level > currentType.level)
                return true;
        }
        return false;
    }

    private void UpdateUpgradeCost()
    {
        if (upgradeCostText == null || _selectedUpgradeTarget == null) return;

        float totalCost = CalculateUpgradeCost();
        upgradeCostText.text = $"升级成本: ${totalCost:F0}";
    }

    private float CalculateUpgradeCost()
    {
        if (roadSelector == null || roadNetwork == null || _selectedUpgradeTarget == null)
            return 0f;

        float totalCost = 0f;
        var selectedCells = roadSelector.SelectedCells;

        foreach (var cell in selectedCells)
        {
            var segment = roadNetwork.GetRoadAt(cell);
            if (segment == null) continue;

            var currentType = roadNetwork.GetRoadType(segment.roadTypeId);
            if (currentType == null) continue;

            if (_selectedUpgradeTarget.level > currentType.level)
            {
                // 计算升级成本（目标类型的建造成本 - 当前类型的建造成本）
                totalCost += Mathf.Max(0, _selectedUpgradeTarget.moneyCost - currentType.moneyCost);
            }
        }

        return totalCost;
    }

    public void OnConfirmUpgrade()
    {
        if (roadSelector == null || roadNetwork == null || _selectedUpgradeTarget == null) return;

        float upgradeCost = CalculateUpgradeCost();

        // 检查金钱是否足够
        if (BaseManager.Instance != null)
        {
            var baseSave = BaseManager.Instance.GetActiveBaseSaveData();
            if (baseSave == null)
            {
                var allBases = BaseManager.Instance.AllBaseSaveData;
                if (allBases.Count > 0) baseSave = allBases[0];
            }

            if (baseSave != null)
            {
                if (baseSave.money < upgradeCost)
                {
                    Debug.LogWarning($"[RoadManagementUI] Not enough money for upgrade! Need ${upgradeCost:F0}, have ${baseSave.money:F0}");
                    return;
                }

                // 扣除金钱
                baseSave.money -= upgradeCost;
                BaseManager.Instance.UpdateBaseSaveData(baseSave);
            }
        }

        // 升级所有选中的道路
        var selectedCells = roadSelector.SelectedCells;
        int upgraded = 0;

        foreach (var cell in selectedCells)
        {
            var segment = roadNetwork.GetRoadAt(cell);
            if (segment == null) continue;

            var currentType = roadNetwork.GetRoadType(segment.roadTypeId);
            if (currentType == null) continue;

            if (_selectedUpgradeTarget.level > currentType.level)
            {
                // 直接修改道路类型
                segment.roadTypeId = _selectedUpgradeTarget.roadTypeId;
                // 重置耐久度（升级后道路是新的）
                segment.FullRepair();
                upgraded++;

                // 触发升级事件（通过 RoadNetwork 的方法）
                roadNetwork.NotifyRoadUpgraded(cell, segment);
            }
        }

        Debug.Log($"[RoadManagementUI] Upgraded {upgraded} road segments to {_selectedUpgradeTarget.displayName}, cost ${upgradeCost:F0}");

        // 关闭升级面板
        if (upgradePanel != null) upgradePanel.SetActive(false);

        // 刷新显示
        UpdateDisplay();
    }

    public void OnCancelUpgrade()
    {
        if (upgradePanel != null) upgradePanel.SetActive(false);
    }

    public void OnDeleteClicked()
    {
        if (roadSelector == null || roadNetwork == null) return;

        var selectedCells = new List<Vector2Int>(roadSelector.SelectedCells);
        int deleted = 0;

        foreach (var cell in selectedCells)
        {
            if (roadNetwork.TryRemoveRoad(cell))
                deleted++;
        }

        Debug.Log($"[RoadManagementUI] Deleted {deleted} road segments");

        // 清除选择
        roadSelector.ClearSelection();
    }
}
