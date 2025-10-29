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

    void Update()
{
    if (!orchestrator) return;
    var eng  = orchestrator.DebugEngine;
    var loop = orchestrator.gameLoop;
    var prog = FindObjectOfType<PlayerProgression>();

    // Enemy from engine is fine
    if (eng != null && enemyText)
    {
        enemyText.text = $"ENEMY\n" +
                         $"Lv {eng.Enemy.Level}\n" +
                         $"HP {eng.Enemy.Hp}/{eng.Enemy.MaxHp}\n" +
                         $"ATK {eng.Enemy.Atk}  DEF {eng.Enemy.Def}";
    }

    // Player: show PROGRESSION as source of truth
    if (playerText && prog != null && loop != null && loop.XpTable != null)
    {
        int xpInto = prog.XpIntoLevel;
        int xpNext = loop.XpTable.GetXpToNextLevel(prog.Level);

        // Optional: also show runtime combat stats from GameLoopService
        var ps = loop.Player;
        playerText.text = $"PLAYER\n" +
                          $"Lv {prog.Level}\n" +
                          $"HP {ps.Hp}/{ps.MaxHp}\n" +
                          $"ATK {ps.Atk}  DEF {ps.Def}\n" +
                          $"XP {xpInto}/{xpNext}";
            if (eng != null)
    {
        playerText.text +=
            $"\n\nPLAYER (engine)\n" +
            $"Lv {eng.Player.Level}\n" +
            $"HP {eng.Player.Hp}/{eng.Player.MaxHp}\n" +
            $"ATK {eng.Player.Atk}  DEF {eng.Player.Def}";
    }                  
    }
}

}
