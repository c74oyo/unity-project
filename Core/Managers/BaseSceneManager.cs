using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Linq;

/// <summary>
/// 基地场景管理器 - 处理大地图和基地视图之间的切换
/// </summary>
public class BaseSceneManager : MonoBehaviour
{
    public static BaseSceneManager Instance { get; private set; }

    [Header("Scene Names")]
    public string worldMapSceneName = "WorldMap";
    public string baseSceneName = "BaseScene";

    [Header("Camera Settings")]
    public BuildCameraController worldMapCamera;
    public Camera baseViewCamera;

    [Header("View Mode")]
    public ViewMode currentViewMode = ViewMode.WorldMap;
    public enum ViewMode
    {
        WorldMap,   // 大地图视图
        BaseView    // 基地详细视图
    }

    [Header("Base View Settings")]
    public Vector3 baseViewCameraOffset = new Vector3(0, 50, -50);
    public float baseViewCameraHeight = 50f;
    public float transitionDuration = 1f;

    // Runtime
    private string _currentBaseId;
    private bool _isTransitioning = false;

    // Events
    public event System.Action<string> OnEnterBase;
    public event System.Action OnExitBase;

    // ============ Lifecycle ============

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 自动查找摄像机
        if (worldMapCamera == null)
            worldMapCamera = FindObjectOfType<BuildCameraController>();
    }

    // ============ Public Methods ============

    /// <summary>
    /// 进入基地视图
    /// </summary>
    public void EnterBase(string baseId)
    {
        if (_isTransitioning)
        {
            Debug.LogWarning("[BaseSceneManager] Already transitioning!");
            return;
        }

        if (string.IsNullOrEmpty(baseId))
        {
            Debug.LogError("[BaseSceneManager] Invalid base ID!");
            return;
        }

        _currentBaseId = baseId;

        // 设置为活跃基地
        if (BaseManager.Instance != null)
        {
            BaseManager.Instance.SetActiveBase(baseId);
        }

        // 根据配置选择切换方式
        if (!string.IsNullOrEmpty(baseSceneName))
        {
            // 场景切换模式
            StartCoroutine(LoadBaseScene(baseId));
        }
        else
        {
            // 相机切换模式（同一场景内）
            StartCoroutine(TransitionToBaseView(baseId));
        }

        OnEnterBase?.Invoke(baseId);
    }

    /// <summary>
    /// 退出基地，返回大地图
    /// </summary>
    public void ExitBaseToWorldMap()
    {
        if (_isTransitioning)
        {
            Debug.LogWarning("[BaseSceneManager] Already transitioning!");
            return;
        }

        // 根据配置选择切换方式
        if (!string.IsNullOrEmpty(worldMapSceneName))
        {
            // 场景切换模式
            StartCoroutine(LoadWorldMapScene());
        }
        else
        {
            // 相机切换模式
            StartCoroutine(TransitionToWorldMap());
        }

        OnExitBase?.Invoke();
        _currentBaseId = null;
    }

    // ============ Scene Loading ============

    private IEnumerator LoadBaseScene(string baseId)
    {
        _isTransitioning = true;

        // 保存当前基地数据
        if (BaseManager.Instance != null)
        {
            var activeBase = FindBaseInstanceById(baseId);
            if (activeBase != null)
            {
                SaveBaseToManager(activeBase);
            }
        }

        // 加载基地场景
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(baseSceneName);
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        // 场景加载完成后，实例化基地
        yield return new WaitForEndOfFrame();
        InstantiateBaseInScene(baseId);

        _isTransitioning = false;
    }

    private IEnumerator LoadWorldMapScene()
    {
        _isTransitioning = true;

        // 保存当前基地数据
        if (!string.IsNullOrEmpty(_currentBaseId))
        {
            var activeBase = FindBaseInstanceById(_currentBaseId);
            if (activeBase != null)
            {
                SaveBaseToManager(activeBase);
            }
        }

        // 加载大地图场景
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(worldMapSceneName);
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        // 等待几帧确保场景完全初始化
        yield return null;
        yield return null;

        // 重新加载所有基地标记
        if (BaseManager.Instance != null)
        {
            BaseManager.Instance.LoadAllBaseMarkers();
        }

        currentViewMode = ViewMode.WorldMap;
        _isTransitioning = false;
    }

    // ============ Camera Transition (Same Scene) ============

    private IEnumerator TransitionToBaseView(string baseId)
    {
        _isTransitioning = true;

        if (BaseManager.Instance == null)
        {
            _isTransitioning = false;
            yield break;
        }

        var saveData = BaseManager.Instance.GetBaseSaveData(baseId);
        if (saveData == null)
        {
            Debug.LogError($"[BaseSceneManager] Base not found: {baseId}");
            _isTransitioning = false;
            yield break;
        }

        // 移动相机到基地位置
        Vector3 targetPosition = saveData.worldPosition + baseViewCameraOffset;

        if (worldMapCamera != null)
        {
            float elapsedTime = 0f;
            Vector3 startPosition = worldMapCamera.transform.position;

            while (elapsedTime < transitionDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / transitionDuration;
                worldMapCamera.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
                yield return null;
            }

            worldMapCamera.transform.position = targetPosition;
        }

        currentViewMode = ViewMode.BaseView;
        _isTransitioning = false;

        // 隐藏大地图UI
        if (BaseMapUI.Instance != null)
        {
            BaseMapUI.Instance.DeselectBase();
            BaseMapUI.Instance.HideSidebar();
        }
    }

    private IEnumerator TransitionToWorldMap()
    {
        _isTransitioning = true;

        // 相机回到世界视图位置
        // 这里可以保存之前的相机位置，或者移动到一个默认位置

        yield return new WaitForSeconds(transitionDuration);

        currentViewMode = ViewMode.WorldMap;
        _isTransitioning = false;

        // 显示大地图UI
        if (BaseMapUI.Instance != null)
        {
            BaseMapUI.Instance.ToggleSidebar();
        }
    }

    // ============ Base Instantiation ============

    private void InstantiateBaseInScene(string baseId)
    {
        if (BaseManager.Instance == null) return;

        var saveData = BaseManager.Instance.GetBaseSaveData(baseId);
        if (saveData == null)
        {
            Debug.LogError($"[BaseSceneManager] Base save data not found: {baseId}");
            return;
        }

        // 创建基地实例 - 注意：基地场景中的位置应该是原点，不是世界地图坐标
        Vector3 baseScenePosition = Vector3.zero;
        GameObject baseGO = null;

        if (BaseManager.Instance.baseInstancePrefab != null)
        {
            baseGO = Instantiate(BaseManager.Instance.baseInstancePrefab, baseScenePosition, Quaternion.identity);
        }
        else
        {
            baseGO = new GameObject($"Base_{saveData.baseName}");
            baseGO.transform.position = baseScenePosition;
            baseGO.AddComponent<BaseInstance>();
        }

        var baseInstance = baseGO.GetComponent<BaseInstance>();
        if (baseInstance != null)
        {
            // 必须在 Awake 之后立即设置 baseId，防止使用错误的 ID
            LoadBaseFromSaveData(baseInstance, saveData);
        }
    }

    private void LoadBaseFromSaveData(BaseInstance baseInstance, BaseSaveData saveData)
    {
        // 立即设置 baseId，覆盖 Awake 中自动生成的 ID
        baseInstance.baseId = saveData.baseId;
        baseInstance.baseName = saveData.baseName;

        // 加载库存
        if (baseInstance.inventory != null)
        {
            baseInstance.inventory.money = saveData.money;

            foreach (var resSave in saveData.resources)
            {
                // 从资源库加载ResourceDefinition
                ResourceDefinition resDef = Resources.LoadAll<ResourceDefinition>("").FirstOrDefault(r => r.name == resSave.resourceName);
                if (resDef != null)
                {
                    baseInstance.inventory.Add(resDef, resSave.amount);
                }
            }
        }

        // TODO: 加载建筑
        // 这需要从saveData.buildings中实例化建筑物
    }

    private void SaveBaseToManager(BaseInstance baseInstance)
    {
        if (baseInstance == null || BaseManager.Instance == null) return;

        // 获取原有保存数据以保留 worldPosition（基地场景中的位置是局部的，不是世界地图位置）
        var existingSaveData = BaseManager.Instance.GetBaseSaveData(baseInstance.baseId);

        if (existingSaveData == null)
        {
            return; // 如果找不到原有数据，跳过保存以防止覆盖错误数据
        }

        // 使用原有的 worldPosition，不要使用 baseInstance.Position
        var saveData = new BaseSaveData(baseInstance.baseId, baseInstance.baseName, existingSaveData.worldPosition);

        // 复制原有的网格设置
        saveData.gridOrigin = existingSaveData.gridOrigin;
        saveData.gridWidth = existingSaveData.gridWidth;
        saveData.gridHeight = existingSaveData.gridHeight;
        saveData.gridCellSize = existingSaveData.gridCellSize;
        saveData.baseCapacity = existingSaveData.baseCapacity;

        // 保存库存
        if (baseInstance.inventory != null)
        {
            saveData.money = baseInstance.inventory.Money;

            var resources = baseInstance.inventory.GetAllResources();
            if (resources != null)
            {
                foreach (var item in resources)
                {
                    if (item.res != null)
                    {
                        saveData.resources.Add(new ResourceSaveData(item.res.name, item.amount));
                    }
                }
            }
        }

        // TODO: 保存建筑

        BaseManager.Instance.UpdateBaseSaveData(saveData);
    }

    private BaseInstance FindBaseInstanceById(string baseId)
    {
        var allBases = FindObjectsOfType<BaseInstance>();

        foreach (var baseInstance in allBases)
        {
            if (baseInstance.baseId == baseId)
                return baseInstance;
        }

        // 如果找不到精确匹配，返回场景中唯一的 BaseInstance（如果只有一个）
        if (allBases.Length == 1)
        {
            return allBases[0];
        }

        return null;
    }

    // ============ Public Utility Methods ============

    public bool IsInBaseView()
    {
        return currentViewMode == ViewMode.BaseView;
    }

    public bool IsInWorldMap()
    {
        return currentViewMode == ViewMode.WorldMap;
    }

    public string GetCurrentBaseId()
    {
        return _currentBaseId;
    }
}