using System.Collections;
using UnityEngine;

public class SkillRunner : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerSpaceCoordinator coordinator;
    [SerializeField] private CombatOrchestrator orchestrator;
    [SerializeField] private BattleVisualController visuals;

    [Header("Tuning")]
    public bool blockWhileCasting = true;
    public bool respectRange = true;

    public void RunSkill(SkillProfile profile)
    {
        if (!profile || !coordinator || !orchestrator || !visuals) return;
        StartCoroutine(CastRoutine(profile));
    }

    IEnumerator CastRoutine(SkillProfile s)
    {
        if ((s.respectRange || respectRange) && !InRange(s)) yield break;

        SpawnCastVfx(s);
        if (s.grantsIFrames) StartCoroutine(IFrames(s.iFrameDuration));
        if (s.castTime > 0f) yield return new WaitForSeconds(s.castTime);

        switch (s.kind)
        {
            case SkillKind.Displacement: yield return DoDisplacement(s); break;
            case SkillKind.Damage: yield return DoDamage(s); break;
            case SkillKind.CrowdControl: yield return DoCrowdControl(s); break;
            case SkillKind.Support: yield return DoSupport(s); break;
        }

        SpawnLandVfxIfDirect(s);
    }

    bool InRange(SkillProfile s)
    {
        float selfX  = orchestrator.GetPlayerLogicalX();
        float enemyX = orchestrator.GetTargetEnemyTransform() ? orchestrator.GetTargetEnemyTransform().localPosition.x : selfX + 999f;
        float dist   = Mathf.Abs(enemyX - selfX);
        return dist >= s.minRangeToUse && dist <= s.maxRangeToUse;
    }

    IEnumerator DoDisplacement(SkillProfile s)
    {
        float dir = (orchestrator.GetTargetEnemyTransform()?.localPosition.x ?? (orchestrator.GetPlayerLogicalX()+1f)) - orchestrator.GetPlayerLogicalX() >= 0f ? +1 : -1;
        float dx  = s.moveDistance * dir;

        if (s.moveDuration <= 0.001f) coordinator.RequestPlayerDisplacement(dx);
        else
        {
            float t = 0f;
            while (t < s.moveDuration)
            {
                float step = dx * (Time.deltaTime / s.moveDuration);
                coordinator.RequestPlayerDisplacement(step);
                t += Time.deltaTime;
                yield return null;
            }
        }
    }

    IEnumerator DoDamage(SkillProfile s)
    {
        int baseAtk = orchestrator.PlayerAtk;
        int dmg = Mathf.Max(1, Mathf.RoundToInt(baseAtk * s.damageMultiplier) + s.flatBonusDamage);
        Debug.Log($"[SkillRunner] Skill '{s.displayName}' dealing {dmg} damage.");

        var target = orchestrator.CurrentTarget;
        var targetTf = orchestrator.GetTargetEnemyTransform();
        if (target == null || !targetTf) yield break;

        if (s.useProjectile && s.vfxOnTravel)
        {
            Debug.Log($"[SkillRunner] Skill '{s.displayName}' launching projectile.");
            Vector3 from = visuals.GetPlayerWorldPosition();
            Vector3 to   = targetTf.position;
            float dist   = Mathf.Abs(to.x - from.x);
            float tta    = (s.projectileSpeed > 0.001f) ? dist / s.projectileSpeed : 0.2f;

            visuals.SpawnSkillProjectile(
                visuals.PlayerMuzzle ? visuals.PlayerMuzzle : null,
                targetTf,
                s.vfxOnTravel,
                tta
            );
            yield return new WaitForSeconds(tta);

            if (s.vfxOnLand)
            {
                var land = Instantiate(s.vfxOnLand, targetTf.position, Quaternion.identity, visuals.GetWorldRoot());
                AutoDestroy.Attach(land, 2f);
            }

            orchestrator.ApplyDamageToEnemy(target.enemyId, dmg);

            // ---- NEW: Apply DoT after successful hit ----

            if (s.appliesDot && s.dotDamagePerTick > 0 && s.dotDuration > 0f)
            {
                Debug.Log($"[SkillRunner] Skill '{s.displayName}' applied DoT: {s.dotDamagePerTick} dmg/tick every {s.dotTickEvery}s for {s.dotDuration}s.");
                orchestrator.ApplyDotToEnemy(target.enemyId, s.dotDamagePerTick, s.dotDuration, s.dotTickEvery);
            }
                
        }
        else
        {
            if (s.vfxOnLand)
            {
                var go = Instantiate(s.vfxOnLand, targetTf.position, Quaternion.identity, visuals.GetWorldRoot());
                AutoDestroy.Attach(go, 2f);
            }

            orchestrator.ApplyDamageToEnemy(target.enemyId, dmg);

            // ---- NEW: Apply DoT after instant (non-projectile) hit ----
            Debug.Log($"[SkillRunner] Skill applies dot: '{s.appliesDot}', dmg/tick: {s.dotDamagePerTick}, duration: {s.dotDuration}, tickEvery: {s.dotTickEvery}");
            if (s.appliesDot && s.dotDamagePerTick > 0 && s.dotDuration > 0f)
            {
                orchestrator.ApplyDotToEnemy(target.enemyId, s.dotDamagePerTick, s.dotDuration, s.dotTickEvery);
                Debug.Log($"[SkillRunner] Skill '{s.displayName}' applied DoT: {s.dotDamagePerTick} dmg/tick every {s.dotTickEvery}s for {s.dotDuration}s.");
            }

        }

        yield return null;
    }

    IEnumerator DoCrowdControl(SkillProfile s)
    {
        var target = orchestrator.CurrentTarget;
        if (target == null) yield break;

        switch (s.ccType)
        {
            case CCType.Stun:
                orchestrator.CO_StunEnemy(target, s.ccDuration);
                break;
            case CCType.Knockback:
            {
                float dir = (orchestrator.GetTargetEnemyTransform().localPosition.x - orchestrator.GetPlayerLogicalX()) >= 0f ? +1 : -1;
                orchestrator.KnockbackEnemy(target, +s.ccMagnitude * dir);
                break;
            }
            case CCType.Pull:
            {
                float dir = (orchestrator.GetTargetEnemyTransform().localPosition.x - orchestrator.GetPlayerLogicalX()) >= 0f ? +1 : -1;
                orchestrator.KnockbackEnemy(target, -s.ccMagnitude * dir);
                break;
            }
            case CCType.Slow:
                orchestrator.CO_StunEnemy(target, Mathf.Min(s.ccDuration * 0.4f, s.ccDuration));
                break;
        }
        yield return null;
    }

    IEnumerator DoSupport(SkillProfile s)
    {
        switch (s.supportType)
        {
            case SupportType.Heal:
                orchestrator.CO_HealPlayer(s.supportAmount);
                orchestrator.ForceRefreshHpUI_Player();
                break;
            case SupportType.Shield:
                orchestrator.CO_AddShieldToPlayer(s.supportAmount);
                break;
        }
        yield return null;
    }

    // VFX helpers
    void SpawnCastVfx(SkillProfile s)
    {
        if (!s.vfxOnCast) return;
        var root = visuals.GetWorldRoot();
        var go = Instantiate(s.vfxOnCast, visuals.GetPlayerWorldPosition(), Quaternion.identity, root);
        AutoDestroy.Attach(go, 2f);
    }
    void SpawnLandVfxIfDirect(SkillProfile s)
    {
        if (!s.vfxOnLand) return;
        if (s.kind == SkillKind.Damage && s.useProjectile) return;
        var root = visuals.GetWorldRoot();
        var pos  = (s.targeting == TargetingType.Self) ? visuals.GetPlayerWorldPosition() : orchestrator.GetTargetEnemyWorldPos();
        var go   = Instantiate(s.vfxOnLand, pos, Quaternion.identity, root);
        AutoDestroy.Attach(go, 2f);
    }

    IEnumerator IFrames(float dur)
    {
        // hook your i-frames here if needed
        yield return new WaitForSeconds(dur);
    }
}
