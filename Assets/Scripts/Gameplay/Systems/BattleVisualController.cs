// Assets/Scripts/Gameplay/Systems/BattleVisualController.cs
using System.Collections;
using UnityEngine;

public partial class BattleVisualController : MonoBehaviour
{
    [Header("Scene Refs")]
    public GameLoopService game;           // drag your GameRoot (GameLoopService) here
    public Transform playerRoot;           // where your player sprite sits
    public Animator playerAnimator;        // player's Animator

    [Header("Colliders (engagement & validation)")]
    public Collider2D playerEngageZone;    // player's front body/engage trigger
    public Collider2D playerAttackRange;   // player's attack range (optional)

    [Header("Enemy Spawning")]
    [SerializeField] private GameObject[] enemyPrefabs;   // Orc, Skeleton, Mushroom, etc.
    [SerializeField] private Transform enemySpawnPoint;   // spawn location (usually right side)
    [SerializeField] private Transform enemyParent;       // optional parent for cleanliness

    [Header("Boss Settings")]
    [SerializeField] private int bossEvery = 10;          // every N kills spawn a boss
    [SerializeField] private float bossScaleFactor = 1.25f; // multiply existing scale
    [SerializeField] private Color bossTint = Color.red;

    [Header("Positions & Speed")]
    public Transform engagePoint;          // where enemy should stand when engaged
    public float enemyMoveSpeed = 2.5f;

    [Header("Timings")]
    public float playerAttackRate = 1.0f;  // seconds between player attacks
    public float enemyAttackRate  = 1.2f;  // seconds between enemy attacks

    [Header("Animator Parameters (Triggers/Bools)")]
    public string Param_AttackTrigger = "Attack";
    public string Param_HitTrigger    = "Hit";
    public string Param_DeadBool      = "Dead";

    [Header("Player State Names (match controller)")]
    public string Player_IdleState   = "Soldier-Idle";
    public string Player_WalkState   = "Soldier-Walk";
    public string Player_AttackState = "Soldier-Attack";
    public string Player_HitState    = "Soldier-Hurt";
    public string Player_DeathState  = "Soldier-Death";

    [Header("Enemy State Names (unify across enemy controllers)")]
    public string Enemy_IdleState   = "Orc-Idle";
    public string Enemy_WalkState   = "Orc-Walk";
    public string Enemy_AttackState = "Orc-Attack";
    public string Enemy_HitState    = "Orc-Hurt";
    public string Enemy_DeathState  = "Orc-Death";

    // ---- Runtime caches ----
    private int _killCount;
    private GameObject _currentEnemy;
    private SpriteRenderer _currentEnemySR;
    private Animator _currentEnemyAnimator;
    private Vector3 _enemyBaseScale = Vector3.one;

    private Transform enemyRoot;
    private Animator enemyAnimator;
    private Collider2D enemyBody;

    private Coroutine approachCo;
    private Coroutine playerAtkCo;
    private Coroutine enemyAtkCo;

    // ----------------- Unity lifecycle -----------------
    void Awake()
    {
        if (!game) game = FindObjectOfType<GameLoopService>();
        if (!playerAnimator) Debug.LogWarning("[Battle] Player Animator not assigned.");
        if (!enemySpawnPoint || !engagePoint) Debug.LogWarning("[Battle] Spawn/Engage points not assigned.");
    }

