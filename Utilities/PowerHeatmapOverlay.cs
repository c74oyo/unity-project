using UnityEngine;

/// <summary>
/// Power heatmap overlay: samples effective power supply over your GridSystem area
/// and renders a transparent texture on a ground-aligned quad.
/// </summary>
[DisallowMultipleComponent]
public class PowerHeatmapOverlay : MonoBehaviour
{
    [Header("Area Source")]
    public GridSystem grid;                  // 推荐直接拖你的 GridSystem
    public bool fallbackUseManualArea = false;

    [Header("Manual Area (if no GridSystem)")]
    public Vector3 origin = Vector3.zero;
    public float cellSize = 1f;
    public int width = 50;
    public int height = 50;

    [Header("Overlay")]
    public MeshRenderer overlayRenderer;     // 贴图会设置到这里
    public float overlayY = 0.05f;           // 覆盖层离地高度，避免 Z-fighting
    public bool visible = true;

    [Header("Sampling / Performance")]
    [Tooltip("每隔多少秒刷新一次热力图（0.2~1.0比较合适）")]
    public float refreshInterval = 0.5f;

    [Tooltip("每个像素代表多少个格子。1=每格一个像素（最清晰但更慢），2=2格一个像素（更快）")]
    [Min(1)] public int cellsPerPixel = 1;

    [Header("Value Mapping")]
    [Tooltip("用于归一化的最大值（0=自动估算）")]
    public float maxValue = 0f;

    [Tooltip("0 值时的最小可见透明度（建议 0）")]
    [Range(0f, 1f)] public float minAlpha = 0f;

    [Tooltip("最大值时的透明度（建议 0.5~0.8）")]
    [Range(0f, 1f)] public float maxAlpha = 0.65f;

    [Tooltip("颜色渐变（0=低/无，1=高）")]
    public Gradient gradient;

    private Texture2D _tex;
    private Color32[] _pixels;
    private float _timer;

