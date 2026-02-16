using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 世界地图工具栏 — 控制建造基地、道路选择、道路建造等模式
///
/// 模式定义：
///   None       — 默认浏览模式（点击基地可进入）
///   BuildBase  — 建造基地模式（左键点击地图放置基地）
///   RoadSelect — 道路选择模式（点选/框选已有道路，查看/修复/拆除）
///   RoadBuild  — 道路建造模式（选起点→终点建造新道路）
///
/// 按钮布局建议：
///   [Build Base] [Road Select] [Road Build] [Exit Mode]
///
/// 快捷键：
///   1 = BuildBase, 2 = RoadSelect, 3 = RoadBuild, Esc = ExitMode
/// </summary>
public class WorldMapToolbar : MonoBehaviour
{
    public static WorldMapToolbar Instance { get; private set; }

    // ============ Mode Enum ============

    public enum ToolMode
    {
        None,        // 浏览模式
        BuildBase,   // 建造基地
        RoadSelect,  // 道路选择（点选/框选）
        RoadBuild    // 道路建造
    }

    // ============ UI References ============

    [Header("Buttons")]
    public Button buildBaseButton;
    public Button roadSelectButton;
    public Button roadBuildButton;
    public Button exitModeButton;

    [Header("Status Display")]
    [Tooltip("当前模式文字显示")]
    public TextMeshProUGUI modeStatusText;

    [Header("Key Bindings")]
    public KeyCode buildBaseKey = KeyCode.Alpha1;
    public KeyCode roadSelectKey = KeyCode.Alpha2;
    public KeyCode roadBuildKey = KeyCode.Alpha3;
    public KeyCode exitKey = KeyCode.Escape;

    [Header("Visual")]
    [Tooltip("选中按钮的颜色")]
    public Color activeButtonColor = new Color(0.3f, 0.8f, 0.3f, 1f);
    [Tooltip("未选中按钮的颜色")]
    public Color normalButtonColor = Color.white;

    // ============ References (auto-found) ============

    private WorldMapTester _baseTester;
    private RoadBuilder _roadBuilder;
    private RoadSelector _roadSelector;
    private WorldMapBuildModeController _modeController;

    // ============ Runtime ============

    public ToolMode CurrentMode { get; private set; } = ToolMode.None;

    public event System.Action<ToolMode> OnModeChanged;

    // ============ Lifecycle ============

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // 自动查找场景中的系统
        _baseTester = FindObjectOfType<WorldMapTester>();
        _roadBuilder = FindObjectOfType<RoadBuilder>();
        _roadSelector = FindObjectOfType<RoadSelector>();
        _modeController = FindObjectOfType<WorldMapBuildModeController>();

        // 绑定按钮
        if (buildBaseButton != null)
            buildBaseButton.onClick.AddListener(() => ToggleMode(ToolMode.BuildBase));
        if (roadSelectButton != null)
            roadSelectButton.onClick.AddListener(() => ToggleMode(ToolMode.RoadSelect));
        if (roadBuildButton != null)
            roadBuildButton.onClick.AddListener(() => ToggleMode(ToolMode.RoadBuild));
        if (exitModeButton != null)
            exitModeButton.onClick.AddListener(() => SetMode(ToolMode.None));

