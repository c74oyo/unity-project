using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WorksiteRowUI : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text nameTMP;
    public TMP_Text countTMP;
    public TMP_Text statusTMP;
    public Button minusBtn;
    public Button plusBtn;

    private Worksite _ws;

    public void Bind(Worksite ws)
    {
        _ws = ws;

        if (minusBtn) minusBtn.onClick.RemoveAllListeners();
        if (plusBtn) plusBtn.onClick.RemoveAllListeners();

        if (minusBtn) minusBtn.onClick.AddListener(() =>
        {
            if (_ws == null) return;
            int desired = GetDesired(_ws);
            _ws.SetDesired(desired - 1);
            Refresh();
        });

        if (plusBtn) plusBtn.onClick.AddListener(() =>
        {
            if (_ws == null) return;
            int desired = GetDesired(_ws);
            _ws.SetDesired(desired + 1);
            Refresh();
        });

        Refresh();
    }

    public void Refresh()
    {
        if (_ws == null) return;

        if (nameTMP) nameTMP.text = _ws.name;

        int max = GetMaxWorkers(_ws);
        int desired = Mathf.Clamp(GetDesired(_ws), 0, max);

        // present 优先读字段/属性，读不到就用 ClampedDesired/NeededCount 反推
        int present = GetPresentWorkers(_ws, desired);

        if (countTMP) countTMP.text = $"{present}/{desired} (Max {max})";

        string status = "OK";

        bool hasEntrance = GetBoolMember(_ws, "HasEntrance", fallback: true);
        int noPath = GetIntMember(_ws, "noPathCount", "NoPathCount", fallback: 0);
        int needed = GetIntMember(_ws, "NeededCount", fallback: Mathf.Max(0, desired - present));

        if (!hasEntrance) status = "No Entrance";
        else if (noPath > 0) status = $"No Path x{noPath}";
        else if (needed > 0) status = "Waiting Workers";

        if (statusTMP) statusTMP.text = status;

        if (minusBtn) minusBtn.interactable = (desired > 0);
        if (plusBtn) plusBtn.interactable = (desired < max);
    }

    // =========================
    // Robust getters (field/property tolerant)
    // =========================

    private static int GetMaxWorkers(Worksite ws)
    {
        // 优先读 maxWorkers/MaxWorkers
        int max = GetIntMember(ws, "maxWorkers", "MaxWorkers", fallback: -1);
        if (max >= 0) return max;

        // 兜底：如果你 Worksite 里有 public int maxWorkers（默认脚本是有的）
        // 这里再反射一次也能覆盖
        return Mathf.Max(0, max);
    }

    private static int GetDesired(Worksite ws)
    {
        // 优先读 desiredWorkers/DesiredWorkers
        int d = GetIntMember(ws, "desiredWorkers", "DesiredWorkers", fallback: -1);
        if (d >= 0) return d;

        // 兜底：用 ClampedDesired（Worksite 里通常有）
        return GetIntMember(ws, "ClampedDesired", fallback: 0);
    }

    private static int GetPresentWorkers(Worksite ws, int desiredFallback)
    {
        // 优先读 presentWorkers/PresentWorkers
        int p = GetIntMember(ws, "presentWorkers", "PresentWorkers", fallback: -1);
        if (p >= 0) return p;

        // 兜底：present = desired - NeededCount（由 Worksite 公开计算属性提供）
        int needed = GetIntMember(ws, "NeededCount", fallback: 0);
        return Mathf.Max(0, desiredFallback - needed);
    }

    private static int GetIntMember(object obj, string fieldName, string propName = null, int fallback = 0)
    {
        if (obj == null) return fallback;
        var t = obj.GetType();

        // field
        var f = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null && f.FieldType == typeof(int))
            return (int)f.GetValue(obj);

        // prop
        if (!string.IsNullOrEmpty(propName))
        {
            var p = t.GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.PropertyType == typeof(int))
                return (int)p.GetValue(obj);
        }

        // or propName==fieldName scenario
        var p2 = t.GetProperty(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p2 != null && p2.PropertyType == typeof(int))
            return (int)p2.GetValue(obj);

        return fallback;
    }

    private static bool GetBoolMember(object obj, string propOrFieldName, bool fallback = false)
    {
        if (obj == null) return fallback;
        var t = obj.GetType();

        var f = t.GetField(propOrFieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null && f.FieldType == typeof(bool))
            return (bool)f.GetValue(obj);

        var p = t.GetProperty(propOrFieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null && p.PropertyType == typeof(bool))
            return (bool)p.GetValue(obj);

        return fallback;
    }
}
