using UnityEngine;
using UnityEngine.UI;
using Core.Combat;

public class CombatDebugPanel : MonoBehaviour
{
    [Header("UI References")]
    public Text playerText;
    public Text enemyText;
    public CombatOrchestrator orchestrator;

    void Update()
    {
        if (!orchestrator) return;
        var eng = orchestrator.DebugEngine;
        if (eng == null) return;

        playerText.text = $"PLAYER\n" +
                          $"Lv {eng.Player.Level}\n" +
                          $"HP {eng.Player.Hp}/{eng.Player.MaxHp}\n" +
                          $"ATK {eng.Player.Atk}  DEF {eng.Player.Def}";

        enemyText.text = $"ENEMY\n" +
                         $"Lv {eng.Enemy.Level}\n" +
                         $"HP {eng.Enemy.Hp}/{eng.Enemy.MaxHp}\n" +
                         $"ATK {eng.Enemy.Atk}  DEF {eng.Enemy.Def}";
    }
}
