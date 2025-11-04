using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Core.Combat;

public class SkillRunner : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerSpaceCoordinator coordinator;   // assign in Inspector
    [SerializeField] private Animator playerAnimator;              // optional

    [Header("Tuning")]
    [SerializeField] private bool respectRange = true;             // uses min/max from SkillProfile
    [SerializeField] private bool blockWhileCasting = true;        // prevents overlapping casts

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

        // ----- VFX: On Cast (spawn at player's muzzle/root; parent under world root) -----
        if (skill.vfxOnCast)
        {
            var co  = CombatOrchestrator.Instance;
            var bvc = co ? co.visuals : null;
            var pos = bvc ? bvc.GetPlayerWorldPosition() : coordinator.transform.position;
            var go  = Instantiate(skill.vfxOnCast, pos, Quaternion.identity);
            var wr  = bvc ? bvc.GetWorldRoot() : null;
            if (wr) go.transform.SetParent(wr, worldPositionStays: true);
            AutoDestroy.Attach(go);
        }

        // Optional anim trigger from MovementMode
        if (playerAnimator)
        {
            switch (skill.movement)
            {
                case MovementMode.DashToward: playerAnimator.SetTrigger("Charge"); break;
                case MovementMode.DashAway:   playerAnimator.SetTrigger("Roll");   break;
                case MovementMode.BlinkAway:  playerAnimator.SetTrigger("Blink");  break;
                case MovementMode.None:       break;
            }
        }

        // Cast time
        if (skill.castTime > 0f)
            yield return new WaitForSeconds(skill.castTime);

        // Logical positions at fire time
        float selfX  = coordinator.LogicalX;
        float enemyX = GetEnemyLogicalX(selfX);

        // Displacement delta (0 for pure-damage)
        float dx = ComputeDisplacement(skill, selfX, enemyX);

        // ---------- DAMAGE ROLE ----------
        if (skill.isPureDamage || skill.role == SkillRole.Damage)
        {
            // compute ETA if projectile is used; else 0 (instant)
            float eta = 0f;
            float projectileSpeed = 0f;

            if (skill.usesProjectile)
            {
                projectileSpeed = skill.projectileSpeedOverride;
                if (projectileSpeed <= 0f)
                {
                    var co = CombatOrchestrator.Instance;
                    if (co != null)
                        projectileSpeed = co.DebugEngine?.PlayerAttack?.projectileSpeed ?? 0f;
                }
                if (projectileSpeed > 0f)
                    eta = Mathf.Abs(enemyX - selfX) / projectileSpeed;
            }

            // tell the engine to apply damage (truth)
            var orchestrator = CombatOrchestrator.Instance;
            if (orchestrator != null && orchestrator.DebugEngine != null)
            {
                orchestrator.DebugEngine.ApplyPureDamage(
                    Side.Player,
                    skill.damageMultiplier,
                    skill.flatBonusDamage,
                    eta
                );
            }
            else
            {
                Debug.LogWarning("[SkillRunner] Engine not available to ApplyPureDamage.");
            }

            // ----- VFX: Travel (projectile) -----
            if (skill.usesProjectile)
            {
                var co  = CombatOrchestrator.Instance;
                var bvc = co ? co.visuals : null;
                if (bvc)
                {
                    var projectilePrefab = skill.vfxOnTravel != null
                        ? skill.vfxOnTravel
                        : null; // fallback to default player projectile if you want

                    if (projectilePrefab != null && projectileSpeed > 0f)
                        bvc.SpawnSkillProjectile(projectilePrefab, projectileSpeed);
                }
            }

            // ----- VFX: Land -----
            if (!skill.usesProjectile && skill.vfxOnLand)
            {
                var co  = CombatOrchestrator.Instance;
                var bvc = co ? co.visuals : null;
                var pos = bvc ? bvc.GetEnemyWorldPosition() : coordinator.transform.position;
                var go  = Instantiate(skill.vfxOnLand, pos, Quaternion.identity);
                var wr  = bvc ? bvc.GetWorldRoot() : null;
                if (wr) go.transform.SetParent(wr, worldPositionStays: true);
                AutoDestroy.Attach(go);
            }
            if (skill.usesProjectile && skill.vfxOnLand && eta > 0f)
            {
                StartCoroutine(SpawnLandVfxAfter(eta, skill.vfxOnLand));
            }

            // cooldown + exit
            _cooldownUntil[skill] = Time.time + Mathf.Max(0f, skill.cooldown);
            _casting = false;
            yield break;
        }

        // ---------- DISPLACEMENT ----------
        // i-frames could be handled here if/when you add a damage gate.
        if (skill.movement == MovementMode.BlinkAway || skill.moveDuration <= 0f)
        {
            if (Mathf.Abs(dx) > 1e-6f)
                coordinator.RequestPlayerDisplacement(dx);
        }
        else
        {
            float elapsed = 0f;
            float last = 0f;
            float dur = Mathf.Max(0.0001f, skill.moveDuration);

            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                float step = Mathf.Lerp(0f, dx, t) - last;
                last += step;

                if (Mathf.Abs(step) > 1e-6f)
                    coordinator.RequestPlayerDisplacement(step);

                yield return null;
            }
        }

        // optional land VFX on displacement
        if (skill.vfxOnLand)
        {
            var co  = CombatOrchestrator.Instance;
            var bvc = co ? co.visuals : null;
            var pos = bvc ? bvc.GetEnemyWorldPosition() : coordinator.transform.position;
            var go  = Instantiate(skill.vfxOnLand, pos, Quaternion.identity);
            var wr  = bvc ? bvc.GetWorldRoot() : null;
            if (wr) go.transform.SetParent(wr, worldPositionStays: true);
            AutoDestroy.Attach(go);
        }

        // start cooldown
        _cooldownUntil[skill] = Time.time + Mathf.Max(0f, skill.cooldown);
        _casting = false;
    }

    private bool InRange(SkillProfile s)
    {
        float selfX  = coordinator.LogicalX;
        float enemyX = GetEnemyLogicalX(selfX);
        float dist   = Mathf.Abs(enemyX - selfX);

        if (s.minRangeToUse > 0f && dist < s.minRangeToUse) return false;
        if (s.maxRangeToUse > 0f && dist > s.maxRangeToUse) return false;
        return true;
    }

    private float ComputeDisplacement(SkillProfile s, float selfX, float enemyX)
    {
        if (s.role != SkillRole.Displacement || s.movement == MovementMode.None)
            return 0f;

        float dirToEnemy = Mathf.Sign(enemyX - selfX);
        if (dirToEnemy == 0f) dirToEnemy = 1f;

        switch (s.movement)
        {
            case MovementMode.DashToward: return  +s.moveDistance * dirToEnemy; // Charge
            case MovementMode.DashAway:   return  -s.moveDistance * dirToEnemy; // Roll
            case MovementMode.BlinkAway:  return  -s.moveDistance * dirToEnemy; // Blink
            default:                      return  0f;
        }
    }

    private float GetEnemyLogicalX(float fallbackFromSelf)
    {
        var co = CombatOrchestrator.Instance;
        if (co != null)
        {
            try { return co.EnemyLogicalX; }
            catch { }

            if (co.DebugEngine != null)
                return co.DebugEngine.Enemy.PosX;
        }
        return fallbackFromSelf + 5f; // safe fallback
    }

    private IEnumerator SpawnLandVfxAfter(float t, GameObject prefab)
    {
        if (!prefab) yield break;
        yield return new WaitForSeconds(t);

        var co  = CombatOrchestrator.Instance;
        var bvc = co ? co.visuals : null;
        if (!bvc) yield break;

        var pos = bvc.GetEnemyWorldPosition();
        var go  = Instantiate(prefab, pos, Quaternion.identity);
        var wr  = bvc.GetWorldRoot();
        if (wr) go.transform.SetParent(wr, worldPositionStays: true);
        AutoDestroy.Attach(go);
    }
}
