using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemySkillRunner : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CombatOrchestrator orchestrator;
    [SerializeField] private BattleVisualController visuals;
    public EnemyUnit unit;

    [Header("Melee Skill")]
    [SerializeField] private SkillProfile melee;   // assign a simple Damage skill
    [SerializeField] private float openerDelay = 0.25f;

    float _cd;

    void Awake()
    {
        if (!orchestrator) orchestrator = FindObjectOfType<CombatOrchestrator>();
        if (!visuals)      visuals      = FindObjectOfType<BattleVisualController>();
        _cd = openerDelay;
    }

    void OnEnable() { _cd = openerDelay; }

    void Update()
    {
        if (unit == null || melee == null) return;
        if (!orchestrator.CanEnemyAct(unit)) { _cd = Mathf.Max(_cd - Time.deltaTime, 0f); return; }

        _cd -= Time.deltaTime;
        if (_cd > 0f) return;

        // range gate
        float ex = orchestrator.GetEnemyLogicalX(unit);
        float px = orchestrator.GetPlayerLogicalX();
        if (Mathf.Abs(px - ex) > melee.maxRangeToUse) { _cd = 0.15f; return; }

        StartCoroutine(CastMelee(melee));
        _cd = melee.cooldown > 0f ? melee.cooldown : 1.2f;
    }

    IEnumerator CastMelee(SkillProfile s)
    {
        var anim = unit.view ? unit.view.GetComponentInChildren<Animator>(true) : null;
        if (anim) anim.SetTrigger("Attack");
        // visuals.playerAnimator.SetTrigger("Hit");

        if (s.vfxOnCast) { var go = Instantiate(s.vfxOnCast, unit.view.position, Quaternion.identity, visuals.GetWorldRoot()); AutoDestroy.Attach(go, 2f); }

        // if (s.castTime > 0f) yield return new WaitForSeconds(s.castTime); // melee is instant for enemy

        int dmg = Mathf.Max(1, Mathf.RoundToInt(unit.atk * s.damageMultiplier) + s.flatBonusDamage);
        orchestrator.CO_ApplyDamageToPlayer(dmg);
        orchestrator.ForceRefreshHpUI_Player();

        if (s.vfxOnLand)
        {
            var hit = Instantiate(s.vfxOnLand, visuals.GetPlayerWorldPosition(), Quaternion.identity, visuals.GetWorldRoot());
            AutoDestroy.Attach(hit, 2f);
        }
        yield return null;
    }
}
