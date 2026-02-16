using System;
using UnityEngine;

/// <summary>
/// 战斗属性加成数据（等级成长 + 装备加成 累加用）
/// </summary>
[Serializable]
public struct CombatStatBonus
{
    public int attack;
    public int defense;
    public int hp;
    public int moveRange;
    public int attackRange;

    public static CombatStatBonus operator +(CombatStatBonus a, CombatStatBonus b)
    {
        return new CombatStatBonus
        {
            attack = a.attack + b.attack,
            defense = a.defense + b.defense,
            hp = a.hp + b.hp,
            moveRange = a.moveRange + b.moveRange,
            attackRange = a.attackRange + b.attackRange
        };
    }

    public static CombatStatBonus Zero => new CombatStatBonus();

    public override string ToString()
    {
        return $"ATK+{attack} DEF+{defense} HP+{hp} MOV+{moveRange} RNG+{attackRange}";
    }
}
