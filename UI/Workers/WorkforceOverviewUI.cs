using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WorkforceOverviewUI : MonoBehaviour
{
    [Header("UI Refs")]
    public Transform content;        // ScrollView/Viewport/Content
    public WorksiteRowUI rowPrefab;  // 行 prefab

    [Header("Refresh")]
    public float refreshInterval = 0.5f;
    private float _timer;

    private readonly Dictionary<Worksite, WorksiteRowUI> _rows = new();

    private void Start()
    {
        Rebuild();
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < refreshInterval) return;
        _timer = 0f;

        Refresh();
    }

    public void Rebuild()
    {
        // 清空旧
        foreach (var kv in _rows)
            if (kv.Value != null) Destroy(kv.Value.gameObject);
        _rows.Clear();

#if UNITY_2023_1_OR_NEWER
        var worksites = FindObjectsByType<Worksite>(FindObjectsSortMode.None);
#else
        var worksites = FindObjectsOfType<Worksite>();
#endif
        for (int i = 0; i < worksites.Length; i++)
        {
            var ws = worksites[i];
            if (ws == null) continue;

            var row = Instantiate(rowPrefab, content);
            row.Bind(ws);
            _rows[ws] = row;
        }
    }

    private void Refresh()
    {
        // Worksite 数量变化时，你可以手动点 Rebuild；阶段1先简化
        foreach (var kv in _rows)
        {
            if (kv.Key == null || kv.Value == null) continue;
            kv.Value.Refresh();
        }
    }
}
