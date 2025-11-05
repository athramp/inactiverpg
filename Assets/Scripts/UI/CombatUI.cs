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
        

        // Init defaults
        PlayerHPBar.value = 1f;
        PlayerXPBar.value = 0f;
        EnemyHPBar.value = 1f;
    }

    void Update()
    {
           var eng = orchestrator.DebugEngine;
        if (orchestrator == null || orchestrator.DebugEngine == null ) return;

        // Player HP bar
        PlayerHPBar.maxValue = eng.Player.MaxHp;
        PlayerHPBar.value = eng.Player.Hp;

        // XP bar
        int xpToNext = game.XpTable.GetXpToNextLevel(eng.Player.Level);
        PlayerXPBar.maxValue = xpToNext;
        PlayerXPBar.value = playerProgression.XpIntoLevel;

        // Enemy HP bar
        EnemyHPBar.maxValue = eng.Enemy.MaxHp;
        EnemyHPBar.value = eng.Enemy.Hp;
    }
}
