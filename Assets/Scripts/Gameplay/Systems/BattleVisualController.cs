// Assets/Scripts/Gameplay/Systems/BattleVisualController.cs
using System.Collections;
using UnityEngine;

public class BattleVisualController : MonoBehaviour
{
    [Header("Scene Refs")]
    public GameLoopService game;         // drag your GameRoot (GameLoopService) here
    public Transform playerRoot;         // a transform where your player sprite lives
    public Animator playerAnimator;      // player's Animator (Idle/Walk/Attack/Die)

    [Header("Colliders (for engagement & hit validation)")]
    public Collider2D playerEngageZone;   // Soldier’s trigger collider
    public Collider2D playerAttackRange;  // Soldier’s (trigger) attack radius (optional but useful)

    [Header("Enemy")]
    public Transform enemySpawnPoint;    // offscreen/right
    public Transform engagePoint;        // where enemy should stand when engaged
    public GameObject enemyPrefab;       // prefab with Sprite+Animator
    public float enemyMoveSpeed = 2.5f;

    [Header("Timings")]
    public float playerAttackRate = 1.0f; // seconds between attacks
    public float enemyAttackRate = 1.2f;

    [Header("Animator Param Names (shared on both controllers)")]
    public string Param_AttackTrigger = "Attack";
    public string Param_HitTrigger    = "Hit";
    public string Param_DeadBool      = "Dead";

    [Header("Player State Names (match your Soldier controller EXACTLY)")]
    public string Player_IdleState   = "Soldier-Idle";
    public string Player_WalkState   = "Soldier-Walk";
    public string Player_AttackState = "Soldier-Attack";
    public string Player_HitState    = "Soldier-Hurt";
    public string Player_DeathState  = "Soldier-Death";

    [Header("Enemy State Names (match your Orc controller EXACTLY)")]
    public string Enemy_IdleState   = "Orc-Idle";
    public string Enemy_WalkState   = "Orc-Walk";
    public string Enemy_AttackState = "Orc-Attack";
    public string Enemy_HitState    = "Orc-Hurt";
    public string Enemy_DeathState  = "Orc-Death";

    Transform enemyRoot;
    Animator enemyAnimator;
    Coroutine approachCo, playerAtkCo, enemyAtkCo;

    void Awake()
    {

        if (!game) game = FindObjectOfType<GameLoopService>();
        // On BattleVisualController.Awake()
if (playerAnimator == null) Debug.LogWarning("[Battle] Player Animator missing");
if (enemyPrefab == null)    Debug.LogWarning("[Battle] Enemy Prefab missing");
if (enemySpawnPoint == null || engagePoint == null) Debug.LogWarning("[Battle] Points missing");

    }

    void OnEnable()
    {
        if (game != null)
        {
            game.OnEnemyKilled += HandleEnemyKilled;
            game.OnPlayerKilled += HandlePlayerKilled;
        }

        SpawnEnemyVisual();
        StartApproach();
    }

    void OnDisable()
    {
        if (game != null)
        {
            game.OnEnemyKilled -= HandleEnemyKilled;
            game.OnPlayerKilled -= HandlePlayerKilled;
        }
    }

    // ----------------- Safe Animator helpers -----------------
    static bool HasState(Animator anim, string stateName, int layer = 0)
    {
        if (!anim || anim.runtimeAnimatorController == null) return false;
        if (layer < 0 || layer >= anim.layerCount) return false;
        int hash = Animator.StringToHash(stateName);
        return anim.HasState(layer, hash);
    }

    static void SafePlay(Animator anim, string stateName, float normalizedTime = 0f, int layer = 0)
    {
        if (!anim || string.IsNullOrEmpty(stateName)) return;
        if (!HasState(anim, stateName, layer))
        {
            Debug.LogWarning($"[Battle] Animator '{anim.gameObject.name}' missing state '{stateName}' on layer {layer}.");
            return;
        }
        // Smooth cross-fade avoids harsh snaps
        anim.CrossFadeInFixedTime(stateName, 0.05f, layer, normalizedTime);
    }

    static void SafeTrigger(Animator anim, string trigger)
    {
        if (!anim || string.IsNullOrEmpty(trigger)) return;
        anim.ResetTrigger(trigger);
        anim.SetTrigger(trigger);
    }

    static void SafeSetBool(Animator anim, string param, bool value)
    {
        if (!anim || string.IsNullOrEmpty(param)) return;
        anim.SetBool(param, value);
    }
    // ---------------------------------------------------------

Collider2D enemyBody; // add as a private field

void SpawnEnemyVisual()
{
    if (enemyRoot) Destroy(enemyRoot.gameObject);
    var go = Instantiate(enemyPrefab, enemySpawnPoint.position, Quaternion.identity, transform);
    enemyRoot = go.transform;
    enemyAnimator = go.GetComponentInChildren<Animator>();
    enemyBody    = go.GetComponentInChildren<Collider2D>(); // grabs the Capsule/Box on the prefab

    // face & start walking like before…
    SafePlay(enemyAnimator, Enemy_WalkState, 0f);
}