    void OnEnable()
    {
        if (game != null)
        {
            game.OnEnemyKilled += HandleEnemyKilled;
            game.OnPlayerKilled += HandlePlayerKilled;
        }

        SpawnEnemyRandom();
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

    // ----------------- Spawning -----------------
    private GameObject PickRandomEnemyPrefab()
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0)
        {
            Debug.LogError("[Battle] No enemyPrefabs assigned on BattleVisualController.");
            return null;
        }
        return enemyPrefabs[UnityEngine.Random.Range(0, enemyPrefabs.Length)];
    }

    private void SpawnEnemyRandom()
    {
        // Clean up any previous instance
        if (_currentEnemy != null)
        {
            if (approachCo != null)
            {
                StopCoroutine(approachCo);
                approachCo = null;
            }

            Destroy(_currentEnemy);
            _currentEnemy = null;
            _currentEnemySR = null;
            _currentEnemyAnimator = null;
        }

        // Pick prefab
        var prefab = PickRandomEnemyPrefab();
        if (prefab == null) return;

        // Spawn
        Vector3 spawnPos = enemySpawnPoint ? enemySpawnPoint.position : Vector3.zero;
        _currentEnemy = Instantiate(
            prefab,
            spawnPos,
            Quaternion.identity,
            enemyParent ? enemyParent : transform
        );

        // Cache shared references used by the rest of the class
        _currentEnemySR        = _currentEnemy.GetComponentInChildren<SpriteRenderer>();
        _currentEnemyAnimator  = _currentEnemy.GetComponentInChildren<Animator>();
        enemyRoot              = _currentEnemy.transform;
        enemyAnimator          = _currentEnemyAnimator;
        enemyBody              = _currentEnemy.GetComponentInChildren<Collider2D>();

        if (enemyAnimator == null)
        {
            Debug.LogError("[Battle] Spawned enemy missing Animator.");
            return;
        }

        // Reset visuals
        if (_currentEnemySR) _currentEnemySR.color = Color.white;
        _enemyBaseScale = _currentEnemy.transform.localScale; // record this prefab's base (often ~20x)
        _currentEnemy.transform.localScale = _enemyBaseScale;

        // Boss check (multiply scale, don't overwrite)
        bool isBoss = bossEvery > 0 && _killCount > 0 && _killCount % bossEvery == 0;
        if (isBoss)
        {
            _currentEnemy.transform.localScale = _enemyBaseScale * bossScaleFactor;
            if (_currentEnemySR) _currentEnemySR.color = bossTint;
        }

        // Reset animator to Idle
        SafePlay(enemyAnimator, Enemy_IdleState, 0f);
    }

    // ----------------- Approach / Engage -----------------
    private void StartApproach()
    {
        if (!engagePoint)
        {
            Debug.LogError("[Battle] engagePoint not assigned.");
            return;
        }

        if (approachCo != null) StopCoroutine(approachCo);
        approachCo = StartCoroutine(ApproachRoutine());
    }

    private IEnumerator ApproachRoutine()
    {
        if (!enemyRoot) yield break;

        // If either collider missing, fallback to simple move-to-engagePoint
        if (!playerEngageZone || !enemyBody)
        {
            Debug.LogWarning("[Battle] Missing playerEngageZone or enemyBody; falling back to engagePoint.");
            // walk anim if present
            SafePlay(enemyAnimator, Enemy_WalkState, 0f);

            while (enemyRoot && Vector2.Distance(enemyRoot.position, engagePoint.position) > 0.05f)
            {
                enemyRoot.position = Vector2.MoveTowards(
                    enemyRoot.position,
                    engagePoint.position,
                    enemyMoveSpeed * Time.deltaTime
                );
                yield return null;
            }
        }
        else
        {
            // walk anim if present
            SafePlay(enemyAnimator, Enemy_WalkState, 0f);

            // Move until colliders touch (Distance <= 0)
            while (enemyRoot)
            {
                var dist = playerEngageZone.Distance(enemyBody);
                if (dist.distance <= 0f) break;

                // Move toward the player
                var step = enemyMoveSpeed * Time.deltaTime;
                enemyRoot.position = Vector2.MoveTowards(enemyRoot.position, playerRoot.position, step);

                yield return null;
            }
        }

        if (!enemyRoot) yield break;

        // Arrived: idle & engage
        SafePlay(enemyAnimator, Enemy_IdleState, 0f);

        game.BeginEngagement();
        StartCombatLoops();
    }

    // ----------------- Combat loops -----------------
    private void StartCombatLoops()
    {
        if (playerAtkCo != null) StopCoroutine(playerAtkCo);
        if (enemyAtkCo  != null) StopCoroutine(enemyAtkCo);

        playerAtkCo = StartCoroutine(PlayerAttackLoop());
        enemyAtkCo  = StartCoroutine(EnemyAttackLoop());
    }

    private IEnumerator PlayerAttackLoop()
    {
        var wait = new WaitForSeconds(playerAttackRate);
        while (true)
        {
            while (game == null || !game.IsEngaged) yield return null;

            if (HasState(playerAnimator, Player_AttackState))
                SafePlay(playerAnimator, Player_AttackState, 0f);
            else
                SafeTrigger(playerAnimator, Param_AttackTrigger);

            yield return wait;
        }
    }

    private IEnumerator EnemyAttackLoop()
    {
        var wait = new WaitForSeconds(enemyAttackRate);
        while (true)
        {
            while (game == null || !game.IsEngaged) yield return null;

            if (HasState(enemyAnimator, Enemy_AttackState))
                SafePlay(enemyAnimator, Enemy_AttackState, 0f);
            else
                SafeTrigger(enemyAnimator, Param_AttackTrigger);

            yield return wait;
        }
    }

    // ----------------- Animation Events -----------------
    public void OnPlayerAttackImpact()
    {
        if (game == null) return;

        // Optional: validate range if you have a playerAttackRange
        if (playerAttackRange && enemyBody)
        {
            var d = playerAttackRange.Distance(enemyBody);
            if (d.distance > 0f) return; // not in range
        }

        int dmg = game.PlayerAttackOnce();

        if (HasState(enemyAnimator, Enemy_HitState))
            SafePlay(enemyAnimator, Enemy_HitState, 0f);
        else
            SafeTrigger(enemyAnimator, Param_HitTrigger);
    }

    public void OnEnemyAttackImpact()
    {
        if (game == null) return;

        // Validate enemy vs player's engage zone
        if (playerEngageZone && enemyBody)
        {
            var d = playerEngageZone.Distance(enemyBody);
            if (d.distance > 0f) return; // not in range
        }

        int dmg = game.EnemyAttackOnce();

        if (HasState(playerAnimator, Player_HitState))
            SafePlay(playerAnimator, Player_HitState, 0f);
        else
            SafeTrigger(playerAnimator, Param_HitTrigger);
    }

    // ----------------- Game events -----------------
    private void HandleEnemyKilled()
    {
        if (enemyAnimator)
        {
            SafeSetBool(enemyAnimator, Param_DeadBool, true);
            if (enemyAtkCo != null) StopCoroutine(enemyAtkCo);
        }

        StartCoroutine(EnemyDeathThenRespawn());
    }

    private IEnumerator EnemyDeathThenRespawn()
    {
        yield return new WaitForSeconds(0.6f); // death anim length buffer

        _killCount++;
        SpawnEnemyRandom();

        // If you want the boss check here (instead of inside Spawn), you can:
        // if (bossEvery > 0 && _killCount % bossEvery == 0) { /* scale/tint here */ }

        StartApproach();
    }

    private void HandlePlayerKilled()
    {
        if (playerAnimator)
        {
            SafeSetBool(playerAnimator, Param_DeadBool, true);
        }
        if (playerAtkCo != null) StopCoroutine(playerAtkCo);
        if (enemyAtkCo  != null) StopCoroutine(enemyAtkCo);

        StartCoroutine(PlayerRespawn());
    }

    private IEnumerator PlayerRespawn()
    {
        yield return new WaitForSeconds(0.8f);

        if (playerAnimator)
        {
            SafeSetBool(playerAnimator, Param_DeadBool, false);
            if (HasState(playerAnimator, Player_IdleState))
                SafePlay(playerAnimator, Player_IdleState, 0f);
        }

        StartCombatLoops();
    }

    // ----------------- Animator helpers -----------------
    private static bool HasState(Animator anim, string stateName, int layer = 0)
    {
        if (!anim || anim.runtimeAnimatorController == null) return false;
        if (layer < 0 || layer >= anim.layerCount) return false;
        int hash = Animator.StringToHash(stateName);
        return anim.HasState(layer, hash);
    }

    private static void SafePlay(Animator anim, string stateName, float normalizedTime = 0f, int layer = 0)
    {
        if (!anim || string.IsNullOrEmpty(stateName)) return;
        if (!HasState(anim, stateName, layer))
        {
            Debug.LogWarning($"[Battle] Animator '{anim.gameObject.name}' missing state '{stateName}' on layer {layer}.");
            return;
        }
        anim.CrossFadeInFixedTime(stateName, 0.05f, layer, normalizedTime);
    }

    private static void SafeTrigger(Animator anim, string trigger)
    {
        if (!anim || string.IsNullOrEmpty(trigger)) return;
        anim.ResetTrigger(trigger);
        anim.SetTrigger(trigger);
    }

    private static void SafeSetBool(Animator anim, string param, bool value)
    {
        if (!anim || string.IsNullOrEmpty(param)) return;
        anim.SetBool(param, value);
    }
}
