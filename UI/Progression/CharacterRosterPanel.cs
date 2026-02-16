using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 角色列表面板 — 显示所有已拥有角色，点击进入详情
/// </summary>
public class CharacterRosterPanel : MonoBehaviour
{
    public static CharacterRosterPanel Instance { get; private set; }

    // ============ UI References ============

    [Header("Panel")]
    public GameObject panel;
    public Button closeButton;

    [Header("Character List")]
    public Transform characterListParent;
    public GameObject characterRowPrefab;

    [Header("Detail Panel Reference")]
    public CharacterDetailPanel detailPanel;

    // ============ Runtime ============

    private readonly List<GameObject> _spawnedRows = new();

    // ============ Lifecycle ============

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (panel != null) panel.SetActive(false);
    }

    private void Start()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    // ============ Public API ============

    public void Open()
    {
        // 自动查找 detailPanel（防止 Inspector 未拖入）
        if (detailPanel == null)
            detailPanel = CharacterDetailPanel.Instance;
        if (detailPanel == null)
            detailPanel = FindObjectOfType<CharacterDetailPanel>(true); // 包括 inactive

        if (panel != null) panel.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        ClearRows();
        if (panel != null) panel.SetActive(false);
    }

    // ============ Refresh ============

    public void Refresh()
    {
        ClearRows();
        var prog = CharacterProgressionManager.Instance;
        if (prog == null) return;

        var ownedUnits = prog.GetOwnedUnits();
        foreach (var unitId in ownedUnits)
        {
            if (characterRowPrefab == null || characterListParent == null) break;

            var row = Instantiate(characterRowPrefab, characterListParent);
            _spawnedRows.Add(row);

            var unitDef = prog.GetUnitDef(unitId);
            int level = prog.GetLevel(unitId);

            // 配置行内容
            var nameText = row.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            var levelText = row.transform.Find("LevelText")?.GetComponent<TextMeshProUGUI>();
            var portraitImg = row.transform.Find("Portrait")?.GetComponent<Image>();
            var selectBtn = row.GetComponent<Button>();

            if (nameText != null)
                nameText.text = unitDef != null ? unitDef.unitName : unitId;
            if (levelText != null)
                levelText.text = $"Lv.{level}";
            if (portraitImg != null && unitDef != null && unitDef.portrait != null)
                portraitImg.sprite = unitDef.portrait;

            // 点击打开详情
            string capturedId = unitId;
            // 如果 prefab 没有 Button 组件，自动添加
            if (selectBtn == null)
                selectBtn = row.AddComponent<Button>();

            selectBtn.onClick.AddListener(() =>
            {
                if (detailPanel != null)
                {
                    detailPanel.Open(capturedId);
                }
                else
                {
                    Debug.LogWarning($"[CharacterRosterPanel] detailPanel is null! Cannot open detail for {capturedId}");
                }
            });
        }
    }

    private void ClearRows()
    {
        foreach (var go in _spawnedRows)
            if (go != null) Destroy(go);
        _spawnedRows.Clear();
    }
}