    void StartApproach()
    {
        if (!engagePoint)
        {
            Debug.LogError("[Battle] engagePoint not assigned.");
            return;
        }

        if (approachCo != null) StopCoroutine(approachCo);
        approachCo = StartCoroutine(ApproachRoutine());
    }

IEnumerator ApproachRoutine()
{
    // simple guard
    if (!playerEngageZone || !enemyBody)
    {
        Debug.LogWarning("[Battle] Missing playerEngageZone or enemyBody collider.");
        // fallback to old engage point if needed
        while (enemyRoot && Vector2.Distance(enemyRoot.position, engagePoint.position) > 0.05f)
        {
            enemyRoot.position = Vector2.MoveTowards(enemyRoot.position, engagePoint.position, enemyMoveSpeed * Time.deltaTime);
            yield return null;
        }
    }
    else
    {
        // Move enemy forward until colliders touch (distance ≤ 0)
        while (enemyRoot)
        {
            // distance > 0 means separated, ≤ 0 means overlapping/touching
            var dist = playerEngageZone.Distance(enemyBody);
            if (dist.distance <= 0f) break;

            // advance towards the player
            var targetX = Mathf.Max(enemyRoot.position.x - dist.distance, enemyRoot.position.x); // naive advance
            var step = enemyMoveSpeed * Time.deltaTime;
            enemyRoot.position = Vector2.MoveTowards(enemyRoot.position, playerRoot.position, step);

            yield return null;
        }
    }

    if (!enemyRoot) yield break;

    // We’re in melee range now
    if (enemyAnimator && HasState(enemyAnimator, Enemy_IdleState)) SafePlay(enemyAnimator, Enemy_IdleState);

    game.BeginEngagement();
    StartCombatLoops();
}

    void StartCombatLoops()
    {
        if (playerAtkCo != null) StopCoroutine(playerAtkCo);
        if (enemyAtkCo != null) StopCoroutine(enemyAtkCo);
        playerAtkCo = StartCoroutine(PlayerAttackLoop());
        enemyAtkCo  = StartCoroutine(EnemyAttackLoop());
    }

    IEnumerator PlayerAttackLoop()
    {
        var wait = new WaitForSeconds(playerAttackRate);
        while (true)
        {
            // Wait until the enemy has reached the engage point
            while (game == null || !game.IsEngaged) yield return null;

            if (HasState(playerAnimator, Player_AttackState))
                SafePlay(playerAnimator, Player_AttackState);
            else
                SafeTrigger(playerAnimator, Param_AttackTrigger);

            yield return wait;
        }
    }

    IEnumerator EnemyAttackLoop()
    {
        var wait = new WaitForSeconds(enemyAttackRate);
        while (true)
        {
            while (game == null || !game.IsEngaged) yield return null;

            if (HasState(enemyAnimator, Enemy_AttackState))
                SafePlay(enemyAnimator, Enemy_AttackState);
            else
                SafeTrigger(enemyAnimator, Param_AttackTrigger);

            yield return wait;
        }
    }

    // === CALLED BY ANIMATION EVENTS ===
public void OnPlayerAttackImpact()
{
    if (game == null) return;

    // If we set up playerAttackRange, validate overlap
    if (playerAttackRange && enemyBody)
    {
        var d = playerAttackRange.Distance(enemyBody);
        if (d.distance > 0f) return; // not in range → no damage
    }

    int dmg = game.PlayerAttackOnce();

    if (HasState(enemyAnimator, Enemy_HitState)) SafePlay(enemyAnimator, Enemy_HitState);
    else SafeTrigger(enemyAnimator, Param_HitTrigger);
}

public void OnEnemyAttackImpact()
{
    if (game == null) return;

    // Validate enemy against player's engage zone (or you can add a dedicated PlayerBody collider)
    if (playerEngageZone && enemyBody)
    {
        var d = playerEngageZone.Distance(enemyBody);
        if (d.distance > 0f) return; // not in range
    }

    int dmg = game.EnemyAttackOnce();

    if (HasState(playerAnimator, Player_HitState)) SafePlay(playerAnimator, Player_HitState);
    else SafeTrigger(playerAnimator, Param_HitTrigger);
}

    // === Game events ===
    void HandleEnemyKilled()
    {
        if (enemyAnimator)
        {
            SafeSetBool(enemyAnimator, Param_DeadBool, true);
            if (enemyAtkCo != null) StopCoroutine(enemyAtkCo);
        }
        StartCoroutine(EnemyDeathThenRespawn());
    }

    IEnumerator EnemyDeathThenRespawn()
    {
        yield return new WaitForSeconds(0.6f); // death anim length
        SpawnEnemyVisual();
        StartApproach();
    }

    void HandlePlayerKilled()
    {
        if (playerAnimator)
        {
            SafeSetBool(playerAnimator, Param_DeadBool, true);
        }
        if (playerAtkCo != null) StopCoroutine(playerAtkCo);
        if (enemyAtkCo  != null) StopCoroutine(enemyAtkCo);

        StartCoroutine(PlayerRespawn());
    }

    IEnumerator PlayerRespawn()
    {
        yield return new WaitForSeconds(0.8f);
        if (playerAnimator)
        {
            SafeSetBool(playerAnimator, Param_DeadBool, false);

            if (HasState(playerAnimator, Player_IdleState)) SafePlay(playerAnimator, Player_IdleState);
        }
        StartCombatLoops();
    }
}
