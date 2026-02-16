using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardRowItemUI : MonoBehaviour
{
    public TMP_Text nameTMP;
    public TMP_Text modsTMP;
    public TMP_Text equippedTMP;
    public Button clickBtn;

    private CharacterCard _card;
    private System.Action _onClick;

    public void Bind(CharacterCard card, string equippedInfo, bool equippedElsewhere, System.Action onClick)
    {
        _card = card;
        _onClick = onClick;

        if (nameTMP) nameTMP.text = card != null ? card.displayName : "(null)";
        if (equippedTMP) equippedTMP.text = $"Equipped: {equippedInfo}";

        if (modsTMP)
        {
            var sb = new StringBuilder();
            if (card != null && card.modifiers != null)
            {
                foreach (var m in card.modifiers)
                {
                    if (m == null) continue;
                    sb.Append(m.type).Append(" ").Append(m.value >= 0 ? "+" : "").Append(m.value.ToString("0.##")).Append("  ");
                }
            }
            modsTMP.text = sb.Length > 0 ? sb.ToString() : "(no modifiers)";
        }

        if (clickBtn != null)
        {
            clickBtn.onClick.RemoveAllListeners();
            clickBtn.onClick.AddListener(() => _onClick?.Invoke());
        }
    }
}
