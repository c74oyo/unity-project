using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 道具商店管理器 — 使用金钱购买养成道具和装备
/// 独立于NPC贸易系统，即买即得（不需要运输）
/// </summary>
public class ItemShopManager : MonoBehaviour
{
    public static ItemShopManager Instance { get; private set; }

    // ============ Shop Config ============

    [Header("道具商品列表（Inspector 配置）")]
    public List<ShopItemListing> itemListings = new();

    [Header("装备商品列表（Inspector 配置）")]
    public List<ShopEquipListing> equipListings = new();

    // ============ Events ============

    public event Action OnPurchaseCompleted;

    // ============ Lifecycle ============

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ============ 购买操作 ============

    /// <summary>
    /// 购买道具
    /// </summary>
    /// <param name="itemId">道具ID</param>
    /// <param name="quantity">购买数量</param>
    /// <param name="payingBaseId">付款基地ID</param>
    /// <returns>是否购买成功</returns>
    public bool TryBuyItem(string itemId, int quantity, string payingBaseId)
    {
        if (quantity <= 0) return false;

        // 查找商品
        var listing = itemListings.Find(l => l.itemDef != null && l.itemDef.itemId == itemId);
        if (listing == null)
        {
            Debug.LogWarning($"[Shop] Item {itemId} not found in shop listings");
            return false;
        }

        // 检查购买上限
        if (listing.maxPerPurchase > 0 && quantity > listing.maxPerPurchase)
        {
            Debug.LogWarning($"[Shop] Exceeds max per purchase: {quantity} > {listing.maxPerPurchase}");
            return false;
        }

        float totalCost = listing.price * quantity;

        // 扣钱
        if (!TryDeductMoney(payingBaseId, totalCost))
        {
            Debug.LogWarning($"[Shop] Insufficient funds: need {totalCost}");
            return false;
        }

        // 发放道具
        PlayerInventoryManager.Instance?.AddItem(itemId, quantity);

        OnPurchaseCompleted?.Invoke();
        Debug.Log($"[Shop] Purchased {quantity}x {itemId} for {totalCost} gold");
        return true;
    }

    /// <summary>
    /// 购买装备（每次只买一件）
    /// </summary>
    public bool TryBuyEquipment(string equipDefId, string payingBaseId)
    {
        var listing = equipListings.Find(l => l.equipDef != null && l.equipDef.equipId == equipDefId);
        if (listing == null)
        {
            Debug.LogWarning($"[Shop] Equipment {equipDefId} not found in shop listings");
            return false;
        }

        if (!TryDeductMoney(payingBaseId, listing.price))
        {
            Debug.LogWarning($"[Shop] Insufficient funds: need {listing.price}");
            return false;
        }

        PlayerInventoryManager.Instance?.AddEquipment(equipDefId);

        OnPurchaseCompleted?.Invoke();
        Debug.Log($"[Shop] Purchased equipment: {equipDefId} for {listing.price} gold");
        return true;
    }

    // ============ 查询 ============

    /// <summary>
    /// 获取所有基地的金钱总和（玩家全局财富）
    /// </summary>
    public float GetTotalMoney()
    {
        if (BaseManager.Instance == null) return 0f;
        float total = 0f;
        foreach (var b in BaseManager.Instance.AllBaseSaveData)
            total += b.money;
        return total;
    }

    // ============ 内部方法 ============

    /// <summary>
    /// 从所有基地中扣除金钱（优先从金钱最多的基地扣）
    /// </summary>
    private bool TryDeductMoney(string baseId, float amount)
    {
        if (BaseManager.Instance == null) return false;

        // 检查总余额
        float total = GetTotalMoney();
        if (total < amount) return false;

        // 按金钱从多到少排序，依次扣除
        float remaining = amount;
        var allBases = BaseManager.Instance.AllBaseSaveData;
        allBases.Sort((a, b) => b.money.CompareTo(a.money));

        foreach (var baseSave in allBases)
        {
            if (remaining <= 0f) break;
            if (baseSave.money <= 0f) continue;

            float deduct = Mathf.Min(baseSave.money, remaining);
            baseSave.money -= deduct;
            remaining -= deduct;
            BaseManager.Instance.UpdateBaseSaveData(baseSave);
        }

        return true;
    }
}

// ============ 商品配置类 ============

/// <summary>
/// 道具商品条目
/// </summary>
[Serializable]
public class ShopItemListing
{
    [Tooltip("道具定义")]
    public ItemDefinition itemDef;

    [Tooltip("售价")]
    public float price;

    [Tooltip("每次最大购买数量（0=无限）")]
    public int maxPerPurchase;
}

/// <summary>
/// 装备商品条目
/// </summary>
[Serializable]
public class ShopEquipListing
{
    [Tooltip("装备定义")]
    public EquipmentDefinition equipDef;

    [Tooltip("售价")]
    public float price;
}
