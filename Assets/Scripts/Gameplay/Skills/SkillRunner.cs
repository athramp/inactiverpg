using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkillRunner : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerSpaceCoordinator coordinator;   // assign in Inspector
    [SerializeField] private Animator playerAnimator;              // optional

    [Header("Tuning")]
    [SerializeField] private bool respectRange = true;             // uses min/max from SkillProfile
    [SerializeField] private bool blockWhileCasting = true;        // prevents overlapping casts

    // internal
    private readonly Dictionary<SkillProfile, float> _cooldownUntil = new();
    private bool _casting;

    public void RunSkill(SkillProfile skill)
    {
        if (!isActiveAndEnabled || skill == null || coordinator == null) return;

        // cooldown gate
        if (_cooldownUntil.TryGetValue(skill, out var until) && Time.time < until) return;

        // block overlap if desired
        if (blockWhileCasting && _casting) return;

        // range gate (logical 1D space)
        if (respectRange && !InRange(skill)) return;

        StartCoroutine(CastRoutine(skill));
    }

    private IEnumerator CastRoutine(SkillProfile skill)
    {
        _casting = true;

        // vfx on cast
        if (skill.vfxOnCast) Instantiate(skill.vfxOnCast, coordinator.transform.position, Quaternion.identity);

        // optional anim trigger
        if (playerAnimator)
        {
            switch (skill.kind)
            {
                case SkillKind.Charge: playerAnimator.SetTrigger("Charge"); break;
                case SkillKind.Roll:   playerAnimator.SetTrigger("Roll");   break;
                case SkillKind.Blink:  playerAnimator.SetTrigger("Blink");  break;
            }
        }

        // cast time
        if (skill.castTime > 0f) yield return new WaitForSeconds(skill.castTime);

        // compute displacement relative to current enemy position
        float dx = ComputeDisplacement(skill);

        // i-frames (hook in your damage gate here if you have one)
        // if (skill.grantsIFrames) DamageGate.EnableFor(skill.iFrameDuration);

        // movement: blink instant, others smoothly over moveDuration
        if (skill.kind == SkillKind.Blink || skill.moveDuration <= 0f)
        {
            coordinator.RequestPlayerDisplacement(dx);
            // coordinator.SyncVisualToLogical();
        }
        else
        {
            float elapsed = 0f;
            float last = 0f;
            while (elapsed < skill.moveDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, skill.moveDuration));
                float step = Mathf.Lerp(0f, dx, t) - last;
                last += step;

                coordinator.RequestPlayerDisplacement(step);
                // coordinator.SyncVisualToLogical();
                yield return null;
            }
        }

        // land vfx
        if (skill.vfxOnLand) Instantiate(skill.vfxOnLand, coordinator.transform.position, Quaternion.identity);

        // start cooldown
        _cooldownUntil[skill] = Time.time + Mathf.Max(0f, skill.cooldown);

        _casting = false;
    }

    private bool InRange(SkillProfile s)
    {
        float selfX  = coordinator.LogicalX;
        float enemyX = GetEnemyLogicalX(selfX);
        float dist   = Mathf.Abs(enemyX - selfX);
        Debug.Log(
          $"[SkillRunner] Checking range for {s.displayName}: " +
          $"selfX={selfX:F2} enemyX={enemyX:F2} dist={dist:F2} " +
          $"min={s.minRangeToUse} max={s.maxRangeToUse}"
        );
        if (s.minRangeToUse > 0f && dist < s.minRangeToUse) return false;
        if (s.maxRangeToUse > 0f && dist > s.maxRangeToUse) return false;
        return true;
    }

    private float ComputeDisplacement(SkillProfile s)
    {
        float selfX  = coordinator.GetLogicalX();                 // or .LogicalX if you added the prop
float enemyX = GetEnemyLogicalX(selfX);
float dirToEnemy = Mathf.Sign(enemyX - selfX);
float d = Mathf.Abs(s.moveDistance);

float dx = s.kind switch {
    SkillKind.Charge => +d * dirToEnemy,
    SkillKind.Roll   => -d * dirToEnemy,
    SkillKind.Blink  => -d * dirToEnemy,
    _                => 0f
};

Debug.Log(
  $"[SkillRunner] {s.displayName} ({s.kind})  moveDist={s.moveDistance}  " +
  $"selfX={selfX:F2} enemyX={enemyX:F2} dirToEnemy={dirToEnemy} -> dx={dx:F2}"
);
return dx;
    }

    private float GetEnemyLogicalX(float fallbackFromSelf)
    {
        var co = CombatOrchestrator.Instance;
        if (co != null) return co.EnemyLogicalX;     // your orchestrator exposes this helper
        return fallbackFromSelf + 5f;                // safe fallback if orchestrator unavailable
    }

    // optional convenience if you want to wire up keys quickly
#if UNITY_EDITOR
    [ContextMenu("Debug Use Blink/Charge/Roll (if assigned via inspector)")]
    private void _noop() { }
#endif
}
