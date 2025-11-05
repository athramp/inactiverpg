using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemySkillRunner : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CombatOrchestrator orchestrator; // auto-find if null

    [Header("Rotation")]
    [SerializeField] private SkillProfile[] skills;
    [SerializeField] private bool randomOrder = true;

    [Header("Cadence")]
    [SerializeField] private Vector2 cooldownRange = new Vector2(0.9f, 1.6f);
    [SerializeField] private float openerDelay = 0.25f;

    [Header("Gating")]
    [SerializeField] private bool gateByRange = true;
    [SerializeField] private float extraRangeBuffer = 0f;
    [SerializeField] private float defaultDisplacementDuration = 0.15f;
    [SerializeField] private Vector2 randomStaggerRange = new Vector2(0.0f, 0.05f);
[SerializeField] private BattleVisualController visuals; // auto-find

void Awake() {
    if (!orchestrator) orchestrator = FindObjectOfType<CombatOrchestrator>();
    if (!visuals)      visuals      = FindObjectOfType<BattleVisualController>();
    _cd = openerDelay; _nextIdx = 0;
}

// --- VFX helpers ---
void SpawnCastVfx(SkillProfile s) {
    if (!visuals || !s.vfxOnCast) return;
    Instantiate(s.vfxOnCast, visuals.GetEnemyWorldPosition(), Quaternion.identity, visuals.GetWorldRoot());
}

void SpawnLandVfxAtPlayer(SkillProfile s) {
    if (!visuals || !s.vfxOnLand) return;
    Instantiate(s.vfxOnLand, visuals.GetPlayerWorldPosition(), Quaternion.identity, visuals.GetWorldRoot());
}

void SpawnLandVfxSelf(SkillProfile s) {
    if (!visuals || !s.vfxOnLand) return;
    Instantiate(s.vfxOnLand, visuals.GetEnemyWorldPosition(), Quaternion.identity, visuals.GetWorldRoot());
}

