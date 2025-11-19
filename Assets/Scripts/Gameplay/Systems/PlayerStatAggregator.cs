using Gameplay.Equipment;
using UnityEngine;

public class PlayerStatAggregator : MonoBehaviour
{
    [SerializeField] private EquipmentSlots equipment;
    [SerializeField] private CombatOrchestrator orchestrator;

    void Awake()
    {
        if (!equipment) equipment = FindObjectOfType<EquipmentSlots>();
        if (!orchestrator) orchestrator = FindObjectOfType<CombatOrchestrator>();
    }

    void OnEnable()
    {
        if (equipment != null)
            equipment.OnEquipmentChanged += Recalculate;
        Recalculate();
    }

    void OnDisable()
    {
        if (equipment != null)
            equipment.OnEquipmentChanged -= Recalculate;
    }

    void Recalculate()
    {
        if (equipment == null || orchestrator == null) return;

        var stats = new Gameplay.Systems.PlayerCombatStats();

        foreach (var gear in equipment.AllEquipped())
        {
            if (gear?.Substats == null) continue;
            foreach (var roll in gear.Substats)
            {
                float percent = roll.value * 0.01f;
                switch (roll.type)
                {
                    case GearSubstatType.AttackSpeed:
                        stats.attackSpeedAA += percent;
                        break;
                    case GearSubstatType.CritChance:
                        stats.critChanceAA += percent;
                        break;
                    case GearSubstatType.SkillCritChance:
                        stats.skillCritChance += percent;
                        break;
                    case GearSubstatType.SkillCritDamage:
                        stats.skillCritDamage += percent;
                        break;
                    case GearSubstatType.DotDamage:
                        stats.dotDamagePct += percent;
                        break;
                    case GearSubstatType.BossDamage:
                        stats.damageVsBossPct += percent;
                        break;
                    case GearSubstatType.Thorns:
                        stats.thornsPct += percent;
                        break;
                    case GearSubstatType.Evasion:
                        stats.evasionRating += roll.value;
                        break;
                    case GearSubstatType.Accuracy:
                        stats.accuracyRating += roll.value;
                        break;
                    case GearSubstatType.Regeneration:
                        stats.regenPct += percent;
                        break;
                    case GearSubstatType.ShieldPower:
                        stats.shieldPowerPct += percent;
                        break;
                }
            }
        }

        orchestrator.ApplyCombatStats(stats);
    }
}
