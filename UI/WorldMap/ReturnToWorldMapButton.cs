using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 返回大地图按钮 - 从基地视图返回大地图
/// </summary>
[RequireComponent(typeof(Button))]
public class ReturnToWorldMapButton : MonoBehaviour
{
    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(OnButtonClicked);
    }

    private void OnButtonClicked()
    {
        if (BaseSceneManager.Instance != null)
        {
            BaseSceneManager.Instance.ExitBaseToWorldMap();
        }
        else
        {
            Debug.LogWarning("[ReturnToWorldMapButton] BaseSceneManager not found!");
        }
    }

    private void Update()
    {
        // 根据当前视图模式显示/隐藏按钮
        if (BaseSceneManager.Instance != null)
        {
            gameObject.SetActive(BaseSceneManager.Instance.IsInBaseView());
        }
    }
}