IEnumerator SpawnProjectileTravel(SkillProfile s, float duration) {
    if (!visuals || !s.vfxOnTravel) yield break;
    var root = visuals.GetWorldRoot();
    var go = Instantiate(s.vfxOnTravel, visuals.GetEnemyWorldPosition(), Quaternion.identity, root);

    Vector3 from = visuals.GetEnemyWorldPosition();
    Vector3 to   = visuals.GetPlayerWorldPosition();

    float t = 0f;
    while (t < 1f) {
        t += Time.deltaTime / Mathf.Max(0.001f, duration);
        float e = t * t * (3f - 2f * t); // smoothstep
        go.transform.position = Vector3.Lerp(from, to, e);
        yield return null;
    }
    // Land VFX at player on arrival
    SpawnLandVfxAtPlayer(s);
    Destroy(go);
}

    float _cd;
    int _nextIdx;


    void OnEnable() { _cd = openerDelay; }

    void Update()
    {
        if (skills == null || skills.Length == 0 || orchestrator == null) return;
        if (!CanEnemyAct()) return;

        _cd -= Time.deltaTime;
        if (_cd > 0f) return;

        var s = PickNextSkill();
        if (!gateByRange || InRange(s))
            StartCoroutine(Execute(s));

        _cd = Random.Range(cooldownRange.x, cooldownRange.y);
    }

    SkillProfile PickNextSkill()
    {
        if (randomOrder) return skills[Random.Range(0, skills.Length)];
        var r = skills[_nextIdx]; _nextIdx = (_nextIdx + 1) % skills.Length; return r;
    }

    bool InRange(SkillProfile s)
    {
        float enemyX  = orchestrator.GetEnemyLogicalX();
        float playerX = orchestrator.GetPlayerLogicalX();
        float dist = Mathf.Abs(playerX - enemyX);
        float min = s.respectRange ? s.minRangeToUse : 0f;
        float max = s.respectRange ? s.maxRangeToUse : 999f;
        return dist >= min && dist <= (max + extraRangeBuffer);
    }

    bool CanEnemyAct() => orchestrator.CanEnemyAct();

    IEnumerator Execute(SkillProfile s)
    {
        // pre-cast
        SpawnCastVfx(s);
        if (s.castTime > 0f) yield return new WaitForSeconds(s.castTime);

        switch (s.kind)
        {
            case SkillKind.Damage:
                yield return DoDamage(s);
                break;

            case SkillKind.CrowdControl:
                DoCC(s);
                break;

            case SkillKind.Support:
                DoSupport(s);
                break;

            case SkillKind.Displacement:
                DoDisplacement(s);
                break;
        }
    }

    IEnumerator DoDamage(SkillProfile s)
    {
        int baseAtk = orchestrator.GetEnemyAtk(); // mirror of PlayerAtk
        int dmg = Mathf.Max(1, Mathf.RoundToInt(baseAtk * s.damageMultiplier) + s.flatBonusDamage);

        if (s.useProjectile)
        {
            // compute simple ETA; you can spawn your own VFX here if desired
            float enemyX = orchestrator.GetEnemyLogicalX();
            float playerX = orchestrator.GetPlayerLogicalX();
            float dist = Mathf.Abs(playerX - enemyX);
            float eta = (s.projectileSpeed > 0.001f) ? dist / s.projectileSpeed : 0.2f;
            StartCoroutine(SpawnProjectileTravel(s, eta));
            orchestrator.EmitProjectileETA_FromEnemy(eta); // see patch below
            yield return new WaitForSeconds(eta);
            orchestrator.CO_ApplyDamageToPlayer(dmg);
        }
        else {
            SpawnLandVfxAtPlayer(s);
            orchestrator.CO_ApplyDamageToPlayer(dmg);
         }

        
    }
        void DoDisplacement(SkillProfile s)
    {        
        float enemyX  = orchestrator.GetEnemyLogicalX();
    float playerX = orchestrator.GetPlayerLogicalX();
    float dir = Mathf.Sign(playerX - enemyX);

    float dx = (s.moveDistance > 0f ? s.moveDistance : s.ccMagnitude);
    float dur = (s.moveDuration > 0f ? s.moveDuration : defaultDisplacementDuration);
    float stagger = (randomStaggerRange.y > 0f)
        ? Random.Range(randomStaggerRange.x, randomStaggerRange.y)
        : 0f;

    if (s.targeting == TargetingType.Self)
    {
        orchestrator.TweenEnemyBy(dir * dx, dur, stagger);
    }

    }
    void DoCC(SkillProfile s)
    {
        switch (s.ccType)
        {
            case CCType.Stun:
                orchestrator.CO_StunPlayer(s.ccDuration);
                break;
            case CCType.Knockback:
                float dir = Mathf.Sign(orchestrator.GetPlayerLogicalX() - orchestrator.GetEnemyLogicalX()); // push away from enemy
                orchestrator.KnockbackPlayer(+s.ccMagnitude * dir);
                break;
            case CCType.Pull:
                dir = Mathf.Sign(orchestrator.GetPlayerLogicalX() - orchestrator.GetEnemyLogicalX());
                orchestrator.KnockbackPlayer(-s.ccMagnitude * dir);
                break;
            case CCType.Slow:
                // reuse stun-lite
                orchestrator.CO_StunPlayer(Mathf.Min(s.ccDuration * 0.4f, s.ccDuration));
                break;
        }
    }

    void DoSupport(SkillProfile s)
    {
        switch (s.supportType)
        {
            case SupportType.Heal:
                orchestrator.CO_HealEnemy(s.supportAmount);
                // orchestrator.RefreshEnemyHpUI();
                break;
            case SupportType.Shield:
                orchestrator.CO_AddShieldToEnemy(s.supportAmount);
                break;
            case SupportType.BuffAtk:
            case SupportType.Haste:
                // hook up if you have enemy buffs; otherwise no-op
                break;
        }
    }
}
