using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 角色详情面板 — 查看属性、升级、装备管理
/// 从 CharacterRosterPanel 点击角色后打开
/// </summary>
public class CharacterDetailPanel : MonoBehaviour
{
    public static CharacterDetailPanel Instance { get; private set; }

    // ============ UI References ============

    [Header("Panel")]
    public GameObject panel;
    public Button closeButton;

    [Header("Character Info")]
    public Image portraitImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI expText;

    [Header("Stats Display")]
    [Tooltip("HP 数值文字")]
    public TextMeshProUGUI hpStatText;
    public TextMeshProUGUI atkStatText;
    public TextMeshProUGUI defStatText;
    public TextMeshProUGUI moveStatText;
    public TextMeshProUGUI atkRangeStatText;

    [Header("Equipment Slots")]
    [Tooltip("武器槽按钮")]
    public Button weaponSlotButton;
    public TextMeshProUGUI weaponSlotText;
    public Image weaponSlotIcon;

    [Tooltip("护甲槽按钮")]
    public Button armorSlotButton;
    public TextMeshProUGUI armorSlotText;
    public Image armorSlotIcon;

    [Header("Equipment Selection Popup")]
    [Tooltip("装备选择弹出面板")]
    public GameObject equipSelectPopup;
    public Transform equipSelectListParent;
    public GameObject equipSelectRowPrefab;
    public Button equipSelectCloseButton;

    [Header("Level Up Section")]
    [Tooltip("经验书选择列表父物体")]
    public Transform expBookListParent;
    public GameObject expBookRowPrefab;

    [Header("Equipment Upgrade")]
    [Tooltip("强化按钮（选中装备后出现）")]
    public Button upgradeWeaponButton;
    public Button upgradeArmorButton;

    // ============ Runtime ============

    private string _currentUnitId;
    private EquipSlot _pendingEquipSlot;
    private readonly List<GameObject> _spawnedRows = new();
    private readonly List<GameObject> _spawnedExpRows = new();
    private readonly List<GameObject> _spawnedEquipSelectRows = new();

