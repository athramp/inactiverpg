// Assets/Scripts/Gameplay/Systems/AnimEventRelay.cs
using UnityEngine;

public class AnimEventRelay : MonoBehaviour
{
    public BattleVisualController visuals;
    public bool enableAnimationEvents = true; // expose if you like

    // Called by the animation timeline
    public void AttackImpact()
    {
        // If visuals are engine-driven, do NOT relay
        if (!enableAnimationEvents || (visuals && visuals.useEngineDrive)) return;

        // Old behavior (only when not engine-driven)
        if (visuals) visuals.OnPlayerAttackImpact();
    }

    // Add similar guards for any other relayed events (AttackStart, Hit, etc.)
}
