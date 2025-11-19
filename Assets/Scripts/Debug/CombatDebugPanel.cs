using UnityEngine;
using UnityEngine.UI;
using Core.Combat;
using TMPro;
using Gameplay.Systems;

public class CombatDebugPanel : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text playerText;
    public TMP_Text enemyText;
    public CombatOrchestrator orchestrator;
    [SerializeField] private PlayerProgression playerProgression;

    void Awake()
    {
        if (!playerProgression)
            playerProgression = FindObjectOfType<PlayerProgression>();
    }

    void Update()
    {
        if (!orchestrator) return;
        var eng  = orchestrator.DebugEngine;

        // ---- Enemy (current target from orchestrator) ----
        if (enemyText)
        {
            var u = orchestrator.CurrentTarget;
            if (u != null)
            {
                string name = u.def ? u.def.monsterId : $"Enemy#{u.enemyId}";
                enemyText.text =
                    $"ENEMY\n" +
                    $"{name}\n" +
                    $"HP {u.hp}/{u.maxHp}\n" +
                    $"ATK {u.atk}  DEF {u.defStat}\n" +
                    $"X {u.posX:0.00}";
            }
            else
            {
                enemyText.text = "ENEMY\nNone";
            }
        }

        // ---- Player (progression + engine stats) ----
        if (playerText && playerProgression != null)
        {
            int xpInto = playerProgression.XpIntoLevel;
            int xpNext = playerProgression.XpToNextLevel;
            PlayerCombatStats stats = orchestrator.CombatStats;

            string s = $"PLAYER\n" +
                       $"Lv {playerProgression.Level}\n";

            if (eng != null)
            {
                s += $"HP {eng.Player.Hp}/{eng.Player.MaxHp}\n" +
                     $"ATK {eng.Player.Atk}  DEF {eng.Player.Def}\n";
            }

            s += $"AA Spd {stats.attackSpeedAA * 100f:+0;-0;0}%  Crit {stats.critChanceAA * 100f:+0;-0;0}%\n" +
                 $"Skill Crit {stats.skillCritChance * 100f:+0;-0;0}%  CritDmg {stats.skillCritDamage * 100f:+0;-0;0}%\n" +
                 $"DOT {stats.dotDamagePct * 100f:+0;-0;0}%  Boss {stats.damageVsBossPct * 100f:+0;-0;0}%\n" +
                 $"Thorns {stats.thornsPct * 100f:+0;-0;0}%  Regen {stats.regenPct * 100f:+0;-0;0}%\n" +
                 $"Acc {stats.accuracyRating:0.##}  Eva {stats.evasionRating:0.##}";
            s += $"\nXP {xpInto}/{xpNext}";
            playerText.text = s;
        }
    }
}
