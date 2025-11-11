using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public partial class BattleVisualController : MonoBehaviour
{
    [Header("World")]
    [SerializeField] Transform worldRoot;
    [SerializeField] Transform playerMover;
    [SerializeField] Transform enemyParent;
    [SerializeField] Transform enemySpawnPoint;

    [Header("Player")]
    public Transform playerRoot;
    public Animator  playerAnimator;
    [SerializeField] private RuntimeAnimatorController warriorCtrl;
    [SerializeField] private RuntimeAnimatorController mageCtrl;
    [SerializeField] private RuntimeAnimatorController archerCtrl;

    [Header("VFX / Projectiles")]
    [SerializeField] private GameObject playerProjectilePrefab;
    [SerializeField] private Transform  playerMuzzle;
    [SerializeField] private float playerProjectileTrailSpeed = 6f;
    public bool useEngineDrive = true; // old scripts toggle this

public Transform PlayerMuzzle => playerMuzzle;

    [Header("Anim Params")]
    public string Param_AttackTrigger = "Attack";
    public string Param_HitTrigger    = "Hit";
    public string Param_DeadBool      = "Dead";

    public float EnemySpawnX => enemySpawnPoint ? enemySpawnPoint.position.x : 0f;

    void Awake()
    {
        if (!worldRoot) worldRoot = transform;
        if (!enemyParent) enemyParent = transform;
    }

    public void ApplyPlayerClass(string classId)
    {
        if (!playerAnimator) return;
        RuntimeAnimatorController ctrl = warriorCtrl;
        if (string.Equals(classId, "Mage",   System.StringComparison.OrdinalIgnoreCase)) ctrl = mageCtrl;
        if (string.Equals(classId, "Archer", System.StringComparison.OrdinalIgnoreCase)) ctrl = archerCtrl;
        playerAnimator.runtimeAnimatorController = ctrl;
    }

    public void TriggerPlayerAttack()
    {
        if (!playerAnimator) return;
        playerAnimator.ResetTrigger(Param_HitTrigger);
        playerAnimator.SetTrigger(Param_AttackTrigger);
    }
    public void TriggerPlayerHit()
    {
        if (!playerAnimator) return;
        playerAnimator.ResetTrigger(Param_AttackTrigger);
        playerAnimator.SetTrigger(Param_HitTrigger);
    }

    public void SetXp(int xp, int xpToNext) { /* hook up UI if needed */ }

    // ------- Multi-enemy: spawn view and return its mover -------
    public Transform SpawnEnemyView(MonsterDef def, bool isBoss, Vector3 worldPos)
    {
        if (!def || !def.prefab) { Debug.LogError("[BVC] Missing MonsterDef/prefab"); return null; }
        var moverGO = new GameObject("EnemyMover");
        var mover = moverGO.transform;
        mover.SetParent(worldRoot, false);
        mover.localPosition = worldPos;

        var model = Instantiate(def.prefab, mover);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one * (isBoss ? def.baseScale * 1.25f : def.baseScale);

        // Optional HP bar on model if your prefab expects one:
        // (leave to prefab or orchestrator if needed)
        return mover;
    }
    public void OnPlayerAttackImpact() { /* legacy no-ETA impact; optional VFX */ }

public Vector3 GetEnemyWorldPosition() { 
    // Legacy helper had no target context; return spawn as a neutral point.
    return enemySpawnPoint ? enemySpawnPoint.position : (worldRoot ? worldRoot.position : Vector3.zero);
}
public void SetPlayerX(float x)
{
    if (!playerMover) return;
    var lp = playerMover.localPosition;
    lp.x = x;
    playerMover.localPosition = lp;

    // ensure model is under the mover
    if (playerRoot && playerRoot.parent != playerMover)
        playerRoot.SetParent(playerMover, true);
}
public void OnPlayerDied()
{
    if (playerAnimator)
    {
        playerAnimator.ResetTrigger(Param_AttackTrigger);
        playerAnimator.ResetTrigger(Param_HitTrigger);
        playerAnimator.SetBool(Param_DeadBool, true);
    }
}

public void OnPlayerRespawn()
{
    if (playerAnimator)
    {
        playerAnimator.ResetTrigger(Param_AttackTrigger);
        playerAnimator.ResetTrigger(Param_HitTrigger);
        playerAnimator.SetBool(Param_DeadBool, false);
        playerAnimator.CrossFadeInFixedTime("Player-Idle", 0.05f, 0);
    }
}

    // ------- Projectiles for player skills -------
    public void SpawnSkillProjectile(Transform from, Transform to, GameObject prefab, float etaSec)
    {
        if (!prefab || !from || !to) return;
        var parent = worldRoot ? worldRoot : transform;

        var anchor = new GameObject("ProjectileTargetAnchor").transform;
        anchor.SetParent(parent, true);
        anchor.position = to.position;

        var proj = Instantiate(prefab, from.position, Quaternion.identity, parent).transform;
        float dist  = Vector3.Distance(from.position, anchor.position);
        float speed = (etaSec > 0.001f) ? (dist / etaSec) : playerProjectileTrailSpeed;

        StartCoroutine(FlyToAnchor(proj, anchor, speed));
    }

    public Transform GetWorldRoot() => worldRoot ? worldRoot : transform;
    public Vector3   GetPlayerWorldPosition() => playerRoot ? playerRoot.position : Vector3.zero;

    private IEnumerator FlyToAnchor(Transform proj, Transform anchor, float speed)
    {
        if (!proj || !anchor) yield break;
        float eps = Mathf.Max(0.0001f, speed) * Time.deltaTime * 1.25f;
        while (proj && anchor)
        {
            var to = anchor.position;
            if ((proj.position - to).sqrMagnitude <= eps * eps) break;
            proj.position = Vector3.MoveTowards(proj.position, to, speed * Time.deltaTime);
            yield return null;
        }
        if (proj) Destroy(proj.gameObject);
        if (anchor) Destroy(anchor.gameObject);
    }
}
