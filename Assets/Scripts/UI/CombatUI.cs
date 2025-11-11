using UnityEngine;
using UnityEngine.UI;

public class CombatUI : MonoBehaviour
{
    public Slider PlayerHPBar;
    public Slider PlayerXPBar;
    public Slider EnemyHPBar;
    public CombatOrchestrator orchestrator;
    [SerializeField] PlayerProgression playerProgression;
    [SerializeField] GameLoopService game;

    void Start()
    {
        PlayerHPBar.value = 1f;
        PlayerXPBar.value = 0f;
        EnemyHPBar.value = 1f;
    }

    void Update()
    {
        if (orchestrator == null || orchestrator.DebugEngine == null) return;

        var eng = orchestrator.DebugEngine;

        // Player HP bar
        PlayerHPBar.maxValue = eng.Player.MaxHp;
        PlayerHPBar.value    = eng.Player.Hp;

        // XP bar
        int xpToNext = game.XpTable.GetXpToNextLevel(eng.Player.Level);
        PlayerXPBar.maxValue = xpToNext;
        PlayerXPBar.value    = playerProgression.XpIntoLevel;

        // Enemy HP bar → current target (multi-enemy)
        var target = orchestrator.CurrentTarget;
        if (target != null)
        {
            EnemyHPBar.maxValue = Mathf.Max(1, target.maxHp);
            EnemyHPBar.value    = Mathf.Clamp(target.hp, 0, target.maxHp);
        }
        else
        {
            EnemyHPBar.maxValue = 1f;
            EnemyHPBar.value    = 0f;
        }
    }
}
