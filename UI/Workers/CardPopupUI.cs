using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardPopupUI : MonoBehaviour
{
    [Header("Refs")]
    public PlayerWorkerRoster roster;
    public WorkerAssignmentManager assignment;

    [Header("UI")]
    public GameObject root;          // 整个弹窗根（Panel_CardPopupRoot）
    public TMP_Text titleTMP;
    public TMP_Text currentTMP;
    public Button removeBtn;
    public Button closeBtn;

    [Header("List")]
    public Transform listContent;    // ScrollView/Viewport/Content
    public CardRowItemUI rowPrefab;

    [Header("Confirm Replace")]
    public ConfirmReplacePopupUI confirmPopup;

    private Worksite _ws;
    private int _slot;

    private readonly List<CardRowItemUI> _rows = new();

    private void Awake()
    {
        if (assignment == null) assignment = WorkerAssignmentManager.Instance;
        if (root != null) root.SetActive(false);

        if (closeBtn) closeBtn.onClick.AddListener(Close);
        if (removeBtn) removeBtn.onClick.AddListener(RemoveCurrent);
    }

    private void Update()
    {
        // 右键点击关闭弹窗
        if (root != null && root.activeSelf && Input.GetMouseButtonDown(1))
        {
            Close();
        }
    }

    public void Open(Worksite ws, int slotIndex)
    {
        _ws = ws;
        _slot = slotIndex;

        if (assignment == null) assignment = WorkerAssignmentManager.Instance;

        if (root != null) root.SetActive(true);
        RefreshHeader();
        RebuildList();
    }

    public void Close()
    {
        if (root != null) root.SetActive(false);
        _ws = null;
        _slot = -1;
    }

    private void RefreshHeader()
    {
        if (_ws == null) return;

        if (titleTMP) titleTMP.text = $"Select Card (Slot {_slot + 1}/{_ws.SlotCount})";

        var current = _ws.GetCard(_slot);
        if (currentTMP)
            currentTMP.text = current != null ? $"Current: {current.displayName}" : "Current: (Empty)";

        if (removeBtn)
            removeBtn.interactable = (current != null);
    }

    private void RemoveCurrent()
    {
        if (_ws == null || assignment == null) return;
        assignment.UnassignCard(_ws, _slot);
        RefreshHeader();
        RebuildList();
    }

    private void RebuildList()
    {
        if (roster == null || rowPrefab == null || listContent == null) return;

        // clear
        for (int i = 0; i < _rows.Count; i++)
            if (_rows[i] != null) Destroy(_rows[i].gameObject);
        _rows.Clear();

        foreach (var card in roster.ownedCards)
        {
            if (card == null) continue;

            var row = Instantiate(rowPrefab, listContent);
            _rows.Add(row);

            string equippedInfo = "(none)";
            bool equippedElsewhere = false;

            if (assignment != null && assignment.TryGetEquippedInfo(card, out var oldWs, out var oldSlot))
            {
                if (oldWs != null)
                {
                    equippedInfo = $"{oldWs.name} (Slot {oldSlot + 1})";
                    equippedElsewhere = !(_ws == oldWs && _slot == oldSlot);
                }
            }

            row.Bind(card, equippedInfo, equippedElsewhere, () => OnClickCard(card));
        }
    }

    private void OnClickCard(CharacterCard card)
    {
        if (_ws == null || card == null || assignment == null) return;

        // 如果这张卡已经在其它建筑，弹确认框
        if (assignment.TryGetEquippedInfo(card, out var oldWs, out _)
            && oldWs != null
            && oldWs != _ws)
        {
            if (confirmPopup != null)
            {
                string msg = $"该“角色”已在 {oldWs.name} 建筑中使用，是否确认替换？";
                confirmPopup.Open(msg,
                    onYes: () =>
                    {
                        assignment.AssignCard(_ws, _slot, card, allowMove: true, out _);
                        RefreshHeader();
                        RebuildList();
                    },
                    onNo: () => { }
                );
            }
            return;
        }

        // 未占用或在本建筑：直接装备（allowMove=true 也无害）
        assignment.AssignCard(_ws, _slot, card, allowMove: true, out _);
        RefreshHeader();
        RebuildList();
    }
}