    // ============ Lifecycle ============

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (panel != null) panel.SetActive(false);
        if (equipSelectPopup != null) equipSelectPopup.SetActive(false);
    }

    private void Start()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);

        // 自动从 Button 子物体查找 Text / Icon（防止 Inspector 拖反）
        AutoResolveSlotUI(weaponSlotButton, ref weaponSlotText, ref weaponSlotIcon);
        AutoResolveSlotUI(armorSlotButton, ref armorSlotText, ref armorSlotIcon);

        if (weaponSlotButton != null)
            weaponSlotButton.onClick.AddListener(() => OpenEquipSelect(EquipSlot.Weapon));
        if (armorSlotButton != null)
            armorSlotButton.onClick.AddListener(() => OpenEquipSelect(EquipSlot.Armor));

        if (equipSelectCloseButton != null)
            equipSelectCloseButton.onClick.AddListener(CloseEquipSelect);

        if (upgradeWeaponButton != null)
            upgradeWeaponButton.onClick.AddListener(() => TryUpgradeSlot(EquipSlot.Weapon));
        if (upgradeArmorButton != null)
            upgradeArmorButton.onClick.AddListener(() => TryUpgradeSlot(EquipSlot.Armor));
    }

    /// <summary>
    /// 自动从槽位按钮的子物体查找 Text 和 Icon，避免 Inspector 拖错
    /// 如果 Inspector 已经正确设置则不覆盖
    /// </summary>
    private void AutoResolveSlotUI(Button slotBtn, ref TextMeshProUGUI slotText, ref Image slotIcon)
    {
        if (slotBtn == null) return;

        // 优先用 Inspector 里已设置的，但检查是否真的属于这个 Button 的子物体
        if (slotText != null && !slotText.transform.IsChildOf(slotBtn.transform))
        {
            Debug.LogWarning($"[DetailPanel] slotText '{slotText.name}' is NOT a child of '{slotBtn.name}', auto-resolving...");
            slotText = null;
        }
        if (slotIcon != null && !slotIcon.transform.IsChildOf(slotBtn.transform))
        {
            Debug.LogWarning($"[DetailPanel] slotIcon '{slotIcon.name}' is NOT a child of '{slotBtn.name}', auto-resolving...");
            slotIcon = null;
        }

        // 自动查找子物体中的 Text 和 Icon
        if (slotText == null)
            slotText = slotBtn.GetComponentInChildren<TextMeshProUGUI>();
        if (slotIcon == null)
        {
            // 跳过 Button 自身的 Image，找子物体中的
            foreach (var img in slotBtn.GetComponentsInChildren<Image>())
            {
                if (img.gameObject != slotBtn.gameObject)
                {
                    slotIcon = img;
                    break;
                }
            }
        }
    }

    // ============ Public API ============

    public void Open(string unitId)
    {
        _currentUnitId = unitId;
        if (panel != null) panel.SetActive(true);
        CloseEquipSelect();
        Refresh();
    }

    public void Close()
    {
        ClearAllRows();
        if (panel != null) panel.SetActive(false);
        if (equipSelectPopup != null) equipSelectPopup.SetActive(false);
    }

    // ============ Refresh ============

    public void Refresh()
    {
        if (string.IsNullOrEmpty(_currentUnitId)) return;

        var prog = CharacterProgressionManager.Instance;
        if (prog == null) return;

        var unitDef = prog.GetUnitDef(_currentUnitId);
        int level = prog.GetLevel(_currentUnitId);
        int totalExp = prog.GetTotalExp(_currentUnitId);
        int expNeeded = prog.GetExpForNextLevel(_currentUnitId);

        // ---- 基本信息 ----
        if (nameText != null)
            nameText.text = unitDef != null ? unitDef.unitName : _currentUnitId;

        if (levelText != null)
            levelText.text = $"Lv.{level}";

        if (expText != null)
        {
            if (level >= prog.maxLevel)
                expText.text = "MAX LEVEL";
            else
                expText.text = $"EXP: {totalExp} / {expNeeded}";
        }

        if (portraitImage != null)
        {
            if (unitDef != null && unitDef.portrait != null)
            {
                portraitImage.sprite = unitDef.portrait;
                portraitImage.enabled = true;
            }
            else
            {
                portraitImage.enabled = false;
            }
        }

        // ---- 属性计算展示 ----
        RefreshStats(unitDef, prog);

        // ---- 装备槽位 ----
        RefreshEquipSlots();

        // ---- 经验书列表 ----
        RefreshExpBooks();
    }

    // ============ Stats Display ============

    private void RefreshStats(CharacterCard unitDef, CharacterProgressionManager prog)
    {
        if (unitDef == null) return;

        int level = prog.GetLevel(_currentUnitId);
        CombatStatBonus totalBonus = prog.GetTotalBonus(_currentUnitId);

        // 等级加成部分
        int levelBonusHP = prog.hpPerLevel * (level - 1);
        int levelBonusATK = prog.attackPerLevel * (level - 1);
        int levelBonusDEF = prog.defensePerLevel * (level - 1);

        // 装备加成 = 总加成 - 等级加成
        int equipBonusHP = totalBonus.hp - levelBonusHP;
        int equipBonusATK = totalBonus.attack - levelBonusATK;
        int equipBonusDEF = totalBonus.defense - levelBonusDEF;
        int equipBonusMOV = totalBonus.moveRange;
        int equipBonusRNG = totalBonus.attackRange;

        // 最终值
        int finalHP = unitDef.maxHP + totalBonus.hp;
        int finalATK = unitDef.attack + totalBonus.attack;
        int finalDEF = unitDef.defense + totalBonus.defense;
        int finalMOV = unitDef.moveRange + totalBonus.moveRange;
        int finalRNG = unitDef.attackRange + totalBonus.attackRange;

        // 格式: "Base 50 + Lv +20 + Equip +10 = 80"
        if (hpStatText != null)
            hpStatText.text = FormatStat("HP", unitDef.maxHP, levelBonusHP, equipBonusHP, finalHP);

        if (atkStatText != null)
            atkStatText.text = FormatStat("ATK", unitDef.attack, levelBonusATK, equipBonusATK, finalATK);

        if (defStatText != null)
            defStatText.text = FormatStat("DEF", unitDef.defense, levelBonusDEF, equipBonusDEF, finalDEF);

        if (moveStatText != null)
            moveStatText.text = FormatStat("MOV", unitDef.moveRange, 0, equipBonusMOV, finalMOV);

        if (atkRangeStatText != null)
            atkRangeStatText.text = FormatStat("RNG", unitDef.attackRange, 0, equipBonusRNG, finalRNG);
    }

    private string FormatStat(string label, int baseVal, int levelBonus, int equipBonus, int total)
    {
        string result = $"{label}: <color=#CCCCCC>{baseVal}</color>";
        if (levelBonus > 0)
            result += $" <color=#88FF88>+{levelBonus}</color>";
        if (equipBonus > 0)
            result += $" <color=#88BBFF>+{equipBonus}</color>";
        result += $" = <color=#FFFFFF>{total}</color>";
        return result;
    }

    // ============ Equipment Slots ============

    private void RefreshEquipSlots()
    {
        var inv = PlayerInventoryManager.Instance;
        if (inv == null)
        {
            Debug.LogWarning("[DetailPanel] PlayerInventoryManager.Instance is null, cannot show equipment");
            return;
        }

        var equipped = inv.GetEquippedBy(_currentUnitId);
        Debug.Log($"[DetailPanel] RefreshEquipSlots for '{_currentUnitId}': found {equipped.Count} equipped item(s)");

        // 武器槽
        OwnedEquipmentSaveData weaponEquip = null;
        OwnedEquipmentSaveData armorEquip = null;

        foreach (var e in equipped)
        {
            var def = inv.GetEquipDef(e.equipDefId);
            if (def == null) continue;
            if (def.slot == EquipSlot.Weapon) weaponEquip = e;
            if (def.slot == EquipSlot.Armor) armorEquip = e;
        }

        // 更新武器槽显示
        UpdateSlotDisplay(weaponEquip, weaponSlotText, weaponSlotIcon, upgradeWeaponButton);
        // 更新护甲槽显示
        UpdateSlotDisplay(armorEquip, armorSlotText, armorSlotIcon, upgradeArmorButton);
    }

    private void UpdateSlotDisplay(OwnedEquipmentSaveData equip,
        TextMeshProUGUI slotText, Image slotIcon, Button upgradeBtn)
    {
        var inv = PlayerInventoryManager.Instance;

        if (equip != null && inv != null)
        {
            var def = inv.GetEquipDef(equip.equipDefId);
            if (slotText != null)
            {
                string lvStr = equip.level > 0 ? $" +{equip.level}" : "";
                slotText.text = def != null ? $"{def.displayName}{lvStr}" : equip.equipDefId;
            }
            if (slotIcon != null)
            {
                if (def != null && def.icon != null)
                {
                    slotIcon.sprite = def.icon;
                    slotIcon.enabled = true;
                }
                else
                {
                    slotIcon.enabled = false;
                }
            }
            if (upgradeBtn != null)
            {
                // 满级隐藏强化按钮
                bool canUpgrade = def != null && equip.level < def.maxLevel;
                upgradeBtn.gameObject.SetActive(canUpgrade);
            }
        }
        else
        {
            if (slotText != null) slotText.text = "-- Empty --";
            if (slotIcon != null) slotIcon.enabled = false;
            if (upgradeBtn != null) upgradeBtn.gameObject.SetActive(false);
        }
    }

    // ============ Equipment Selection Popup ============

    private void OpenEquipSelect(EquipSlot slot)
    {
        _pendingEquipSlot = slot;
        if (equipSelectPopup != null) equipSelectPopup.SetActive(true);
        RefreshEquipSelectList();
    }

    private void CloseEquipSelect()
    {
        ClearEquipSelectRows();
        if (equipSelectPopup != null) equipSelectPopup.SetActive(false);
    }

    private void RefreshEquipSelectList()
    {
        ClearEquipSelectRows();
        var inv = PlayerInventoryManager.Instance;
        if (inv == null) return;

        // 添加"卸下装备"选项
        if (equipSelectRowPrefab != null && equipSelectListParent != null)
        {
            // 检查是否有装备需要卸下
            var equipped = inv.GetEquippedBy(_currentUnitId);
            bool hasEquippedInSlot = false;
            string equippedInstanceId = "";
            foreach (var e in equipped)
            {
                var eDef = inv.GetEquipDef(e.equipDefId);
                if (eDef != null && eDef.slot == _pendingEquipSlot)
                {
                    hasEquippedInSlot = true;
                    equippedInstanceId = e.instanceId;
                    break;
                }
            }

            if (hasEquippedInSlot)
            {
                var unequipRow = Instantiate(equipSelectRowPrefab, equipSelectListParent);
                _spawnedEquipSelectRows.Add(unequipRow);

                var nameT = unequipRow.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
                if (nameT != null) nameT.text = "<color=#FF8888>Unequip</color>";

                var detailT = unequipRow.transform.Find("DetailText")?.GetComponent<TextMeshProUGUI>();
                if (detailT != null) detailT.text = "Remove current equipment";

                var btn = unequipRow.GetComponent<Button>();
                string capturedId = equippedInstanceId;
                if (btn != null)
                {
                    btn.onClick.AddListener(() =>
                    {
                        CharacterProgressionManager.Instance?.UnequipItem(capturedId);
                        CloseEquipSelect();
                        Refresh();
                    });
                }
            }
        }

        // 列出所有未装备的、同槽位的装备
        var allEquips = inv.GetAllEquipment();
        foreach (var equip in allEquips)
        {
            // 跳过已装备给其他角色或当前角色的
            if (!string.IsNullOrEmpty(equip.equippedToUnitId)) continue;

            var def = inv.GetEquipDef(equip.equipDefId);
            if (def == null || def.slot != _pendingEquipSlot) continue;

            if (equipSelectRowPrefab == null || equipSelectListParent == null) break;

            var row = Instantiate(equipSelectRowPrefab, equipSelectListParent);
            _spawnedEquipSelectRows.Add(row);

            var nameText2 = row.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            var detailText2 = row.transform.Find("DetailText")?.GetComponent<TextMeshProUGUI>();
            var iconImg = row.transform.Find("Icon")?.GetComponent<Image>();

            string lvStr = equip.level > 0 ? $" +{equip.level}" : "";
            if (nameText2 != null)
                nameText2.text = $"{def.displayName}{lvStr}";

            if (detailText2 != null)
            {
                var bonus = def.GetBonusAtLevel(equip.level);
                var parts = new List<string>();
                if (bonus.attack > 0) parts.Add($"ATK+{bonus.attack}");
                if (bonus.defense > 0) parts.Add($"DEF+{bonus.defense}");
                if (bonus.hp > 0) parts.Add($"HP+{bonus.hp}");
                if (bonus.moveRange > 0) parts.Add($"MOV+{bonus.moveRange}");
                if (bonus.attackRange > 0) parts.Add($"RNG+{bonus.attackRange}");
                detailText2.text = string.Join("  ", parts);
            }

            if (iconImg != null && def.icon != null)
            {
                iconImg.sprite = def.icon;
                iconImg.enabled = true;
            }

            // 点击装备
            var selectBtn = row.GetComponent<Button>();
            string capturedInstanceId = equip.instanceId;
            if (selectBtn != null)
            {
                selectBtn.onClick.AddListener(() =>
                {
                    CharacterProgressionManager.Instance?.EquipItem(_currentUnitId, capturedInstanceId);
                    CloseEquipSelect();
                    Refresh();
                });
            }
        }
    }

    // ============ Level Up (Exp Books) ============

    private void RefreshExpBooks()
    {
        ClearExpBookRows();
        var inv = PlayerInventoryManager.Instance;
        var prog = CharacterProgressionManager.Instance;
        if (inv == null || prog == null) return;
        if (prog.GetLevel(_currentUnitId) >= prog.maxLevel) return;

        var allItems = inv.GetAllItems();
        foreach (var kvp in allItems)
        {
            var itemDef = inv.GetItemDef(kvp.Key);
            if (itemDef == null || itemDef.itemType != ItemType.ExpBook) continue;
            if (kvp.Value <= 0) continue;

            if (expBookRowPrefab == null || expBookListParent == null) break;

            var row = Instantiate(expBookRowPrefab, expBookListParent);
            _spawnedExpRows.Add(row);

            var nameT = row.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            var countT = row.transform.Find("CountText")?.GetComponent<TextMeshProUGUI>();
            var effectT = row.transform.Find("EffectText")?.GetComponent<TextMeshProUGUI>();
            var iconImg = row.transform.Find("Icon")?.GetComponent<Image>();
            var useBtn = row.transform.Find("UseButton")?.GetComponent<Button>();

            if (nameT != null)
            {
                nameT.text = itemDef.displayName;
                nameT.color = itemDef.GetRarityColor();
            }
            if (countT != null)
                countT.text = $"x{kvp.Value}";
            if (effectT != null)
                effectT.text = $"+{itemDef.effectValue} EXP";
            if (iconImg != null && itemDef.icon != null)
                iconImg.sprite = itemDef.icon;

            string capturedItemId = kvp.Key;
            if (useBtn != null)
            {
                useBtn.onClick.AddListener(() =>
                {
                    bool success = prog.UseExpItem(_currentUnitId, capturedItemId);
                    if (success)
                        Refresh();
                });
            }
        }
    }

    // ============ Equipment Upgrade ============

    private void TryUpgradeSlot(EquipSlot slot)
    {
        var inv = PlayerInventoryManager.Instance;
        if (inv == null) return;

        var equipped = inv.GetEquippedBy(_currentUnitId);
        OwnedEquipmentSaveData targetEquip = null;

        foreach (var e in equipped)
        {
            var def = inv.GetEquipDef(e.equipDefId);
            if (def != null && def.slot == slot)
            {
                targetEquip = e;
                break;
            }
        }

        if (targetEquip == null) return;

        // 尝试使用第一种可用的强化石
        var allItems = inv.GetAllItems();
        foreach (var kvp in allItems)
        {
            var itemDef = inv.GetItemDef(kvp.Key);
            if (itemDef != null && itemDef.itemType == ItemType.StrengthenStone && kvp.Value > 0)
            {
                bool success = inv.TryUpgradeEquipment(targetEquip.instanceId, kvp.Key);
                if (success)
                {
                    Debug.Log($"[Detail] Upgraded {targetEquip.equipDefId} with {kvp.Key}");
                    Refresh();
                    return;
                }
            }
        }

        Debug.Log("[Detail] No strengthen stones available or upgrade failed");
    }

    // ============ Cleanup ============

    private void ClearAllRows()
    {
        ClearExpBookRows();
        ClearEquipSelectRows();
        foreach (var go in _spawnedRows)
            if (go != null) Destroy(go);
        _spawnedRows.Clear();
    }

    private void ClearExpBookRows()
    {
        foreach (var go in _spawnedExpRows)
            if (go != null) Destroy(go);
        _spawnedExpRows.Clear();
    }

    private void ClearEquipSelectRows()
    {
        foreach (var go in _spawnedEquipSelectRows)
            if (go != null) Destroy(go);
        _spawnedEquipSelectRows.Clear();
    }
}
