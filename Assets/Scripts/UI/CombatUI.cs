using UnityEngine;
using UnityEngine.UI;

public class CombatUI : MonoBehaviour
{
    public Slider PlayerHPBar;
    public Slider PlayerXPBar;
    public Slider EnemyHPBar;

    private GameLoopService game;

    void Start()
    {
        game = FindObjectOfType<GameLoopService>();

        // Init defaults
        PlayerHPBar.value = 1f;
        PlayerXPBar.value = 0f;
        EnemyHPBar.value = 1f;
    }

    void Update()
    {
        if (game == null || game.Player == null || game.Enemy == null) return;

        // Player HP bar
        PlayerHPBar.maxValue = game.Player.MaxHp;
        PlayerHPBar.value = game.Player.Hp;

        // XP bar
        int xpToNext = game.XpTable.GetXpToNextLevel(game.Player.Level);
        PlayerXPBar.maxValue = xpToNext;
        PlayerXPBar.value = game.Player.CurrentXp;

        // Enemy HP bar
        EnemyHPBar.maxValue = game.Enemy.HpMax;
        EnemyHPBar.value = game.Enemy.Hp;
    }
}