    private void Reset()
    {
        // 给一个默认渐变：透明 -> 蓝 -> 黄 -> 红（你可以自己改）
        gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0f, 0f, 0f, 0f), 0f),
                new GradientColorKey(new Color(0f, 0.6f, 1f, 1f), 0.33f),
                new GradientColorKey(new Color(1f, 0.9f, 0.2f, 1f), 0.70f),
                new GradientColorKey(new Color(1f, 0.2f, 0.2f, 1f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 1f),
            }
        );
    }

    private void Awake()
    {
        EnsureOverlayRenderer();
        RebuildOverlayQuadTransform();
        RebuildTextureIfNeeded();
        RefreshNow();
    }

    private void Update()
    {
        if (!visible)
        {
            if (overlayRenderer != null && overlayRenderer.enabled)
                overlayRenderer.enabled = false;
            return;
        }

        if (overlayRenderer != null && !overlayRenderer.enabled)
            overlayRenderer.enabled = true;

        _timer += Time.deltaTime;
        if (_timer >= refreshInterval)
        {
            _timer = 0f;
            RefreshNow();
        }
    }

    public void SetVisible(bool on)
    {
        visible = on;
        if (overlayRenderer != null) overlayRenderer.enabled = on;
    }

    public void RefreshNow()
    {
        EnsureOverlayRenderer();
        RebuildOverlayQuadTransform();
        RebuildTextureIfNeeded();

        if (_tex == null || _pixels == null) return;

        // 找所有 PowerGenerator
#if UNITY_2023_1_OR_NEWER
        var gens = FindObjectsByType<PowerGenerator>(FindObjectsSortMode.None);
#else
        var gens = FindObjectsOfType<PowerGenerator>();
#endif
        if (gens == null || gens.Length == 0)
        {
            // 没发电站：清空为透明
            for (int i = 0; i < _pixels.Length; i++) _pixels[i] = new Color32(0, 0, 0, 0);
            _tex.SetPixels32(_pixels);
            _tex.Apply(false);
            return;
        }

        // 自动估算 maxValue：取所有 generator supply 之和（这是上限，不会太离谱）
        float autoMax = 0f;
        for (int i = 0; i < gens.Length; i++)
            if (gens[i] != null) autoMax += gens[i].Supply;

        float vmax = (maxValue > 0f) ? maxValue : Mathf.Max(0.0001f, autoMax);

        // 采样区域参数
        GetArea(out Vector3 o, out float cs, out int w, out int h);

        int pxW = Mathf.Max(1, w / cellsPerPixel);
        int pxH = Mathf.Max(1, h / cellsPerPixel);

        // 每个像素对应的“格子中心”（用块中心采样）
        int idx = 0;
        for (int py = 0; py < pxH; py++)
        {
            for (int px = 0; px < pxW; px++)
            {
                // block center cell index
                int cellX = Mathf.Clamp(px * cellsPerPixel + cellsPerPixel / 2, 0, w - 1);
                int cellY = Mathf.Clamp(py * cellsPerPixel + cellsPerPixel / 2, 0, h - 1);

                Vector3 pos = o + new Vector3((cellX + 0.5f) * cs, 0f, (cellY + 0.5f) * cs);

                float value = 0f;
                for (int g = 0; g < gens.Length; g++)
                {
                    var gen = gens[g];
                    if (gen == null) continue;
                    value += gen.GetEffectiveSupplyAt(pos);
                }

                float t = Mathf.Clamp01(value / vmax);

                Color c = gradient.Evaluate(t);
                c.a = Mathf.Lerp(minAlpha, maxAlpha, t);

                _pixels[idx++] = (Color32)c;
            }
        }

        // Unity 纹理 y 轴方向与世界 z 轴有时会反；我们这里保持 “py=0 对应 z=0”
        _tex.SetPixels32(_pixels);
        _tex.Apply(false);
    }

    private void GetArea(out Vector3 o, out float cs, out int w, out int h)
    {
        if (grid != null && !fallbackUseManualArea)
        {
            o = grid.origin;
            cs = grid.cellSize;
            w = grid.width;
            h = grid.height;
        }
        else
        {
            o = origin;
            cs = cellSize;
            w = width;
            h = height;
        }
    }

    private void EnsureOverlayRenderer()
    {
        if (overlayRenderer != null) return;

        // 尝试找同物体的 MeshRenderer
        overlayRenderer = GetComponent<MeshRenderer>();
        if (overlayRenderer == null)
        {
            // 自动创建一个 Quad 子物体
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "PowerHeatmapQuad";
            quad.transform.SetParent(transform, false);
            // Quad 自带 Collider，删掉避免挡射线
            var col = quad.GetComponent<Collider>();
            if (col != null) Destroy(col);

            overlayRenderer = quad.GetComponent<MeshRenderer>();
        }
    }

    private void RebuildOverlayQuadTransform()
    {
        if (overlayRenderer == null) return;

        GetArea(out Vector3 o, out float cs, out int w, out int h);

        float sizeX = w * cs;
        float sizeZ = h * cs;

        Transform t = overlayRenderer.transform;
        // 把 quad 放到区域中心
        t.position = o + new Vector3(sizeX * 0.5f, overlayY, sizeZ * 0.5f);
        t.rotation = Quaternion.Euler(90f, 0f, 0f); // Quad 面朝上
        t.localScale = new Vector3(sizeX, sizeZ, 1f);
    }

    private void RebuildTextureIfNeeded()
    {
        GetArea(out _, out _, out int w, out int h);

        int pxW = Mathf.Max(1, w / cellsPerPixel);
        int pxH = Mathf.Max(1, h / cellsPerPixel);

        if (_tex != null && _tex.width == pxW && _tex.height == pxH)
            return;

        _tex = new Texture2D(pxW, pxH, TextureFormat.RGBA32, false, true);
        _tex.filterMode = FilterMode.Bilinear;
        _tex.wrapMode = TextureWrapMode.Clamp;

        _pixels = new Color32[pxW * pxH];

        // 把贴图塞进材质
        if (overlayRenderer != null)
        {
            // 你需要给 overlayRenderer 一个透明 Unlit 材质（URP/Unlit），否则会不透明或看不见
            var mat = overlayRenderer.sharedMaterial;
            if (mat != null)
                mat.mainTexture = _tex;
        }
    }
}