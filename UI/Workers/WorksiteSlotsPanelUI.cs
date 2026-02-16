using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WorksiteSlotsPanelUI : MonoBehaviour
{
    [Header("Refs")]
    public Worksite currentWorksite;

    [Header("Slots")]
    public Button[] slotButtons;          // Slot1..SlotN
    public TMP_Text[] slotLabels;         // 每个按钮上的文字TMP（可选）

    [Header("Popup")]
    public CardPopupUI cardPopup;         // 居中弹窗控制器

    public void Bind(Worksite ws)
    {
        currentWorksite = ws;
        Refresh();
    }

    public void Refresh()
    {
        if (currentWorksite == null)
        {
            for (int i = 0; i < slotButtons.Length; i++)
            {
                if (slotButtons[i]) slotButtons[i].interactable = false;
                if (i < slotLabels.Length && slotLabels[i]) slotLabels[i].text = "Slot";
            }
            return;
        }

        int n = currentWorksite.SlotCount;

        for (int i = 0; i < slotButtons.Length; i++)
        {
            int idx = i;

            if (slotButtons[i])
            {
                slotButtons[i].interactable = idx < n;

                slotButtons[i].onClick.RemoveAllListeners();
                slotButtons[i].onClick.AddListener(() =>
                {
                    if (cardPopup != null)
                        cardPopup.Open(currentWorksite, idx);
                });
            }

            if (i < slotLabels.Length && slotLabels[i])
            {
                if (idx >= n) slotLabels[i].text = "—";
                else
                {
                    var card = currentWorksite.GetCard(idx);
                    slotLabels[i].text = card != null ? card.displayName : "Empty";
                }
            }
        }
    }
}
