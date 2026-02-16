using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 道路建造UI - 显示道路建造相关的界面元素
/// </summary>
public class RoadBuilderUI : MonoBehaviour
{
    [Header("References")]
    public RoadBuilder roadBuilder;
    public RoadNetwork roadNetwork;

    [Header("UI Elements")]
    public GameObject buildPanel;               // 主面板
    public TextMeshProUGUI modeText;            // 当前模式文字
    public TextMeshProUGUI selectedTypeText;    // 当前选择的道路类型
    public TextMeshProUGUI costText;            // 成本预览
    public TextMeshProUGUI pathLengthText;      // 路径长度

    [Header("Road Type Selection")]
    public Transform roadTypeButtonContainer;   // 道路类型按钮容器
    public GameObject roadTypeButtonPrefab;     // 道路类型按钮预制体

    [Header("Tips")]
    public TextMeshProUGUI tipsText;            // 操作提示

    // Runtime
    private List<GameObject> _roadTypeButtons = new();

    // ============ Lifecycle ============

    private void Awake()
    {
        if (roadBuilder == null)
            roadBuilder = FindObjectOfType<RoadBuilder>();

        if (roadNetwork == null)
            roadNetwork = FindObjectOfType<RoadNetwork>();
    }

    private void Start()
    {
        // 创建道路类型选择按钮
        CreateRoadTypeButtons();

        // 初始化显示
        UpdateUI();

        // 订阅事件
        if (roadBuilder != null)
        {
            roadBuilder.OnBuildModeChanged += OnBuildModeChanged;
            roadBuilder.OnPathPreviewUpdated += OnPathPreviewUpdated;
            roadBuilder.OnRoadBuilt += OnRoadBuilt;
        }

        // 初始隐藏面板
        if (buildPanel != null)
            buildPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (roadBuilder != null)
        {
            roadBuilder.OnBuildModeChanged -= OnBuildModeChanged;
            roadBuilder.OnPathPreviewUpdated -= OnPathPreviewUpdated;
            roadBuilder.OnRoadBuilt -= OnRoadBuilt;
        }
    }

