using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuildUIController : MonoBehaviour
{
    [Header("Refs")]
    public PlacementManager placement;
    public BuildCatalog catalog;                 // 你的 BuildCatalog（可选：用于自动生成按钮）
    public GlobalInventoryHUD inventoryHud;      // 你已有的库存HUD（可选）

    [Header("TopBar")]
    public TMP_Text modeTMP;

    [Header("Panels")]
    public GameObject buildPalettePanel;         // 建造清单面板（可隐藏/显示）
    public GameObject bottomBarPanel;

    [Header("Panel Behavior")]
    [Tooltip("UI 面板是否常驻显示（不随建造模式切换）")]
    public bool alwaysShowUI = false;

    [Header("BottomBar Buttons (optional)")]
    public Button buildBtn;
    public Button demolishBtn;
    public Button rotateBtn;
    public Button cancelBtn;

    [Header("Build Palette (optional - auto generate)")]
    public bool autoBuildButtons = true;
    public Transform buildButtonContainer;       // ScrollView/Content
    public Button buildButtonPrefab;             // 一个普通 Button，上面带 TMP_Text
    public bool showCostOnButton = true;

    [Header("Button Size (optional)")]
    public bool overrideButtonSize = false;
    public Vector2 buttonSize = new Vector2(100f, 100f);

    // 注意：热力图现在由 PlacementManager 统一控制，这里不再需要引用
    // [Header("Overlay (optional)")]
    // public PowerHeatmapOverlay heatmap;

    private readonly List<Button> _spawnedButtons = new();

    private void Awake()
    {
        if (buildPalettePanel != null)
        {
            // 如果设置为常驻显示，则保持激活状态
            buildPalettePanel.SetActive(alwaysShowUI);
        }
    }

    private void Start()
    {
        WireButtons();

        if (autoBuildButtons)
            RebuildBuildButtons();

        RefreshModeText();
        RefreshButtonInteractivity();
    }

    private void Update()
    {
        // 轻量刷新（你也可以改成事件驱动；先用这个稳定）
        RefreshModeText();
        RefreshButtonInteractivity();
    }

    private void WireButtons()
    {
        if (buildBtn != null) buildBtn.onClick.AddListener(OnClickBuild);
        if (demolishBtn != null) demolishBtn.onClick.AddListener(OnClickDemolish);
        if (rotateBtn != null) rotateBtn.onClick.AddListener(OnClickRotate);
        if (cancelBtn != null) cancelBtn.onClick.AddListener(OnClickCancel);
    }

    // ======================
    // Button callbacks
    // ======================

    public void OnClickBuild()
    {
        if (placement == null) return;

        // 如果正在拆除，先退出拆除
        if (placement.DemolishMode)
        {
            placement.SetMode(PlacementManager.InteractionMode.Normal, clearSelection: true);
        }

        if (placement.BuildMode)
        {
            // 已在建造 -> 退出建造，同时关闭列表
            placement.SetMode(PlacementManager.InteractionMode.Normal, clearSelection: true);
            if (!alwaysShowUI && buildPalettePanel != null)
                buildPalettePanel.SetActive(false);
            return;
        }

        // 不在建造 -> 进入建造，并打开列表
        placement.SetMode(PlacementManager.InteractionMode.Build, clearSelection: false);
        if (buildPalettePanel != null) buildPalettePanel.SetActive(true);
    }

    public void OnClickDemolish()
    {
        if (placement == null) return;

        // 关闭建造列表（如果不是常驻显示）
        if (!alwaysShowUI && buildPalettePanel != null)
            buildPalettePanel.SetActive(false);

        if (placement.DemolishMode)
        {
            // 已在拆除 -> 退出拆除
            placement.SetMode(PlacementManager.InteractionMode.Normal, clearSelection: true);
            return;
        }

        // 进入拆除模式
        placement.SetMode(PlacementManager.InteractionMode.Demolish, clearSelection: true);
    }

    public void OnClickRotate()
    {
        if (placement == null) return;
        placement.UI_Rotate();
    }

    public void OnClickCancel()
    {
        if (placement == null) return;

        // 优先关闭列表（如果不是常驻显示）
        if (!alwaysShowUI && buildPalettePanel != null)
            buildPalettePanel.SetActive(false);

        // 退出当前模式，回到普通模式
        placement.SetMode(PlacementManager.InteractionMode.Normal, clearSelection: true);
    }

    public void OnClickClosePalette()
    {
        // 如果设置为常驻显示，则不允许关闭
        if (alwaysShowUI) return;

        if (buildPalettePanel != null)
            buildPalettePanel.SetActive(false);
    }

    // ======================
    // UI Refresh
    // ======================

    private void RefreshModeText()
    {
        if (modeTMP == null || placement == null) return;

        if (placement.DemolishMode)
            modeTMP.text = "Mode: Demolish";
        else if (placement.BuildMode)
            modeTMP.text = "Mode: Build";
        else
            modeTMP.text = "Mode: Normal";
    }

    private void RefreshButtonInteractivity()
    {
        if (placement == null) return;

        // 拆除模式下：旋转对建造无意义，可以禁用
        bool canBuildOps = placement.BuildMode && !placement.DemolishMode;

        if (rotateBtn != null) rotateBtn.interactable = canBuildOps;
        if (cancelBtn != null) cancelBtn.interactable = true; // Cancel 永远可用（用于退出模式）

        // 建造按钮/拆除按钮永远可点
        if (buildBtn != null) buildBtn.interactable = true;
        if (demolishBtn != null) demolishBtn.interactable = true;
    }

    // ======================
    // Auto build buttons
    // ======================

    public void RebuildBuildButtons()
    {
        if (!autoBuildButtons) return;
        if (buildButtonContainer == null || buildButtonPrefab == null) return;

        // clear
        for (int i = 0; i < _spawnedButtons.Count; i++)
            if (_spawnedButtons[i] != null) Destroy(_spawnedButtons[i].gameObject);
        _spawnedButtons.Clear();

        var defs = GetBuildablesFromCatalog(catalog);
        foreach (var def in defs)
        {
            if (def == null) continue;

            var btn = Instantiate(buildButtonPrefab, buildButtonContainer);
            _spawnedButtons.Add(btn);

            // 设置按钮尺寸（可选）
            if (overrideButtonSize)
            {
                RectTransform rt = btn.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.sizeDelta = buttonSize;
                }
            }

            // button label
            var tmp = btn.GetComponentInChildren<TMP_Text>();
            if (tmp != null)
            {
                if (showCostOnButton && def.buildCost != null && def.buildCost.Count > 0)
                    tmp.text = $"{def.displayName}\n(Cost: {def.buildCost.Count} items)";
                else
                    tmp.text = def.displayName;
            }

            btn.onClick.AddListener(() =>
            {
                if (placement == null) return;

                // 选建筑时，如果在拆除模式，先退出拆除
                if (placement.DemolishMode)
                {
                    placement.SetMode(PlacementManager.InteractionMode.Normal, clearSelection: true);
                }

                // 进入建造模式并设置当前建筑
                placement.SetMode(PlacementManager.InteractionMode.Build, clearSelection: false);
                placement.SetCurrent(def);

                if (buildPalettePanel != null) buildPalettePanel.SetActive(true);
            });
        }
    }

    private static IEnumerable<BuildableDefinition> GetBuildablesFromCatalog(BuildCatalog cat)
    {
        if (cat == null) yield break;

        // 用反射尽量兼容你的 BuildCatalog 字段命名（buildables / Buildables 等）
        var t = cat.GetType();
        FieldInfo f =
            t.GetField("buildables", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
            t.GetField("Buildables", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (f == null) yield break;

        object v = f.GetValue(cat);
        if (v == null) yield break;

        // 常见：List<BuildableDefinition>
        if (v is IEnumerable<BuildableDefinition> typed)
        {
            foreach (var d in typed) yield return d;
            yield break;
        }

        // 兜底：IList
        if (v is IList list)
        {
            foreach (var item in list)
                if (item is BuildableDefinition d) yield return d;
        }
    }
}