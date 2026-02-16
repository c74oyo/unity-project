using System.Collections.Generic;
using UnityEngine;

public class WorkerAssignmentManager : MonoBehaviour
{
    public static WorkerAssignmentManager Instance { get; private set; }

    // card -> (worksite, slotIndex)
    private readonly Dictionary<CharacterCard, (Worksite ws, int slot)> _equipped = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public bool TryGetEquippedInfo(CharacterCard card, out Worksite ws, out int slotIndex)
    {
        if (card != null && _equipped.TryGetValue(card, out var info) && info.ws != null)
        {
            ws = info.ws;
            slotIndex = info.slot;
            return true;
        }
        ws = null;
        slotIndex = -1;
        return false;
    }

    public bool IsEquipped(CharacterCard card) => TryGetEquippedInfo(card, out _, out _);

    /// <summary>
    /// 尝试把 card 装到 ws 的 slotIndex。
    /// allowMove=false：若 card 已在别处装备，返回 false。
    /// allowMove=true：会先从旧 Worksite 卸下，再装到新 Worksite。
    /// </summary>
    public bool AssignCard(Worksite ws, int slotIndex, CharacterCard card, bool allowMove, out string failReason)
    {
        failReason = null;
        if (ws == null) { failReason = "Worksite is null"; return false; }
        if (card == null) { failReason = "Card is null"; return false; }
        if (!ws.IsValidSlot(slotIndex)) { failReason = "Invalid slot"; return false; }

        // 如果该卡已经装备在某处
        if (TryGetEquippedInfo(card, out var oldWs, out var oldSlot))
        {
            // 已经在同一个槽位则视为成功
            if (oldWs == ws && oldSlot == slotIndex) return true;

            if (!allowMove)
            {
                failReason = $"Card already equipped on {oldWs.name}";
                return false;
            }

            // 允许移动：先卸下旧位置
            if (oldWs != null)
                oldWs.SetCardInternal(oldSlot, null);

            _equipped.Remove(card);
        }

        // 如果目标槽位已经有卡，先解除其占用表
        var existing = ws.GetCard(slotIndex);
        if (existing != null)
            _equipped.Remove(existing);

        ws.SetCardInternal(slotIndex, card);
        _equipped[card] = (ws, slotIndex);
        return true;
    }

    public void UnassignCard(Worksite ws, int slotIndex)
    {
        if (ws == null || !ws.IsValidSlot(slotIndex)) return;

        var card = ws.GetCard(slotIndex);
        if (card != null)
            _equipped.Remove(card);

        ws.SetCardInternal(slotIndex, null);
    }

    public void UnassignCardEverywhere(CharacterCard card)
    {
        if (!TryGetEquippedInfo(card, out var ws, out var slot)) return;
        if (ws != null) ws.SetCardInternal(slot, null);
        _equipped.Remove(card);
    }
}
