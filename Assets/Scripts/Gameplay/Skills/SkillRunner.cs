// Assets/Scripts/Gameplay/Skills/SkillRunner.cs
using System.Collections;
using UnityEngine;

public class SkillRunner : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerSpaceCoordinator coordinator;   // world shifting + player visual motion
    [SerializeField] private CombatOrchestrator orchestrator;      // engine wrappers
    [SerializeField] private BattleVisualController visuals;       // VFX helpers

    [Header("Tuning")]
    public bool blockWhileCasting = true; // set animation bool if you want
    public bool respectRange = true;      // fallback if profile.respectRange unset

    public void RunSkill(SkillProfile profile)
    {
        Debug.Log($"[SkillRunner] RunSkill {profile.name}");
        if (!profile || !coordinator || !orchestrator || !visuals) return;
        StartCoroutine(CastRoutine(profile));
    }

    IEnumerator CastRoutine(SkillProfile s)
    {
        // Range gate (1D)
        if ((s.respectRange || respectRange) && !InRange(s))
            yield break;

        // Optional: set animator flags here via visuals.playerAnimator
        if (blockWhileCasting) ; // e.g., set a "Casting" bool

        // Cast VFX
        SpawnCastVfx(s);

        // i-frames (optional)
        if (s.grantsIFrames) StartCoroutine(IFrames(s.iFrameDuration));

        // Pre-cast wait
        if (s.castTime > 0f) yield return new WaitForSeconds(s.castTime);

        switch (s.kind)
        {
            case SkillKind.Displacement:
                yield return DoDisplacement(s);
                break;

            case SkillKind.Damage:
                yield return DoDamage(s);
                break;

            case SkillKind.CrowdControl:
                yield return DoCrowdControl(s);
                break;

            case SkillKind.Support:
                yield return DoSupport(s);
                break;
        }

        // Optional land VFX for non-projectile impacts
        SpawnLandVfxIfDirect(s);

        // End casting flag
        if (blockWhileCasting) ; // unset animator flag
    }

    // ---------- Range checks ----------
    bool InRange(SkillProfile s)
    {
        float selfX  = orchestrator.PlayerX;
        float enemyX = orchestrator.EnemyX;
        float dist   = Mathf.Abs(enemyX - selfX);
        return dist >= s.minRangeToUse && dist <= s.maxRangeToUse;
    }

    int OrientationToEnemy() => (orchestrator.EnemyX - orchestrator.PlayerX) >= 0f ? +1 : -1;

    // ---------- Kinds ----------
    IEnumerator DoDisplacement(SkillProfile s)
    {
        float dir = OrientationToEnemy();  // +toward enemy, -away
        float dx  = s.moveDistance * dir;
        if (s.moveDuration <= 0.001f)
        {
            // blink: instant
            coordinator.RequestPlayerDisplacement(dx);
        }
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
        // compute engine damage (multiplier + flat)
        // You could ask the engine for current ATK and DEF, but a simple “pure damage”
        // matches your current ApplyPureDamage pattern.
        int baseAtk = orchestrator.PlayerAtk;
        int dmg = Mathf.Max(1, Mathf.RoundToInt(baseAtk * s.damageMultiplier) + s.flatBonusDamage);

        if (s.useProjectile && s.vfxOnTravel)
        {
            // Visual projectile (engine damage is still time-locked by the engine if you want,
            // but for pure skills we apply on land to “feel right”)
            Vector3 from = visuals.GetPlayerWorldPosition();
            Vector3 to   = visuals.GetEnemyWorldPosition();
            float dist   = Mathf.Abs(to.x - from.x);
            float tta    = (s.projectileSpeed > 0.001f) ? dist / s.projectileSpeed : 0.2f;

            // Fire projectile using your existing VFX pipeline
            visuals.SpawnSkillProjectile(s.vfxOnTravel, s.projectileSpeed);

            // Wait for arrival
            yield return new WaitForSeconds(tta);

            // Land VFX + engine damage
            SpawnLandVfxAtEnemy(s);
            orchestrator.CO_ApplyDamageToEnemy(dmg);
        }
        else
        {
            // Instant hit (melee burst, beam, etc.)
            SpawnLandVfxAtEnemy(s);
            orchestrator.CO_ApplyDamageToEnemy(dmg);
        }
        yield return null;
    }

    IEnumerator DoCrowdControl(SkillProfile s)
    {
        switch (s.ccType)
        {
            case CCType.Stun:
            Debug.Log("[SkillRunner] CC: Stun enemy for: " + s.ccDuration + " seconds.");
                orchestrator.CO_StunEnemy(s.ccDuration);
                SpawnLandVfxAtEnemy(s);
                break;

            case CCType.Knockback:
                // Positive magnitude pushes enemy away from player
                float dir = OrientationToEnemy(); // +enemy on right, -enemy on left
                orchestrator.CO_KnockbackEnemy(+s.ccMagnitude * dir);
                SpawnLandVfxAtEnemy(s);
                break;

            case CCType.Pull:
                dir = OrientationToEnemy();
                orchestrator.CO_KnockbackEnemy(-s.ccMagnitude * dir);
                SpawnLandVfxAtEnemy(s);
                break;

            case CCType.Slow:
                // You can implement slow in engine (attack speed debuff). For now, stun-lite:
                orchestrator.CO_StunEnemy(Mathf.Min(s.ccDuration * 0.4f, s.ccDuration));
                SpawnLandVfxAtEnemy(s);
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
                Debug.Log($"[SkillRunner] Healed player for {s.supportAmount}");
                SpawnLandVfxSelf(s);
                break;

            case SupportType.Shield:
                orchestrator.CO_AddShieldToPlayer(s.supportAmount);
                SpawnLandVfxSelf(s);
                break;

            case SupportType.BuffAtk:
                // Example: push buff into engine: set Player.AtkBuffTimer/Multiplier (you can add wrapper)
                // orchestrator.Engine_PlayerStartAtkBuff(s.supportDuration, 1.25f); // add small wrapper if you want
                SpawnLandVfxSelf(s);
                break;

            case SupportType.Haste:
                // You can later add “attack rate multiplier” in engine config
                // For now you can reuse Atk buff or skip
                SpawnLandVfxSelf(s);
                break;
        }
        yield return null;
    }

    // ---------- VFX helpers ----------
    void SpawnCastVfx(SkillProfile s)
    {
        if (!s.vfxOnCast) return;
        var root = visuals.GetWorldRoot();
        var go = Instantiate(s.vfxOnCast, visuals.GetPlayerWorldPosition(), Quaternion.identity, root);
        AutoDestroy.Attach(go, 2.0f);
    }

    void SpawnLandVfxIfDirect(SkillProfile s)
    {
        if (!s.vfxOnLand) return;
        if (s.kind == SkillKind.Damage && s.useProjectile) return; // handled on arrival
        var root = visuals.GetWorldRoot();
        var pos = (s.targeting == TargetingType.Self) ? visuals.GetPlayerWorldPosition() : visuals.GetEnemyWorldPosition();
        var go = Instantiate(s.vfxOnLand, pos, Quaternion.identity, root);
        AutoDestroy.Attach(go, 2.0f);
    }

    void SpawnLandVfxAtEnemy(SkillProfile s)
    {
        if (!s.vfxOnLand) return;
        var go = Instantiate(s.vfxOnLand, visuals.GetEnemyWorldPosition(), Quaternion.identity, visuals.GetWorldRoot());
        AutoDestroy.Attach(go, 2.0f);
    }

    void SpawnLandVfxSelf(SkillProfile s)
    {
        if (!s.vfxOnLand) return;
        var go = Instantiate(s.vfxOnLand, visuals.GetPlayerWorldPosition(), Quaternion.identity, visuals.GetWorldRoot());
        AutoDestroy.Attach(go, 2.0f);
    }

    IEnumerator IFrames(float seconds)
    {
        // Hook your damage intake gate here if needed
        yield return new WaitForSeconds(seconds);
    }
}
