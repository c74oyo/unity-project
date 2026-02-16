using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ConfirmReplacePopupUI : MonoBehaviour
{
    public GameObject root;      // Panel_ConfirmReplaceRoot
    public TMP_Text messageTMP;
    public Button yesBtn;
    public Button noBtn;

    private Action _onYes;
    private Action _onNo;

    private void Awake()
    {
        if (root != null) root.SetActive(false);

        if (yesBtn) yesBtn.onClick.AddListener(() => { Close(); _onYes?.Invoke(); });
        if (noBtn) noBtn.onClick.AddListener(() => { Close(); _onNo?.Invoke(); });
    }

    public void Open(string msg, Action onYes, Action onNo)
    {
        _onYes = onYes;
        _onNo = onNo;

        if (messageTMP) messageTMP.text = msg;
        if (root) root.SetActive(true);
    }

    public void Close()
    {
        if (root) root.SetActive(false);
        _onYes = null;
        _onNo = null;
    }
}
