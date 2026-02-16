using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 基地3D标记 - 可点击的3D图标
/// 用于在大地图上显示和选择基地
/// </summary>
[RequireComponent(typeof(Collider))]
public class BaseMarker3D : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("References")]
    public BaseInstance baseInstance;
    public string baseId;  // 如果没有BaseInstance，使用baseId从BaseManager获取信息

    [Header("Visual Settings")]
    public GameObject highlightObject;  // 高亮显示的对象
    public Color normalColor = Color.white;
    public Color hoverColor = Color.yellow;
    public Color selectedColor = Color.cyan;

    [Header("Auto-Setup")]
    public bool autoFindBaseInstance = true;

    private Renderer[] _renderers;
    private bool _isHovered = false;
    private bool _isSelected = false;

    // Events
    public event System.Action<BaseMarker3D> OnMarkerClicked;

    private void Awake()
    {
        // 自动查找BaseInstance
        if (autoFindBaseInstance && baseInstance == null)
        {
            baseInstance = GetComponentInParent<BaseInstance>();
        }

        // 如果有BaseInstance，获取baseId
        if (baseInstance != null)
        {
            baseId = baseInstance.baseId;
        }

        // 获取所有渲染器
        _renderers = GetComponentsInChildren<Renderer>();

        // 确保有Collider
        var collider = GetComponent<Collider>();
        if (collider == null)
        {
            Debug.LogWarning($"[BaseMarker3D] No collider found on {gameObject.name}, adding BoxCollider");
            gameObject.AddComponent<BoxCollider>();
        }

        UpdateVisuals();
    }

    private void OnMouseDown()
    {
        // 使用传统的鼠标点击检测（适用于3D对象）
        HandleClick();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 使用EventSystem的点击检测（需要PhysicsRaycaster）
        HandleClick();
    }

    private void HandleClick()
    {
        Debug.Log($"[BaseMarker3D] Clicked on base marker: {GetBaseName()}");

        // 触发点击事件
        OnMarkerClicked?.Invoke(this);

        string id = GetBaseId();

        // 使用 BaseMapUI 选中基地（会移动摄像机并打开详情弹窗）
        if (BaseMapUI.Instance != null)
        {
            BaseMapUI.Instance.SelectBase(id);
        }
        // 回退：直接打开弹出面板
        else if (BaseDetailPopup.Instance != null)
        {
            BaseDetailPopup.Instance.Show(id);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovered = true;
        UpdateVisuals();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        UpdateVisuals();
    }

    private void OnMouseEnter()
    {
        _isHovered = true;
        UpdateVisuals();
    }

    private void OnMouseExit()
    {
        _isHovered = false;
        UpdateVisuals();
    }

    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        Color targetColor = normalColor;

        if (_isSelected)
            targetColor = selectedColor;
        else if (_isHovered)
            targetColor = hoverColor;

        // 更新所有渲染器的颜色
        foreach (var renderer in _renderers)
        {
            if (renderer != null)
            {
                renderer.material.color = targetColor;
            }
        }

        // 显示/隐藏高亮对象
        if (highlightObject != null)
        {
            highlightObject.SetActive(_isHovered || _isSelected);
        }
    }

    // ============ Public Methods ============

    public string GetBaseId()
    {
        if (baseInstance != null)
            return baseInstance.baseId;
        return baseId;
    }

    public string GetBaseName()
    {
        if (baseInstance != null)
            return baseInstance.baseName;

        // 从BaseManager获取
        if (!string.IsNullOrEmpty(baseId) && BaseManager.Instance != null)
        {
            var saveData = BaseManager.Instance.GetBaseSaveData(baseId);
            if (saveData != null)
                return saveData.baseName;
        }

        return "Unknown Base";
    }

    public BaseInfoData GetBaseInfo()
    {
        if (baseInstance != null)
            return BaseInfoData.FromBaseInstance(baseInstance);

        // 从BaseManager获取保存数据
        if (!string.IsNullOrEmpty(baseId) && BaseManager.Instance != null)
        {
            var saveData = BaseManager.Instance.GetBaseSaveData(baseId);
            if (saveData != null)
                return BaseInfoData.FromBaseSaveData(saveData);
        }

        return null;
    }

    // ============ Debug ============
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 1f);

        UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, $"Base Marker\n{GetBaseName()}");
    }
#endif
}