        // 确保开局是 None 模式（关掉所有自动启动的系统）
        ForceExitAll();
        UpdateUI();
    }

    private void Update()
    {
        // 快捷键
        if (Input.GetKeyDown(buildBaseKey))
            ToggleMode(ToolMode.BuildBase);
        else if (Input.GetKeyDown(roadSelectKey))
            ToggleMode(ToolMode.RoadSelect);
        else if (Input.GetKeyDown(roadBuildKey))
            ToggleMode(ToolMode.RoadBuild);
        else if (Input.GetKeyDown(exitKey) && CurrentMode != ToolMode.None)
            SetMode(ToolMode.None);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ============ Public API ============

    /// <summary>
    /// 切换模式：已在此模式则退出，否则进入
    /// </summary>
    public void ToggleMode(ToolMode mode)
    {
        SetMode(CurrentMode == mode ? ToolMode.None : mode);
    }

    /// <summary>
    /// 设置模式
    /// </summary>
    public void SetMode(ToolMode mode)
    {
        if (mode == CurrentMode) return;

        // 1. 先退出旧模式
        ExitCurrentMode();

        // 2. 进入新模式
        CurrentMode = mode;
        EnterCurrentMode();

        // 3. 更新 UI
        UpdateUI();

        OnModeChanged?.Invoke(CurrentMode);
        Debug.Log($"[WorldMapToolbar] Mode → {CurrentMode}");
    }

    // ============ Mode Enter / Exit ============

    private void EnterCurrentMode()
    {
        switch (CurrentMode)
        {
            case ToolMode.BuildBase:
                if (_baseTester != null) _baseTester.EnterBuildMode();
                // 道路系统全部关闭
                if (_roadBuilder != null) _roadBuilder.SetBuildMode(false);
                if (_roadSelector != null) _roadSelector.isSelectionEnabled = false;
                break;

            case ToolMode.RoadSelect:
                if (_roadSelector != null) _roadSelector.isSelectionEnabled = true;
                // 其他关闭
                if (_baseTester != null) _baseTester.ExitBuildMode();
                if (_roadBuilder != null) _roadBuilder.SetBuildMode(false);
                break;

            case ToolMode.RoadBuild:
                if (_roadBuilder != null) _roadBuilder.SetBuildMode(true);
                // 其他关闭
                if (_baseTester != null) _baseTester.ExitBuildMode();
                if (_roadSelector != null) _roadSelector.isSelectionEnabled = false;
                break;

            case ToolMode.None:
                // 什么都不启动
                break;
        }
    }

    private void ExitCurrentMode()
    {
        switch (CurrentMode)
        {
            case ToolMode.BuildBase:
                if (_baseTester != null) _baseTester.ExitBuildMode();
                break;

            case ToolMode.RoadSelect:
                if (_roadSelector != null)
                {
                    _roadSelector.ClearSelection();
                    _roadSelector.isSelectionEnabled = false;
                }
                break;

            case ToolMode.RoadBuild:
                if (_roadBuilder != null) _roadBuilder.SetBuildMode(false);
                break;
        }
    }

    /// <summary>
    /// 强制退出所有模式（开局初始化用）
    /// </summary>
    private void ForceExitAll()
    {
        CurrentMode = ToolMode.None;

        if (_baseTester != null) _baseTester.ExitBuildMode();
        if (_roadBuilder != null) _roadBuilder.SetBuildMode(false);
        if (_roadSelector != null) _roadSelector.isSelectionEnabled = false;

        // WorldMapBuildModeController 也退出
        if (_modeController != null) _modeController.ExitAllModes();
    }

    // ============ UI Update ============

    private void UpdateUI()
    {
        // 按钮高亮
        SetButtonColor(buildBaseButton, CurrentMode == ToolMode.BuildBase);
        SetButtonColor(roadSelectButton, CurrentMode == ToolMode.RoadSelect);
        SetButtonColor(roadBuildButton, CurrentMode == ToolMode.RoadBuild);

        // Exit 按钮只在有模式时可见
        if (exitModeButton != null)
            exitModeButton.gameObject.SetActive(CurrentMode != ToolMode.None);

        // 状态文字
        if (modeStatusText != null)
        {
            modeStatusText.text = CurrentMode switch
            {
                ToolMode.BuildBase => "Build Base Mode — Click to place base",
                ToolMode.RoadSelect => "Road Select Mode — Click/drag to select roads",
                ToolMode.RoadBuild => "Road Build Mode — Click start → end to build road",
                _ => ""
            };
            modeStatusText.gameObject.SetActive(CurrentMode != ToolMode.None);
        }
    }

    private void SetButtonColor(Button btn, bool isActive)
    {
        if (btn == null) return;
        var colors = btn.colors;
        colors.normalColor = isActive ? activeButtonColor : normalButtonColor;
        colors.selectedColor = isActive ? activeButtonColor : normalButtonColor;
        btn.colors = colors;
    }
}
