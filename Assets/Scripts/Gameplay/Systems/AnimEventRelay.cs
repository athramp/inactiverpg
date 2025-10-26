// Assets/Scripts/Gameplay/Systems/AnimEventRelay.cs
using UnityEngine;

public class AnimEventRelay : MonoBehaviour
{
    public BattleVisualController controller;
    public bool isPlayer = true;

    void Awake()
    {
        if (!controller) controller = FindObjectOfType<BattleVisualController>();
    }

    // This exact name goes into the Animation Event
    public void AttackImpact()
    {
        if (!controller) return;
        if (isPlayer) controller.OnPlayerAttackImpact();
        else          controller.OnEnemyAttackImpact();
    }
}
