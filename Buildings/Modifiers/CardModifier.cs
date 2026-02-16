using System;
using UnityEngine;

[Serializable]
public class CardModifier
{
    public ModifierType type = ModifierType.Producer_SpeedMul;

    [Tooltip("乘法型加成用“百分比”，例如 +0.25 表示 *1.25；-0.10 表示 *0.90")]
    public float value = 0.0f;

    public enum ModifierType
    {
        Producer_SpeedMul,      // 生产速度倍率
        Producer_InputMul,      // 输入消耗倍率（越小越省）
        Power_RadiusMul,        // 发电覆盖半径倍率（后续接电厂）
        Dock_LoadSpeedMul,      // 装卸速度倍率（后续接 DockYard）
    }
}
