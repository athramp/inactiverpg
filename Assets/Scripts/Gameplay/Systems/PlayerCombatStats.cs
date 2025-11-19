using System;

namespace Gameplay.Systems
{
    [Serializable]
    public struct PlayerCombatStats
    {
        public float attackSpeedAA;   // additive percent (0.1 = +10%)
        public float critChanceAA;
        public float skillCritChance;
        public float skillCritDamage;
        public float dotDamagePct;
        public float damageVsBossPct;
        public float thornsPct;
        public float evasionRating;
        public float accuracyRating;
        public float regenPct;
        public float shieldPowerPct;

        public static PlayerCombatStats operator +(PlayerCombatStats a, PlayerCombatStats b)
        {
            return new PlayerCombatStats
            {
                attackSpeedAA = a.attackSpeedAA + b.attackSpeedAA,
                critChanceAA = a.critChanceAA + b.critChanceAA,
                skillCritChance = a.skillCritChance + b.skillCritChance,
                skillCritDamage = a.skillCritDamage + b.skillCritDamage,
                dotDamagePct = a.dotDamagePct + b.dotDamagePct,
                damageVsBossPct = a.damageVsBossPct + b.damageVsBossPct,
                thornsPct = a.thornsPct + b.thornsPct,
                evasionRating = a.evasionRating + b.evasionRating,
                accuracyRating = a.accuracyRating + b.accuracyRating,
                regenPct = a.regenPct + b.regenPct,
                shieldPowerPct = a.shieldPowerPct + b.shieldPowerPct
            };
        }
    }
}
