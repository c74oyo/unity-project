using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// WorldMapTester - 大地图测试脚本
/// 用于测试基地创建和场景切换功能
/// </summary>
public class WorldMapTester : MonoBehaviour
{
    [Header("Test Config")]
    [Tooltip("Base name prefix")]
    public string baseNamePrefix = "Base";

    [Header("Build Mode")]
    [Tooltip("Is in build mode")]
    public bool isInBuildMode = false;

    [Tooltip("Key to toggle build mode")]
    public KeyCode buildModeKey = KeyCode.B;

    [Header("Input Hints")]
    [Multiline]
    public string inputHints =
        "B: Toggle build mode\n" +
        "Left click (build mode): Create base\n" +
        "Right click: Exit build mode\n" +
        "Space: Enter first base\n" +
        "1-9: Enter base by index";

    private Camera _mainCamera;
    private int _baseCounter = 0;

    private void Start()
    {
        _mainCamera = Camera.main;
    }

    private void Update()
    {
        // 快捷键由 WorldMapToolbar 统一管理，这里不再自行切换模式
        // 只保留非模式类快捷键

        // 右键退出建造模式
        if (Input.GetMouseButtonDown(1) && isInBuildMode)
        {
            // 通知 Toolbar 退出（如果存在），否则直接退出
            if (WorldMapToolbar.Instance != null)
                WorldMapToolbar.Instance.SetMode(WorldMapToolbar.ToolMode.None);
            else
                ExitBuildMode();
        }

        // 左键点击
        if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
        {
            if (isInBuildMode)
            {
                TryCreateBaseAtMousePosition();
            }
            else
            {
                // 非建造模式下，检测是否点击了基地标记（仅在 None 模式下）
                bool isNoneMode = WorldMapToolbar.Instance == null ||
                                  WorldMapToolbar.Instance.CurrentMode == WorldMapToolbar.ToolMode.None;
                if (isNoneMode)
                    TryClickBaseMarker();
            }
        }

        // 空格键进入第一个基地
        if (Input.GetKeyDown(KeyCode.Space))
        {
            EnterFirstBase();
        }

        // L 键列出所有基地
        if (Input.GetKeyDown(KeyCode.L))
        {
            ListAllBases();
        }
    }

    // ============ 建造模式 ============

    /// <summary>
    /// 切换建造模式
    /// </summary>
    public void ToggleBuildMode()
    {
        isInBuildMode = !isInBuildMode;
    }

    /// <summary>
    /// 进入建造模式
    /// </summary>
    public void EnterBuildMode()
    {
        isInBuildMode = true;
    }

    /// <summary>
    /// 退出建造模式
    /// </summary>
    public void ExitBuildMode()
    {
        isInBuildMode = false;
    }

    /// <summary>
    /// 检测鼠标是否在UI上
    /// </summary>
    private bool IsPointerOverUI()
    {
        // 检查是否有 EventSystem
        if (EventSystem.current == null)
            return false;

        return EventSystem.current.IsPointerOverGameObject();
    }

    /// <summary>
    /// 射线检测是否点击了基地标记
    /// </summary>
    private void TryClickBaseMarker()
    {
        if (_mainCamera == null) return;

        Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f);

        // 在所有命中对象中查找 BaseMarker3D（因为地面可能挡住了 OnMouseDown）
        foreach (var hit in hits)
        {
            var marker = hit.collider.GetComponent<BaseMarker3D>();
            if (marker != null)
            {
                string id = marker.GetBaseId();
                Debug.Log($"[WorldMapTester] Clicked base marker: {marker.GetBaseName()} (id={id})");

                // 优先使用 BaseMapUI
                if (BaseMapUI.Instance != null)
                {
                    BaseMapUI.Instance.SelectBase(id);
                }
                else if (BaseDetailPopup.Instance != null)
                {
                    BaseDetailPopup.Instance.Show(id);
                }
                return;
            }
        }
    }

    /// <summary>
    /// 在鼠标点击位置创建基地
    /// </summary>
    private void TryCreateBaseAtMousePosition()
    {
        if (_mainCamera == null || BaseManager.Instance == null)
            return;

        // 发射射线检测地面
        Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
        {
            // 创建基地
            _baseCounter++;
            string baseName = $"{baseNamePrefix} {_baseCounter}";

            BaseSaveData newBase = BaseManager.Instance.CreateNewBase(hit.point, baseName);

            // 可选：立即进入新创建的基地
            // if (newBase != null) SceneTransitionManager.Instance?.EnterBase(newBase.baseId);
        }
    }

    /// <summary>
    /// 进入第一个基地
    /// </summary>
    private void EnterFirstBase()
    {
        if (BaseManager.Instance == null || SceneTransitionManager.Instance == null)
            return;

        var bases = BaseManager.Instance.AllBaseSaveData;
        if (bases.Count == 0) return;

        SceneTransitionManager.Instance.EnterBase(bases[0].baseId);
    }

    /// <summary>
    /// 根据索引进入基地
    /// </summary>
    private void EnterBaseByIndex(int index)
    {
        if (BaseManager.Instance == null || SceneTransitionManager.Instance == null)
            return;

        var bases = BaseManager.Instance.AllBaseSaveData;
        if (index < 0 || index >= bases.Count) return;

        SceneTransitionManager.Instance.EnterBase(bases[index].baseId);
    }

    /// <summary>
    /// 列出所有基地（保留用于调试）
    /// </summary>
    private void ListAllBases()
    {
        if (BaseManager.Instance == null) return;

        var bases = BaseManager.Instance.AllBaseSaveData;
        Debug.Log($"[WorldMapTester] Total bases: {bases.Count}");
        for (int i = 0; i < bases.Count; i++)
        {
            var baseData = bases[i];
            Debug.Log($"  [{i + 1}] {baseData.baseName} - Position: {baseData.worldPosition}");
        }
    }

    // OnGUI debug panel removed — replaced by WorldMapToolbar UI
}