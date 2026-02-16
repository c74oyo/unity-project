using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 基地列表项UI - 用于侧边栏显示
/// </summary>
public class BaseListItemUI : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI baseNameText;
    public TextMeshProUGUI basePositionText;
    public Button selectButton;
    public Image backgroundImage;

    [Header("Resource Zone")]
    public Image resourceZoneIcon;  // 资源区小图标（可选）
    public GameObject resourceZoneBadge;  // 资源区徽章（可选）

    [Header("Colors")]
    public Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    public Color selectedColor = new Color(0.3f, 0.5f, 0.7f, 0.9f);
    public Color hoverColor = new Color(0.25f, 0.35f, 0.45f, 0.85f);

    // Data
    public string BaseId { get; private set; }
    private bool _isSelected = false;

    // Events
    public event System.Action<string> OnItemClicked;

    private void Awake()
    {
        if (selectButton != null)
        {
            selectButton.onClick.AddListener(OnButtonClicked);
        }
    }

    public void Setup(string baseId, string baseName, Vector3 worldPosition)
    {
        BaseId = baseId;

        if (baseNameText != null)
            baseNameText.text = baseName;

        if (basePositionText != null)
            basePositionText.text = $"Pos: ({worldPosition.x:F0}, {worldPosition.z:F0})";

        // 隐藏资源区图标（旧的Setup方法不支持资源区信息）
        if (resourceZoneIcon != null)
            resourceZoneIcon.gameObject.SetActive(false);

        UpdateVisuals();
    }

    /// <summary>
    /// 使用 BaseInfoData 设置列表项（支持资源区图标显示）
    /// </summary>
    public void Setup(BaseInfoData baseData, System.Action<string> onSelectCallback)
    {
        BaseId = baseData.baseId;

        if (baseNameText != null)
            baseNameText.text = baseData.baseName;

        if (basePositionText != null)
            basePositionText.text = $"Pos: ({baseData.worldPosition.x:F0}, {baseData.worldPosition.z:F0})";

        // 显示资源区图标
        if (resourceZoneIcon != null && baseData.zoneInfo != null)
        {
            resourceZoneIcon.gameObject.SetActive(true);
            if (baseData.zoneInfo.icon != null)
                resourceZoneIcon.sprite = baseData.zoneInfo.icon;
        }
        else if (resourceZoneIcon != null)
        {
            resourceZoneIcon.gameObject.SetActive(false);
        }

        // 设置点击回调
        if (onSelectCallback != null)
            OnItemClicked += onSelectCallback;

        UpdateVisuals();
    }

    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        UpdateVisuals();
    }

    private void OnButtonClicked()
    {
        OnItemClicked?.Invoke(BaseId);
    }

    private void UpdateVisuals()
    {
        if (backgroundImage != null)
        {
            backgroundImage.color = _isSelected ? selectedColor : normalColor;
        }
    }

    // Hover effects
    public void OnPointerEnter()
    {
        if (backgroundImage != null && !_isSelected)
        {
            backgroundImage.color = hoverColor;
        }
    }

    public void OnPointerExit()
    {
        if (backgroundImage != null && !_isSelected)
        {
            backgroundImage.color = normalColor;
        }
    }
}