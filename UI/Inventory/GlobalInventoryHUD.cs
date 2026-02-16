using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GlobalInventoryHUD : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("可以是 GlobalInventory 或 BaseInventory")]
    public MonoBehaviour inventoryComponent;

    // Cached reference
    private GlobalInventory _globalInventory;
    private GlobalInventory inventory
    {
        get
        {
            if (_globalInventory == null && inventoryComponent != null)
                _globalInventory = inventoryComponent as GlobalInventory;
            return _globalInventory;
        }
    }

    // =========================
    // Mode A: Text output (legacy)
    // =========================
    [Header("Text Mode (legacy)")]
    public TMP_Text text; // 仍然支持你原来的大段文本输出

    // =========================
    // Mode B: Resource Bars (recommended)
    // =========================
    [Serializable]
    public class ResourceBarRow
    {
        public ResourceDefinition res;

        [Tooltip("可选：显示资源名")]
        public TMP_Text nameTMP;

        [Tooltip("可选：显示 cur/cap 或 cur")]
        public TMP_Text valueTMP;

        [Tooltip("可选：用 Slider 当进度条（建议把 Handle 隐藏）")]
        public Slider barSlider;

        [Tooltip("可选：用 Image.fillAmount 当进度条（Image Type 要设为 Filled）")]
        public Image barFillImage;

        [Tooltip("可选：资源图标 Image（如果你有 icon 想显示，可手动设置 sprite）")]
        public Image iconImage;

        [Tooltip("可选：手动覆盖显示名（不填则用 res.displayName）")]
        public string overrideName;

        [Tooltip("条形最大值的下限（避免 cap=0 时除 0；也可让它显示为 0%）")]
        public int minCapFallback = 1;
    }

    [Header("Bar Mode (TopBar)")]
    [Tooltip("填了 rows 且 rows.Count>0 时，会优先使用条形模式刷新。")]
    public ResourceBarRow[] rows;

    [Header("Display Order (optional)")]
    public ResourceDefinition[] showOrder; // 不填则按 initialContents 的资源顺序

    [Header("Display")]
    public bool showCapacity = true; // 显示 "cur/cap"
    public bool showPercent = false; // valueTMP 是否附带百分比（例如 90/120 (75%)）

    [Header("Refresh")]
    public float refreshInterval = 0.25f;
    private float _timer;

    private readonly StringBuilder _sb = new();

    private void Awake()
    {
        if (inventoryComponent == null)
            inventoryComponent = GlobalInventory.Instance;
    }

    private void OnEnable()
    {
        // 清除缓存，确保使用最新的 inventoryComponent 引用
        _globalInventory = null;

        if (inventoryComponent == null)
            inventoryComponent = GlobalInventory.Instance;

        // Subscribe to events based on inventory type
        if (inventory != null)
        {
            inventory.OnChanged += HandleChanged;
        }
        else if (inventoryComponent is BaseInventory baseInv)
        {
            baseInv.OnChanged += HandleChangedSimple;
        }

        Refresh();
    }

    private void OnDisable()
    {
        // Unsubscribe from events
        if (inventory != null)
        {
            inventory.OnChanged -= HandleChanged;
        }
        else if (inventoryComponent is BaseInventory baseInv)
        {
            baseInv.OnChanged -= HandleChangedSimple;
        }
    }

    private void Update()
    {
        // 兜底：即使没触发事件也能周期刷新
        _timer += Time.deltaTime;
        if (_timer >= refreshInterval)
        {
            _timer = 0f;
            Refresh();
        }
    }

    private void HandleChanged(ResourceDefinition res, int oldV, int newV)
    {
        Refresh();
    }

    private void HandleChangedSimple()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (inventoryComponent == null)
            inventoryComponent = GlobalInventory.Instance;

        // 优先：条形模式
        if (rows != null && rows.Length > 0)
        {
            RefreshBars();
            return;
        }

        // 退回：文本模式（你原来的逻辑）
        RefreshText();
    }

    // =========================
    // Bar Mode
    // =========================
    private void RefreshBars()
    {
        // Support both GlobalInventory and BaseInventory
        bool isGlobalInventory = inventory != null;
        BaseInventory baseInv = inventoryComponent as BaseInventory;

        if (!isGlobalInventory && baseInv == null) return;

        foreach (var row in rows)
        {
            if (row == null || row.res == null) continue;

            int cur = 0;
            int cap = 0;
            bool hasCapacity = false;

            if (isGlobalInventory)
            {
                // GlobalInventory: has per-resource capacity
                cur = inventory.Get(row.res);
                cap = (showCapacity && inventory.useCapacity) ? inventory.GetCapacity(row.res) : 0;
                hasCapacity = showCapacity && inventory.useCapacity;
            }
            else if (baseInv != null)
            {
                // BaseInventory: only has total capacity, not per-resource
                cur = Mathf.RoundToInt(baseInv.GetAmount(row.res));
                // For BaseInventory, we can't show per-resource capacity
                // We could show total capacity, but that's misleading
                cap = 0;
                hasCapacity = false;
            }

            // Name
            if (row.nameTMP != null)
                row.nameTMP.text = string.IsNullOrWhiteSpace(row.overrideName) ? row.res.displayName : row.overrideName;

            // Value text
            if (row.valueTMP != null)
            {
                if (hasCapacity)
                {
                    int safeCap = Mathf.Max(row.minCapFallback, cap);
                    if (showPercent)
                    {
                        float pct = safeCap > 0 ? (float)cur / safeCap : 0f;
                        row.valueTMP.text = $"{cur}/{cap} ({pct * 100f:0}%)";
                    }
                    else
                    {
                        row.valueTMP.text = $"{cur}/{cap}";
                    }
                }
                else
                {
                    row.valueTMP.text = cur.ToString();
                }
            }

            // Bar fill
            float normalized = 0f;
            if (hasCapacity)
            {
                int safeCap = Mathf.Max(row.minCapFallback, cap);
                normalized = safeCap > 0 ? Mathf.Clamp01((float)cur / safeCap) : 0f;
            }
            else
            {
                // 不显示容量时，进度条没有意义——保持 0 或者你也可以改为按某个固定上限
                normalized = 0f;
            }

            if (row.barSlider != null)
            {
                row.barSlider.minValue = 0f;
                row.barSlider.maxValue = 1f;
                row.barSlider.value = normalized;
            }

            if (row.barFillImage != null)
            {
                // 记得把 Image Type 设为 Filled
                row.barFillImage.fillAmount = normalized;
            }
        }

        // 如果你还留着 legacy text 并不想显示，可以把 text 设为 None
        if (text != null)
            text.text = ""; // 条形模式下默认清空大文本，避免重复显示
    }

    // =========================
    // Text Mode (legacy)
    // =========================
    private void RefreshText()
    {
        if (text == null) return;

        _sb.Clear();
        _sb.AppendLine("Inventory");

        bool isGlobalInventory = inventory != null;
        BaseInventory baseInv = inventoryComponent as BaseInventory;

        if (!isGlobalInventory && baseInv == null)
        {
            _sb.AppendLine("(Inventory not found)");
            text.text = _sb.ToString();
            return;
        }

        if (showOrder != null && showOrder.Length > 0)
        {
            foreach (var res in showOrder)
            {
                if (res == null) continue;
                AppendLine(res);
            }
        }
        else if (isGlobalInventory)
        {
            foreach (var ra in inventory.initialContents)
            {
                if (ra.res == null) continue;
                AppendLine(ra.res);
            }
        }
        else if (baseInv != null)
        {
            // For BaseInventory, show all resources in storage
            var resources = baseInv.GetAllResources();
            foreach (var ra in resources)
            {
                if (ra.res == null) continue;
                AppendLine(ra.res);
            }
        }

        text.text = _sb.ToString();

        void AppendLine(ResourceDefinition res)
        {
            int cur = 0;

            if (isGlobalInventory)
            {
                cur = inventory.Get(res);

                if (showCapacity && inventory.useCapacity)
                {
                    int cap = inventory.GetCapacity(res);
                    _sb.Append(res.displayName).Append(": ").Append(cur).Append("/").Append(cap).AppendLine();
                }
                else
                {
                    _sb.Append(res.displayName).Append(": ").Append(cur).AppendLine();
                }
            }
            else if (baseInv != null)
            {
                cur = Mathf.RoundToInt(baseInv.GetAmount(res));
                // BaseInventory doesn't have per-resource capacity
                _sb.Append(res.displayName).Append(": ").Append(cur).AppendLine();
            }
        }
    }
}