    private void Update()
    {
        // 按键切换面板显示
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleBuildPanel();
        }
    }

    // ============ UI Creation ============

    private void CreateRoadTypeButtons()
    {
        if (roadTypeButtonContainer == null || roadTypeButtonPrefab == null) return;
        if (roadNetwork == null) return;

        // 清除旧按钮
        foreach (var btn in _roadTypeButtons)
        {
            if (btn != null) Destroy(btn);
        }
        _roadTypeButtons.Clear();

        // 为每种道路类型创建按钮
        foreach (var roadType in roadNetwork.roadTypes)
        {
            if (roadType == null) continue;

            var btnGO = Instantiate(roadTypeButtonPrefab, roadTypeButtonContainer);
            _roadTypeButtons.Add(btnGO);

            // 设置按钮文字
            var text = btnGO.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = $"{roadType.displayName}\n${roadType.moneyCost}";
            }

            // 设置按钮点击事件
            var button = btnGO.GetComponent<Button>();
            if (button != null)
            {
                string typeId = roadType.roadTypeId; // 闭包捕获
                button.onClick.AddListener(() => SelectRoadType(typeId));
            }

            // 设置按钮颜色
            var image = btnGO.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(roadType.roadColor.r, roadType.roadColor.g, roadType.roadColor.b, 0.8f);
            }
        }
    }

    // ============ Event Handlers ============

    private void OnBuildModeChanged(bool isEnabled)
    {
        UpdateUI();

        // 显示/隐藏面板
        if (buildPanel != null)
        {
            buildPanel.SetActive(isEnabled);
        }
    }

    private void OnPathPreviewUpdated(List<Vector2Int> path, float cost)
    {
        // 更新成本显示
        if (costText != null)
        {
            if (path != null && path.Count > 0)
            {
                costText.text = $"Cost: ${cost:F0}";
                costText.color = cost > 0 ? Color.yellow : Color.white;
            }
            else
            {
                costText.text = "Cost: -";
            }
        }

        // 更新路径长度
        if (pathLengthText != null)
        {
            if (path != null && path.Count > 0)
            {
                pathLengthText.text = $"Length: {path.Count}";
            }
            else
            {
                pathLengthText.text = "Length: -";
            }
        }
    }

    private void OnRoadBuilt(List<Vector2Int> cells)
    {
        // 播放建造成功音效或特效
        Debug.Log($"[RoadBuilderUI] Road built: {cells.Count} cells");

        // 清除成本显示
        if (costText != null)
            costText.text = "Cost: -";

        if (pathLengthText != null)
            pathLengthText.text = "Length: -";
    }

    // ============ UI Actions ============

    public void ToggleBuildPanel()
    {
        if (buildPanel != null)
        {
            bool newState = !buildPanel.activeSelf;
            buildPanel.SetActive(newState);

            // 同时切换建造模式
            if (roadBuilder != null)
            {
                roadBuilder.SetBuildMode(newState);
            }
        }
    }

    public void SelectRoadType(string roadTypeId)
    {
        if (roadNetwork == null || roadBuilder == null) return;

        var roadType = roadNetwork.GetRoadType(roadTypeId);
        if (roadType == null) return;

        roadBuilder.SetSelectedRoadType(roadType);
        UpdateUI();
    }

    public void ToggleBuildMode()
    {
        if (roadBuilder != null)
        {
            roadBuilder.ToggleBuildMode();
        }
    }

    // ============ UI Update ============

    private void UpdateUI()
    {
        // 更新模式文字
        if (modeText != null && roadBuilder != null)
        {
            modeText.text = roadBuilder.isBuildMode ? "Build Mode: ON" : "Build Mode: OFF";
            modeText.color = roadBuilder.isBuildMode ? Color.green : Color.gray;
        }

        // 更新选择的道路类型
        if (selectedTypeText != null && roadBuilder != null)
        {
            if (roadBuilder.selectedRoadType != null)
            {
                selectedTypeText.text = $"Selected: {roadBuilder.selectedRoadType.displayName}";
            }
            else
            {
                selectedTypeText.text = "Selected: None";
            }
        }

        // 更新操作提示
        UpdateTips();

        // 更新按钮选中状态
        UpdateButtonSelection();
    }

    private void UpdateTips()
    {
        if (tipsText == null || roadBuilder == null) return;

        if (!roadBuilder.isBuildMode)
        {
            tipsText.text = "Press R to enter build mode";
        }
        else if (roadBuilder.selectedRoadType == null)
        {
            tipsText.text = "Select a road type";
        }
        else if (!roadBuilder.HasValidPreview)
        {
            tipsText.text = "Left-click to set start point\nRight-click to cancel";
        }
        else
        {
            tipsText.text = "Left-click to confirm build\nRight-click to cancel\nESC to exit";
        }
    }

    private void UpdateButtonSelection()
    {
        if (roadBuilder == null || roadNetwork == null) return;

        string selectedId = roadBuilder.selectedRoadType?.roadTypeId;

        for (int i = 0; i < _roadTypeButtons.Count && i < roadNetwork.roadTypes.Count; i++)
        {
            var btn = _roadTypeButtons[i];
            var roadType = roadNetwork.roadTypes[i];

            if (btn == null || roadType == null) continue;

            var image = btn.GetComponent<Image>();
            if (image != null)
            {
                bool isSelected = roadType.roadTypeId == selectedId;
                // 选中的按钮更亮
                image.color = new Color(
                    roadType.roadColor.r,
                    roadType.roadColor.g,
                    roadType.roadColor.b,
                    isSelected ? 1f : 0.6f
                );
            }

            // 也可以给按钮加边框等
            var outline = btn.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = roadType.roadTypeId == selectedId;
            }
        }
    }

    // ============ Debug ============
#if UNITY_EDITOR
    [ContextMenu("Debug: Refresh UI")]
    private void DebugRefreshUI()
    {
        CreateRoadTypeButtons();
        UpdateUI();
    }
#endif
}
