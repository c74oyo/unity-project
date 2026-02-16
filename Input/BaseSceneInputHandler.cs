using UnityEngine;

/// <summary>
/// BaseSceneInputHandler - 基地场景输入处理
/// 处理基地场景中的快捷键（如返回大地图）
/// </summary>
public class BaseSceneInputHandler : MonoBehaviour
{
    [Header("References")]
    public BaseSceneLoader sceneLoader;

    [Header("Keybindings")]
    [Tooltip("返回大地图的快捷键")]
    public KeyCode returnToWorldMapKey = KeyCode.Escape;

    private void Awake()
    {
        // Auto-find BaseSceneLoader if not manually set
        if (sceneLoader == null)
            sceneLoader = FindObjectOfType<BaseSceneLoader>();
    }

    private void Update()
    {
        // ESC键返回大地图（会自动保存）
        if (Input.GetKeyDown(returnToWorldMapKey))
        {
            ReturnToWorldMap();
        }
    }

    private void ReturnToWorldMap()
    {
        if (sceneLoader != null)
        {
            sceneLoader.SaveAndReturn();
        }
        else if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.ReturnToWorldMap();
        }
    }
}