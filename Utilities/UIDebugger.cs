using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI 调试工具 - 检测 UI 显示问题
/// </summary>
public class UIDebugger : MonoBehaviour
{
    [Header("调试目标")]
    public Canvas[] canvases;
    public GameObject[] uiObjects;

    [Header("设置")]
    public bool logOnStart = true;
    public bool logEveryFrame = false;
    public float logInterval = 2f;

    private float lastLogTime = 0f;

    void Start()
    {
        if (logOnStart)
        {
            Debug.Log("=== [UIDebugger] UI Diagnostic Report ===");
            DiagnoseUI();
        }
    }

    void Update()
    {
        if (logEveryFrame || (Time.time - lastLogTime > logInterval))
        {
            DiagnoseUI();
            lastLogTime = Time.time;
        }
    }

    private void DiagnoseUI()
    {
        Debug.Log("--- Canvas 检测 ---");

        // 自动查找所有 Canvas
        Canvas[] allCanvases = FindObjectsOfType<Canvas>(true);
        Debug.Log($"场景中共找到 {allCanvases.Length} 个 Canvas（包括未激活的）");

        foreach (Canvas canvas in allCanvases)
        {
            bool isActive = canvas.gameObject.activeInHierarchy;
            bool isEnabled = canvas.enabled;
            string renderMode = canvas.renderMode.ToString();
            int sortOrder = canvas.sortingOrder;

            Debug.Log($"Canvas: {canvas.name}");
            Debug.Log($"  ├─ GameObject Active: {isActive}");
            Debug.Log($"  ├─ Component Enabled: {isEnabled}");
            Debug.Log($"  ├─ Render Mode: {renderMode}");
            Debug.Log($"  ├─ Sorting Order: {sortOrder}");
            Debug.Log($"  ├─ Position: {canvas.transform.position}");
            Debug.Log($"  └─ Scale: {canvas.transform.localScale}");

            if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                Debug.Log($"  └─ World Camera: {(canvas.worldCamera != null ? canvas.worldCamera.name : "NULL")}");
            }

            // 检查子对象
            CheckChildren(canvas.transform, 1);
        }

        Debug.Log("--- UI 特定对象检测 ---");

        // 检查用户指定的 UI 对象
        if (uiObjects != null && uiObjects.Length > 0)
        {
            foreach (GameObject obj in uiObjects)
            {
                if (obj == null)
                {
                    Debug.LogWarning("UI Objects 列表中有空引用！");
                    continue;
                }

                bool isActive = obj.activeInHierarchy;
                bool isSelfActive = obj.activeSelf;

                Debug.Log($"UI Object: {obj.name}");
                Debug.Log($"  ├─ ActiveInHierarchy: {isActive}");
                Debug.Log($"  ├─ ActiveSelf: {isSelfActive}");
                Debug.Log($"  └─ Position: {obj.transform.position}");

                // 检查 Image 组件
                Image img = obj.GetComponent<Image>();
                if (img != null)
                {
                    Debug.Log($"  └─ Image: Enabled={img.enabled}, Color={img.color}, Sprite={img.sprite}");
                }

                // 检查 TextMeshProUGUI 组件
                TextMeshProUGUI tmpText = obj.GetComponent<TextMeshProUGUI>();
                if (tmpText != null)
                {
                    Debug.Log($"  └─ TMP Text: Enabled={tmpText.enabled}, Text='{tmpText.text}', Color={tmpText.color}");
                }

                // 检查 Text 组件
                Text text = obj.GetComponent<Text>();
                if (text != null)
                {
                    Debug.Log($"  └─ Text: Enabled={text.enabled}, Text='{text.text}', Color={text.color}");
                }
            }
        }

        Debug.Log("--- Camera 检测 ---");
        Camera[] cameras = FindObjectsOfType<Camera>();
        Debug.Log($"场景中共有 {cameras.Length} 个摄像机");

        foreach (Camera cam in cameras)
        {
            Debug.Log($"Camera: {cam.name}");
            Debug.Log($"  ├─ Enabled: {cam.enabled}");
            Debug.Log($"  ├─ Culling Mask: {LayerMaskToString(cam.cullingMask)}");
            Debug.Log($"  ├─ Clear Flags: {cam.clearFlags}");
            Debug.Log($"  └─ Depth: {cam.depth}");
        }

        Debug.Log("=== [UIDebugger] End Report ===\n");
    }

    private void CheckChildren(Transform parent, int depth)
    {
        if (depth > 2) return; // 只检查前2层子对象

        string indent = new string(' ', depth * 2);

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            bool isActive = child.gameObject.activeSelf;

            Debug.Log($"{indent}├─ {child.name} (Active: {isActive})");
        }
    }

    private string LayerMaskToString(int mask)
    {
        if (mask == -1)
            return "Everything";

        if (mask == 0)
            return "Nothing";

        return mask.ToString();
    }

    // 手动触发诊断（可在 Inspector 中通过事件调用）
    [ContextMenu("运行 UI 诊断")]
    public void RunDiagnostic()
    {
        Debug.Log("=== [UIDebugger] Manual Diagnostic Triggered ===");
        DiagnoseUI();
    }
}
