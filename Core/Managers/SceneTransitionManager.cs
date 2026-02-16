using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// SceneTransitionManager - 场景切换管理器
/// 处理大地图、基地场景、战斗场景之间的切换
/// </summary>
public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [Header("Scene Names")]
    public string worldMapSceneName = "WorldMap";
    public string baseSceneTemplate = "BaseScene";
    public string combatSceneTemplate = "CombatScene";

    [Header("Loading")]
    public bool showLoadingScreen = true;
    public float minimumLoadTime = 0.5f;

    [Header("Runtime")]
    [SerializeField] private string _currentSceneName;
    [SerializeField] private string _targetBaseId;  // 正在加载的基地ID

    private bool _isLoading = false;

    // Events
    public event Action<string> OnSceneLoadStarted;
    public event Action<string> OnSceneLoadCompleted;
    public event Action<string> OnBeforeSceneUnload;

    // ============ Lifecycle ============
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // 更新当前场景名
            Instance._currentSceneName = SceneManager.GetActiveScene().name;
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        _currentSceneName = SceneManager.GetActiveScene().name;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ============ Scene Transition ============

    /// <summary>
    /// 从当前场景返回大地图
    /// </summary>
    public void ReturnToWorldMap()
    {
        if (_isLoading) return;
        StartCoroutine(LoadSceneAsync(worldMapSceneName, null));
    }

    /// <summary>
    /// 从大地图进入指定基地
    /// </summary>
    public void EnterBase(string baseId)
    {
        if (_isLoading) return;

        if (string.IsNullOrEmpty(baseId))
        {
            Debug.LogError("[SceneTransition] Invalid baseId");
            return;
        }

        _targetBaseId = baseId;
        StartCoroutine(LoadSceneAsync(baseSceneTemplate, baseId));
    }

    /// <summary>
    /// 进入战斗场景
    /// </summary>
    public void EnterCombat(string combatZoneId)
    {
        if (_isLoading) return;
        StartCoroutine(LoadSceneAsync(combatSceneTemplate, combatZoneId));
    }

    // ============ Internal Loading ============
    private IEnumerator LoadSceneAsync(string sceneName, string contextData)
    {
        _isLoading = true;
        float startTime = Time.realtimeSinceStartup;

        OnSceneLoadStarted?.Invoke(sceneName);

        // Notify before unload
        OnBeforeSceneUnload?.Invoke(_currentSceneName);

        // 离开大地图时，保存大地图状态（道路、NPC据点等）
        if (_currentSceneName == worldMapSceneName && BaseManager.Instance != null)
        {
            BaseManager.Instance.SaveCurrentGame();
            Debug.Log("[SceneTransition] Saved world map state before leaving.");
        }

        // Start async load
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);

        if (asyncLoad == null)
        {
            _isLoading = false;
            yield break;
        }

        // Wait for load
        while (!asyncLoad.isDone)
        {
            float progress = Mathf.Clamp01(asyncLoad.progress / 0.9f);
            // TODO: Update loading screen UI here
            yield return null;
        }

        // Ensure minimum load time (for smooth transitions)
        float elapsed = Time.realtimeSinceStartup - startTime;
        if (elapsed < minimumLoadTime)
        {
            yield return new WaitForSecondsRealtime(minimumLoadTime - elapsed);
        }

        _currentSceneName = sceneName;

        // Post-load initialization
        if (sceneName == baseSceneTemplate && !string.IsNullOrEmpty(contextData))
        {
            // Notify that we need to load base data
            BaseSceneLoader loader = FindObjectOfType<BaseSceneLoader>();
            if (loader != null)
            {
                loader.LoadBase(contextData);
            }
        }
        else if (sceneName == worldMapSceneName)
        {
            // 返回大地图时，重新加载存档并恢复大地图状态
            yield return null; // 等待一帧确保场景初始化完成
            if (BaseManager.Instance != null)
            {
                BaseManager.Instance.ReloadAndRestoreWorldMap();
            }
        }

        OnSceneLoadCompleted?.Invoke(sceneName);

        _isLoading = false;
    }

    // ============ Query ============
    public bool IsLoading => _isLoading;
    public string CurrentSceneName => _currentSceneName;
    public bool IsInWorldMap => _currentSceneName == worldMapSceneName;
    public bool IsInBaseScene => _currentSceneName == baseSceneTemplate;
    public bool IsInCombatScene => _currentSceneName == combatSceneTemplate;

    // ============ Debug ============
#if UNITY_EDITOR
    [ContextMenu("Debug: Return to World Map")]
    private void DebugReturnToWorldMap()
    {
        if (!Application.isPlaying) return;
        ReturnToWorldMap();
    }

    [ContextMenu("Debug: Reload Current Scene")]
    private void DebugReloadCurrentScene()
    {
        if (!Application.isPlaying) return;
        StartCoroutine(LoadSceneAsync(_currentSceneName, null));
    }
#endif
}