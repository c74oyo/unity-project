using UnityEngine;
using UnityEngine.Rendering;

public class RuntimeGridOverlayGL : MonoBehaviour
{
    [Header("References")]
    public GridSystem grid;
    public Camera targetCamera; // 可选：为空则对所有相机绘制

    [Header("Render Settings")]
    public Color lineColor = new Color(1f, 1f, 1f, 0.25f);
    public float yOffset = 0.03f; // 略微抬高，避免和地面 Z-fighting
    public bool drawOnlyWhenSelectedBuildingMode = false;

    [Header("Optional: Build Mode Hook")]
    public PlacementManager placementManager; // 可选：如果你想只在建造模式显示

    [Header("Debug")]
    public bool enableDebugLogs = true;

    private Material lineMat;
    private int renderCallCount = 0;
    private float lastLogTime = 0f;

    void Start()
    {
        if (enableDebugLogs)
        {
            Debug.Log("=== [RuntimeGridOverlayGL] Start Diagnostic ===");
            Debug.Log($"Grid: {(grid != null ? "OK" : "NULL")}");
            Debug.Log($"Target Camera: {(targetCamera != null ? targetCamera.name : "NULL (will render to all cameras)")}");
            Debug.Log($"Line Color: {lineColor}");
            Debug.Log($"Y Offset: {yOffset}");
            Debug.Log($"Draw Only When Building Mode: {drawOnlyWhenSelectedBuildingMode}");
            Debug.Log($"Placement Manager: {(placementManager != null ? "OK" : "NULL")}");

            if (grid != null)
            {
                Debug.Log($"Grid Origin: {grid.origin}");
                Debug.Log($"Grid Size: {grid.width}x{grid.height}");
                Debug.Log($"Grid Cell Size: {grid.cellSize}");
            }

            // 检查渲染管线
            var pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline == null)
            {
                Debug.Log("Render Pipeline: Built-in (Legacy)");
                Debug.LogWarning("[RuntimeGridOverlayGL] Using Built-in pipeline. RenderPipelineManager events might not fire!");
                Debug.LogWarning("[RuntimeGridOverlayGL] Consider using SimpleGridRenderer instead or switch to URP/HDRP.");
            }
            else
            {
                Debug.Log($"Render Pipeline: {pipeline.GetType().Name}");
            }

            Debug.Log("=== [RuntimeGridOverlayGL] End Diagnostic ===");
        }
    }

    void OnEnable()
    {
        if (enableDebugLogs)
            Debug.Log($"[RuntimeGridOverlayGL] OnEnable called. Grid={grid != null}, Camera={targetCamera != null}");

        EnsureMaterial();

        // 订阅 URP/HDRP 事件（如果使用 SRP）
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;

        if (enableDebugLogs)
            Debug.Log($"[RuntimeGridOverlayGL] Subscribed to RenderPipelineManager.endCameraRendering");
    }

    void OnDisable()
    {
        if (enableDebugLogs)
            Debug.Log($"[RuntimeGridOverlayGL] OnDisable called. RenderCallCount={renderCallCount}");

        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        if (lineMat != null) DestroyImmediate(lineMat);
    }

    // Built-in 渲染管线回调（兼容传统管线）
    void OnRenderObject()
    {
        // 检查是否是 Built-in 管线
        if (GraphicsSettings.currentRenderPipeline != null)
            return; // URP/HDRP 会用 OnEndCameraRendering，不需要这个

        // Built-in 管线的渲染逻辑
        if (grid == null) return;

        // 检查当前相机
        Camera currentCam = Camera.current;
        if (currentCam == null) return;
        if (targetCamera != null && currentCam != targetCamera) return;

        // 检查建造模式
        if (drawOnlyWhenSelectedBuildingMode)
        {
            if (placementManager == null) return;
            if (!placementManager.BuildMode) return;
        }

        DrawGridLines();
    }

    private void EnsureMaterial()
    {
        if (lineMat != null) return;

        // Unity 内置线框材质（可用于 GL）
        Shader shader = Shader.Find("Hidden/Internal-Colored");

        if (shader == null)
        {
            Debug.LogError("[RuntimeGridOverlayGL] Cannot find shader 'Hidden/Internal-Colored'!");
            return;
        }

        lineMat = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        // 开启透明混合、关闭背面裁剪、关闭深度写入
        lineMat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        lineMat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        lineMat.SetInt("_Cull", (int)CullMode.Off);
        lineMat.SetInt("_ZWrite", 0);

        if (enableDebugLogs)
            Debug.Log($"[RuntimeGridOverlayGL] Material created successfully. Shader={shader.name}");
    }

    private void OnEndCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        renderCallCount++;

        // 每秒只打印一次日志，避免刷屏
        if (enableDebugLogs && Time.time - lastLogTime > 1f)
        {
            Debug.Log($"[RuntimeGridOverlayGL] OnEndCameraRendering called. Count={renderCallCount}, Camera={cam.name}");
            lastLogTime = Time.time;
        }

        if (grid == null)
        {
            if (enableDebugLogs && renderCallCount == 1)
                Debug.LogWarning("[RuntimeGridOverlayGL] Grid is null! Cannot draw grid.");
            return;
        }

        if (targetCamera != null && cam != targetCamera)
        {
            if (enableDebugLogs && renderCallCount == 1)
                Debug.Log($"[RuntimeGridOverlayGL] Camera mismatch. Target={targetCamera.name}, Current={cam.name}");
            return;
        }

        if (drawOnlyWhenSelectedBuildingMode)
        {
            if (placementManager == null)
            {
                if (enableDebugLogs && renderCallCount == 1)
                    Debug.LogWarning("[RuntimeGridOverlayGL] drawOnlyWhenSelectedBuildingMode=true but PlacementManager is null!");
                return;
            }

            if (!placementManager.BuildMode)
            {
                if (enableDebugLogs && Time.time - lastLogTime > 1f)
                    Debug.Log($"[RuntimeGridOverlayGL] Not in Build Mode. BuildMode={placementManager.BuildMode}");
                return;
            }
        }

        if (enableDebugLogs && renderCallCount == 1)
            Debug.Log("[RuntimeGridOverlayGL] All checks passed, calling DrawGridLines()");

        DrawGridLines();
    }

    private void DrawGridLines()
    {
        EnsureMaterial();
        if (lineMat == null)
        {
            if (enableDebugLogs && renderCallCount == 1)
                Debug.LogError("[RuntimeGridOverlayGL] lineMat is null in DrawGridLines!");
            return;
        }

        float sizeX = grid.width * grid.cellSize;
        float sizeZ = grid.height * grid.cellSize;

        Vector3 o = grid.origin;
        float y = o.y + yOffset;

        if (enableDebugLogs && renderCallCount == 1)
        {
            Debug.Log($"[RuntimeGridOverlayGL] Drawing grid: Origin={o}, Size=({sizeX}x{sizeZ}), Y={y}, Color={lineColor}");
            Debug.Log($"[RuntimeGridOverlayGL] Grid params: Width={grid.width}, Height={grid.height}, CellSize={grid.cellSize}");
        }

        lineMat.SetPass(0);
        GL.PushMatrix();
        GL.MultMatrix(Matrix4x4.identity);

        GL.Begin(GL.LINES);
        GL.Color(lineColor);

        // 竖线（沿 Z）
        for (int x = 0; x <= grid.width; x++)
        {
            float px = o.x + x * grid.cellSize;
            GL.Vertex(new Vector3(px, y, o.z));
            GL.Vertex(new Vector3(px, y, o.z + sizeZ));
        }

        // 横线（沿 X）
        for (int z = 0; z <= grid.height; z++)
        {
            float pz = o.z + z * grid.cellSize;
            GL.Vertex(new Vector3(o.x, y, pz));
            GL.Vertex(new Vector3(o.x + sizeX, y, pz));
        }

        GL.End();
        GL.PopMatrix();

        if (enableDebugLogs && renderCallCount == 1)
            Debug.Log($"[RuntimeGridOverlayGL] Drew {grid.width + 1} vertical lines and {grid.height + 1} horizontal lines");
    }
}
