using UnityEngine;
using UnityEngine.UI;
using Core.Combat;
using TMPro;

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

            string s = $"PLAYER\n" +
                       $"Lv {playerProgression.Level}\n";

            if (eng != null)
            {
                s += $"HP {eng.Player.Hp}/{eng.Player.MaxHp}\n" +
                     $"ATK {eng.Player.Atk}  DEF {eng.Player.Def}\n";
            }

            s += $"XP {xpInto}/{xpNext}";
            playerText.text = s;
        }
    }
}
