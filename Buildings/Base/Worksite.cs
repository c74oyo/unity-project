using System;
using UnityEngine;

public class Worksite : MonoBehaviour
{
    [Header("Entrance")]
    public Transform workerEntrancePoint;

    [Header("Workforce (visual)")]
    [Min(0)] public int maxWorkers = 5;
    [Tooltip("玩家设定的目标人数（来自总览面板/建筑面板）")]
    [Min(0)] public int desiredWorkers = 0;

    [Header("Arrival")]
    public float arriveDistance = 0.6f;

    [Header("Worker Visual")]
    public bool hideWorkerOnArrival = true;

    [Header("Producer Link (optional)")]
    public ProducerBuilding producer;

    [Header("Runtime (read only)")]
    [SerializeField] private int presentWorkers = 0;
    [SerializeField] private int enRouteWorkers = 0;
    [SerializeField] private int noPathCount = 0;

    // =========================
    // Card Slots (NEW)
    // =========================
    [Header("Card Slots")]
    [Min(0)] public int slotCount = 2;

    [SerializeField] private CharacterCard[] slotCards;

    /// <summary>卡槽变化事件（CardPopup 分配/移除后，HUD/槽位面板可以刷新）</summary>
    public event Action OnSlotsChanged;

    public bool HasEntrance => workerEntrancePoint != null;
    public Vector3 EntrancePos => workerEntrancePoint != null ? workerEntrancePoint.position : transform.position;

    public int PresentWorkers => presentWorkers;
    public int EnRouteWorkers => enRouteWorkers;
    public int NoPathCount => noPathCount;

    public int ClampedDesired => Mathf.Clamp(desiredWorkers, 0, maxWorkers);

    // 关键：把在路上的人扣掉，否则会一直“Need>0”导致刷屏生成
    public int NeededCount => Mathf.Max(0, ClampedDesired - presentWorkers - enRouteWorkers);

    // ---- Slots API ----
    public int SlotCount => Mathf.Max(0, slotCount);

    public bool IsValidSlot(int index) => index >= 0 && index < SlotCount;

    public CharacterCard GetCard(int slotIndex)
    {
        EnsureSlotArray();
        if (!IsValidSlot(slotIndex)) return null;
        return slotCards[slotIndex];
    }

    public int FilledSlotCount
    {
        get
        {
            EnsureSlotArray();
            int c = 0;
            for (int i = 0; i < slotCards.Length; i++)
                if (slotCards[i] != null) c++;
            return c;
        }
    }

    /// <summary>内部使用：只允许 WorkerAssignmentManager 调用</summary>
    internal void SetCardInternal(int slotIndex, CharacterCard card)
    {
        EnsureSlotArray();
        if (!IsValidSlot(slotIndex)) return;

        slotCards[slotIndex] = card;

        // 通知生产建筑（让卡牌加成立刻生效）
        if (producer != null)
            producer.NotifyCardSlotsChanged();

        OnSlotsChanged?.Invoke();
    }

    public float GetModifierSum(CardModifier.ModifierType type)
    {
        EnsureSlotArray();

        float sum = 0f;
        for (int i = 0; i < slotCards.Length; i++)
        {
            var card = slotCards[i];
            if (card == null || card.modifiers == null) continue;

            for (int m = 0; m < card.modifiers.Count; m++)
            {
                var mod = card.modifiers[m];
                if (mod != null && mod.type == type)
                    sum += mod.value;
            }
        }
        return sum;
    }

    public void SetDesired(int value)
    {
        desiredWorkers = Mathf.Clamp(value, 0, maxWorkers);
    }

    // ===== Workforce runtime callbacks =====

    public void NotifyEnRoute(WorkerUnit worker)
    {
        enRouteWorkers = Mathf.Clamp(enRouteWorkers + 1, 0, maxWorkers);
        SyncProducerWorkers();
    }

    public void NotifyArrived(WorkerUnit worker)
    {
        enRouteWorkers = Mathf.Max(0, enRouteWorkers - 1);
        presentWorkers = Mathf.Clamp(presentWorkers + 1, 0, maxWorkers);

        SyncProducerWorkers();

        if (hideWorkerOnArrival && worker != null)
            worker.HideForWork();
    }

    public void NotifyNoPath(WorkerUnit worker)
    {
        enRouteWorkers = Mathf.Max(0, enRouteWorkers - 1);
        noPathCount = Mathf.Max(0, noPathCount + 1);
        SyncProducerWorkers();
    }

    public void NotifyLeft(WorkerUnit worker)
    {
        presentWorkers = Mathf.Max(0, presentWorkers - 1);
        SyncProducerWorkers();
    }

    private void SyncProducerWorkers()
    {
        if (producer != null)
        {
            // 保留：实体工人到岗写回生产（你之后可以决定是否继续用这条链路）
            producer.SetWorkersFromWorksite(presentWorkers);
        }
    }

    private void EnsureSlotArray()
    {
        int n = SlotCount;
        if (slotCards == null || slotCards.Length != n)
        {
            var old = slotCards;
            slotCards = new CharacterCard[n];

            if (old != null)
            {
                int copy = Mathf.Min(old.Length, slotCards.Length);
                for (int i = 0; i < copy; i++) slotCards[i] = old[i];
            }
        }
    }

    private void Awake()
    {
        EnsureSlotArray();
    }

    private void OnValidate()
    {
        maxWorkers = Mathf.Max(0, maxWorkers);
        desiredWorkers = Mathf.Clamp(desiredWorkers, 0, maxWorkers);
        presentWorkers = Mathf.Clamp(presentWorkers, 0, maxWorkers);
        enRouteWorkers = Mathf.Clamp(enRouteWorkers, 0, maxWorkers);
        arriveDistance = Mathf.Max(0.05f, arriveDistance);

        slotCount = Mathf.Max(0, slotCount);
        EnsureSlotArray();
    }
}
