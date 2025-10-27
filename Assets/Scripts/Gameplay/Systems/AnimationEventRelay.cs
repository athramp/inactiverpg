using UnityEngine;

public class AnimationEventRelay : MonoBehaviour
{
    public BattleVisualController controller;

    // Called from PLAYER attack animation
    public void OnPlayerAttackImpact() => controller?.OnPlayerAttackImpact();

    // Called from ENEMY attack animation
    public void OnEnemyAttackImpact() => controller?.OnEnemyAttackImpact();
}
