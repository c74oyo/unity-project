using System;
using UnityEngine;

/// <summary>
/// 大地图建造模式控制器 - 协调道路建造和基地建造的互斥
/// 确保同一时间只有一种建造模式处于激活状态
/// </summary>
public class WorldMapBuildModeController : MonoBehaviour
{
    public static WorldMapBuildModeController Instance { get; private set; }

    [Header("References")]
    [Tooltip("道路建造器")]
    public RoadBuilder roadBuilder;

    [Tooltip("基地建造测试器")]
    public WorldMapTester baseTester;

    [Header("Debug")]
    public bool debugLog = false;

    /// <summary>
    /// 建造模式类型
    /// </summary>
    public enum BuildMode
    {
        None,   // 无建造模式
        Road,   // 道路建造
        Base    // 基地建造
    }

    /// <summary>
    /// 当前建造模式
    /// </summary>
    public BuildMode CurrentMode { get; private set; } = BuildMode.None;

    /// <summary>
    /// 建造模式变化事件
    /// </summary>
    public event Action<BuildMode> OnBuildModeChanged;

    // ============ Lifecycle ============

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 自动查找引用
        if (roadBuilder == null)
            roadBuilder = FindObjectOfType<RoadBuilder>();

        if (baseTester == null)
            baseTester = FindObjectOfType<WorldMapTester>();
    }

    private void Start()
    {
        // 订阅道路建造器的模式变化事件
        if (roadBuilder != null)
        {
            roadBuilder.OnBuildModeChanged += OnRoadBuildModeChanged;
        }

        // 注意：WorldMapTester 没有事件，我们需要在 Update 中监控其状态
        // 或者修改 WorldMapTester 添加事件（这里选择监控方式，避免修改过多文件）
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (roadBuilder != null)
        {
            roadBuilder.OnBuildModeChanged -= OnRoadBuildModeChanged;
        }
    }

    private bool _lastBaseBuildMode = false;

    private void Update()
    {
        // 监控基地建造模式变化（因为 WorldMapTester 没有事件）
        if (baseTester != null)
        {
            bool currentBaseBuildMode = baseTester.isInBuildMode;
            if (currentBaseBuildMode != _lastBaseBuildMode)
            {
                _lastBaseBuildMode = currentBaseBuildMode;
                OnBaseBuildModeChanged(currentBaseBuildMode);
            }
        }
    }

    // ============ Event Handlers ============

    /// <summary>
    /// 道路建造模式变化回调
    /// </summary>
    private void OnRoadBuildModeChanged(bool isEnabled)
    {
        if (isEnabled)
        {
            // 道路建造激活 → 关闭基地建造
            if (baseTester != null && baseTester.isInBuildMode)
            {
                if (debugLog) Debug.Log("[BuildModeController] Road mode ON → Exiting base mode");
                baseTester.ExitBuildMode();
                _lastBaseBuildMode = false;
            }

            SetModeInternal(BuildMode.Road);
        }
        else
        {
            // 道路建造关闭
            if (CurrentMode == BuildMode.Road)
            {
                SetModeInternal(BuildMode.None);
            }
        }
    }

    /// <summary>
    /// 基地建造模式变化回调
    /// </summary>
    private void OnBaseBuildModeChanged(bool isEnabled)
    {
        if (isEnabled)
        {
            // 基地建造激活 → 关闭道路建造
            if (roadBuilder != null && roadBuilder.isBuildMode)
            {
                if (debugLog) Debug.Log("[BuildModeController] Base mode ON → Exiting road mode");
                roadBuilder.SetBuildMode(false);
            }

            SetModeInternal(BuildMode.Base);
        }
        else
        {
            // 基地建造关闭
            if (CurrentMode == BuildMode.Base)
            {
                SetModeInternal(BuildMode.None);
            }
        }
    }

    // ============ Public Methods ============

    /// <summary>
    /// 设置建造模式
    /// </summary>
    public void SetMode(BuildMode mode)
    {
        if (mode == CurrentMode) return;

        switch (mode)
        {
            case BuildMode.None:
                ExitAllModes();
                break;

            case BuildMode.Road:
                // 先退出其他模式
                if (baseTester != null && baseTester.isInBuildMode)
                {
                    baseTester.ExitBuildMode();
                    _lastBaseBuildMode = false;
                }
                // 进入道路建造
                if (roadBuilder != null)
                {
                    roadBuilder.SetBuildMode(true);
                }
                break;

            case BuildMode.Base:
                // 先退出其他模式
                if (roadBuilder != null && roadBuilder.isBuildMode)
                {
                    roadBuilder.SetBuildMode(false);
                }
                // 进入基地建造
                if (baseTester != null)
                {
                    baseTester.EnterBuildMode();
                    _lastBaseBuildMode = true;
                }
                break;
        }
    }

    /// <summary>
    /// 退出所有建造模式
    /// </summary>
    public void ExitAllModes()
    {
        if (roadBuilder != null && roadBuilder.isBuildMode)
        {
            roadBuilder.SetBuildMode(false);
        }

        if (baseTester != null && baseTester.isInBuildMode)
        {
            baseTester.ExitBuildMode();
            _lastBaseBuildMode = false;
        }

        SetModeInternal(BuildMode.None);
    }

    /// <summary>
    /// 切换到下一个建造模式（None → Road → Base → None）
    /// </summary>
    public void CycleBuildMode()
    {
        switch (CurrentMode)
        {
            case BuildMode.None:
                SetMode(BuildMode.Road);
                break;
            case BuildMode.Road:
                SetMode(BuildMode.Base);
                break;
            case BuildMode.Base:
                SetMode(BuildMode.None);
                break;
        }
    }

    /// <summary>
    /// 是否处于任何建造模式
    /// </summary>
    public bool IsInAnyBuildMode => CurrentMode != BuildMode.None;

    /// <summary>
    /// 是否处于道路建造模式
    /// </summary>
    public bool IsInRoadBuildMode => CurrentMode == BuildMode.Road;

    /// <summary>
    /// 是否处于基地建造模式
    /// </summary>
    public bool IsInBaseBuildMode => CurrentMode == BuildMode.Base;

    // ============ Internal ============

    private void SetModeInternal(BuildMode mode)
    {
        if (mode == CurrentMode) return;

        var oldMode = CurrentMode;
        CurrentMode = mode;

        if (debugLog)
            Debug.Log($"[BuildModeController] Mode changed: {oldMode} → {mode}");

        OnBuildModeChanged?.Invoke(mode);
    }

    // ============ Debug UI ============

#if UNITY_EDITOR
    [Header("Editor Debug")]
    public bool showDebugGUI = false;

    private void OnGUI()
    {
        if (!showDebugGUI) return;

        GUILayout.BeginArea(new Rect(Screen.width - 220, 10, 210, 100));
        GUILayout.BeginVertical("box");

        GUILayout.Label("Build Mode Controller");

        string modeColor = CurrentMode switch
        {
            BuildMode.Road => "cyan",
            BuildMode.Base => "yellow",
            _ => "white"
        };
        GUILayout.Label($"Current: <color={modeColor}>{CurrentMode}</color>",
            new GUIStyle(GUI.skin.label) { richText = true });

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Road")) SetMode(BuildMode.Road);
        if (GUILayout.Button("Base")) SetMode(BuildMode.Base);
        if (GUILayout.Button("Exit")) ExitAllModes();
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
#endif
}
