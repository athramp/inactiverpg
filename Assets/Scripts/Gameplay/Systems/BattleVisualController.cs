// Assets/Scripts/Gameplay/Systems/BattleVisualController.cs
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public partial class BattleVisualController : MonoBehaviour
{
    [Header("Scene Refs")]
    public GameLoopService game;           // drag your GameRoot (GameLoopService) here
    public Transform playerRoot;           // where your player sprite sits
    public Animator playerAnimator;        // player's Animator
    public PlayerClassVisualMap playerVisuals;
    [Header("Debug")]
    [SerializeField] private bool enableDebug = false;

    // ===== Engine / Orchestrator hooks (assigned by CombatOrchestrator) =====
    [Header("Engine/Orchestrator Hooks")]
    public bool useEngineDrive = false; // when true, the engine controls attack timing

    // Orchestrator receives animation hit frames via these:
    public Action PlayerImpactEvent; // invoked when player's attack anim hits
    public Action EnemyImpactEvent;  // invoked when enemy's attack anim hits

    [Header("Enemy HP Bar (UI)")]
    [SerializeField] private GameObject enemyHpBarPrefab;   // assign a small UI Slider prefab (no handle)
    private Slider _enemyHpBar;                         // runtime instance

    [Header("Colliders (engagement & validation)")]
    public Collider2D playerEngageZone;    // already there
    public Collider2D playerAttackRange;   // already there
    public Collider2D playerBody;          // NEW (assign in Inspector; if none, assign playerEngageZone)

    [Header("Enemy Spawning")]
    // [SerializeField] private GameObject[] enemyPrefabs; // Eye, Goblin, Mushroom, Orc, Skeleton, etc.
    [SerializeField] private Transform enemySpawnPoint; // spawn location (usually right side)
    [SerializeField] private Transform enemyParent;     // optional parent for cleanliness

    [Header("Boss Settings")]
    [SerializeField] private int bossEvery = 10;          // every N kills spawn a boss
    [SerializeField] private float bossScaleFactor = 1.25f;
    [SerializeField] private Color bossTint = Color.red;

    [Header("Positions & Speed")]
    //public Transform engagePoint;          // where enemy should stand when engaged
    public float enemyMoveSpeed = 2.5f;

    [Header("Timings")]
    public float playerAttackRate = 1.0f;
    public float enemyAttackRate = 1.2f;

    [Header("Animator Parameters (Triggers/Bools)")]
    public string Param_AttackTrigger = "Attack";
    public string Param_HitTrigger = "Hit";
    public string Param_DeadBool = "Dead";
    public string Param_WalkTrigger = "Walk";


    [Header("Player State Names (match controller)")]
    public string Player_IdleState = "Player-Idle";
    public string Player_WalkState = "Player-Walk";
    public string Player_AttackState = "Player-Attack";
    public string Player_HitState = "Player-Hurt";
    public string Player_DeathState = "Player-Death";

    [Header("Enemy State Names (unify across enemy controllers)")]
    public string Enemy_IdleState = "Idle";
    public string Enemy_WalkState = "Walk";
    public string Enemy_AttackState = "Attack";
    public string Enemy_HitState = "Hurt";
    public string Enemy_DeathState = "Death";

    // ---- Runtime caches ----
    private GameObject _currentEnemy;
    private SpriteRenderer _currentEnemySR;
    private Animator _currentEnemyAnimator;
    private Vector3 _enemyBaseScale = Vector3.one;

    private Transform enemyRoot;
    private Animator enemyAnimator;
    public Collider2D enemyBody;

    private Coroutine approachCo;
    // private MonsterDef _currentDef;
    // private bool _currentIsBoss;

    // public MonsterDef CurrentMonsterDef => _currentDef;
    // public bool CurrentIsBoss => _currentIsBoss;

    // ----------------- Unity lifecycle -----------------
    void Awake()
    {
        if (!game) game = FindObjectOfType<GameLoopService>();
        if (!playerAnimator) Debug.LogWarning("[Battle] Player Animator not assigned.");
        //if (!enemySpawnPoint || !engagePoint) Debug.LogWarning("[Battle] Spawn/Engage points not assigned.");
    }

    private void OnEnable()
    {
        StartCoroutine(WaitForGameLoopAndSpawn());
    }
    private IEnumerator WaitForGameLoopAndSpawn()
    {
        // Wait until GameLoopService and Firebase are fully ready
        yield return new WaitUntil(() =>
            game != null &&
            game.IsInitialized &&
            FirebaseGate.IsReady &&
            game.CurrentMonsterDef != null);

        SpawnEnemyFromDef(game.CurrentMonsterDef, game.CurrentIsBoss);
        StartApproach(); // safe now, prefab and body exist

        if (enableDebug)
            Debug.Log("[BVC] Initial enemy spawned & started approach after GameLoopService ready.");
    }

    // ----------------- Player visuals API (called by orchestrator) -----------------
        public void TriggerPlayerAttack()
        {
            if (!playerAnimator) return;

            if (HasState(playerAnimator, Player_AttackState))
            {
                // Force-restart the state at t=0 so the AnimationEvent always fires
                SafePlay(playerAnimator, Player_AttackState, 0f);
            }
            else
            {
                // fallback to trigger graph
                playerAnimator.ResetTrigger(Param_HitTrigger); // avoid being stuck in hurt
                SafeTrigger(playerAnimator, Param_AttackTrigger);
            }
        }

        public void TriggerEnemyAttack()
        {
            if (!enemyAnimator) return;

            // Always restart the attack animation from the beginning
            if (HasState(enemyAnimator, Enemy_AttackState))
                enemyAnimator.Play(Enemy_AttackState, 0, 0f);
            else
                SafeTrigger(enemyAnimator, Param_AttackTrigger);
        }

    public void SetPlayerHp(int hp, int maxHp) { /* optional: wire a player bar here */ }

    public void SetEnemyHp(int hp, int max)
    {
        if (_enemyHpBar)
        {
            _enemyHpBar.maxValue = max;
            _enemyHpBar.value = hp;
        }
    }

    public void SetXp(int xp, int xpToNext) { /* optional: forward to a UI script */ }

    public void OnEnemyDied() { HandleEnemyKilled(); }
    public void OnPlayerDied() { HandlePlayerKilled(); }

    public void OnEnemyRespawned()
    {
        // ensure enemyBody points to the new enemy's collider
        enemyBody = enemyRoot ? enemyRoot.GetComponentInChildren<Collider2D>() : null;
        if (_enemyHpBar) _enemyHpBar.gameObject.SetActive(true);
    }
    public void OnPlayerRespawned() { /* no-op */ }

    public void ApplyPlayerClass(string classId)
    {
        var aoc = playerVisuals ? playerVisuals.Get(classId) : null;
        if (aoc && playerAnimator)
        {
            playerAnimator.runtimeAnimatorController = aoc;
            if (HasState(playerAnimator, Player_IdleState)) SafePlay(playerAnimator, Player_IdleState, 0f);
        }
    }

    // ----------------- Spawning -----------------
    // private GameObject PickRandomEnemyPrefab()
    // {
    //     if (enemyPrefabs == null || enemyPrefabs.Length == 0)
    //     {
    //         Debug.LogError("[Battle] No enemyPrefabs assigned on BattleVisualController.");
    //         return null;
    //     }
    //     return enemyPrefabs[UnityEngine.Random.Range(0, enemyPrefabs.Length)];
    // }

    public void SpawnEnemyFromDef(MonsterDef def, bool isBoss)
{
    if (!def || !def.prefab)
    {
        Debug.LogError("[BVC] Missing MonsterDef or prefab.");
        return;
    }

        // destroy old visual
        if (_currentEnemy) Destroy(_currentEnemy);

    // instantiate the def’s prefab under enemyRoot
    var spawnPos = enemySpawnPoint ? enemySpawnPoint.position : enemyRoot.position;
    _currentEnemy = Instantiate(def.prefab,spawnPos,Quaternion.identity, enemyRoot);
    enemyAnimator = _currentEnemy.GetComponentInChildren<Animator>();
    _currentEnemySR = _currentEnemy.GetComponentInChildren<SpriteRenderer>(true);
    enemyBody = _currentEnemy.GetComponentInChildren<Collider2D>(true);

    // VISUALS (scale/tint) — use def + boss flag
    float scaleMul = isBoss ? bossScaleFactor : 1f;
    var visual = enemyAnimator ? enemyAnimator.transform : _currentEnemy.transform;
    visual.localScale = Vector3.one * def.baseScale * scaleMul;
    if (_currentEnemySR) _currentEnemySR.color = isBoss ? bossTint : def.tint;

    // HP bar
    if (enemyHpBarPrefab)
    {
        if (_enemyHpBar) Destroy(_enemyHpBar.gameObject);
        var barGo = Instantiate(enemyHpBarPrefab, _currentEnemy.transform);
        _enemyHpBar = barGo.GetComponentInChildren<Slider>(true);
        _enemyHpBar.maxValue = game.Enemy.HpMax;
        _enemyHpBar.value    = game.Enemy.Hp;
    }

    // start approach
    StartApproach();
}


    /// <summary>
    /// Reads MonsterTag/MonsterDef from the spawned prefab, sets visual scale/tint,
    /// and pushes stats into the game's enemy runtime (via reflection so your current
    /// GameLoopService shape keeps compiling).
    /// </summary>
    private void InitEnemyFromDef(GameObject enemyGO, bool isBoss)
{
    if (!enemyGO) return;

    var tag = enemyGO.GetComponentInChildren<MonsterTag>();
    if (!tag || !tag.def)
    {
        Debug.LogWarning("[Battle] Missing MonsterTag/MonsterDef on enemy prefab; using default visuals.");
        return;
    }

    var d = tag.def;

    // VISUALS ONLY (no stat writes here)
    float scaleMul = isBoss ? bossScaleFactor : 1f;

    // Scale only the visual child (Animator) if possible; fallback to root
    var visual = _currentEnemyAnimator ? _currentEnemyAnimator.transform : enemyGO.transform;
    visual.localScale = Vector3.one * d.baseScale * scaleMul;

    if (_currentEnemySR)
        _currentEnemySR.color = isBoss ? bossTint : d.tint;
}

    // Reflection helpers (used by InitEnemyFromDef) unused for now
    private static void TrySetMember(object obj, string name, object value)
    {
        if (obj == null) return;
        var t = obj.GetType();

        var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null)
        {
            if (value != null && f.FieldType != value.GetType()) value = ConvertValue(value, f.FieldType);
            f.SetValue(obj, value);
            return;
        }

        var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null && p.CanWrite)
        {
            if (value != null && p.PropertyType != value.GetType()) value = ConvertValue(value, p.PropertyType);
            p.SetValue(obj, value, null);
        }
    }

    private static object TryGetMember(object obj, string name)
    {
        if (obj == null) return null;
        var t = obj.GetType();

        var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null) return f.GetValue(obj);

        var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null && p.CanRead) return p.GetValue(obj, null);

        return null;
    }

    private static object ConvertValue(object value, Type targetType)
    {
        try
        {
            if (targetType == typeof(int)) return Convert.ToInt32(value);
            if (targetType == typeof(float)) return Convert.ToSingle(value);
            if (targetType == typeof(double)) return Convert.ToDouble(value);
            if (targetType == typeof(string)) return Convert.ToString(value);
            return value;
        }
        catch { return value; }
    }

    // ----------------- Approach / Engage -----------------
    private void StartApproach()
    {
        if (approachCo != null) StopCoroutine(approachCo);
            approachCo = StartCoroutine(ApproachRoutine());
    }

    private IEnumerator ApproachRoutine()
    {
        // make sure all references exist
        if (!_currentEnemy || !playerEngageZone || !enemyBody)
            yield break;

        var mover = _currentEnemy.transform;

        // start walking animation
        SafePlay(enemyAnimator, Enemy_WalkState, 0f);

        // move until in melee range
        while (true)
        {
            if (!mover || !playerEngageZone || !enemyBody)
                yield break;

            var dist = playerEngageZone.Distance(enemyBody).distance;
            if (dist <= 0f) break; // reached melee

            var step = enemyMoveSpeed * Time.deltaTime;
            mover.position = Vector2.MoveTowards(
                mover.position,
                playerRoot.position,
                step
            );

            yield return null;
        }

        // arrived → idle & start combat
        SafePlay(enemyAnimator, Enemy_IdleState, 0f);
        game.BeginEngagement(); // optional if engine-driven
    }

    // ----------------- Animation Events -----------------
    public void OnPlayerAttackImpact()
    {
        if (!useEngineDrive) return;

        Debug.Log("[BVC] Player impact event raised");

        // Range gate (keep if you want contact-only melee)
        if (playerAttackRange && enemyBody)
        {
            var d = playerAttackRange.Distance(enemyBody);
            if (d.distance > 0f)
            {
                Debug.Log("[Battle] Player impact ignored (out of range)");
                return;
            }
        }

        // Notify orchestrator/engine
        PlayerImpactEvent?.Invoke();

        // ENEMY HIT REACTION — do NOT interrupt enemy's attack animation
        if (!IsInState(enemyAnimator, Enemy_AttackState))
        {
            if (HasState(enemyAnimator, Enemy_HitState)) SafePlay(enemyAnimator, Enemy_HitState);
            else SafeTrigger(enemyAnimator, Param_HitTrigger);
        }
    }

    public void OnEnemyAttackImpact()
    {
        if (!useEngineDrive) return;

        Debug.Log("[BVC] Enemy impact event raised");

        // Range gate
        if (playerEngageZone && enemyBody)
        {
            var d = playerEngageZone.Distance(enemyBody);
            if (d.distance > 0f)
            {
                Debug.Log("[Battle] Enemy impact ignored (out of range)");
                return;
            }
        }

        // Notify orchestrator/engine
        EnemyImpactEvent?.Invoke();

        // Visual hit reaction on player (do NOT interrupt attack)
        if (HasState(playerAnimator, Player_HitState)) SafePlay(playerAnimator, Player_HitState);
        else if (!IsInState(playerAnimator, Player_AttackState))
            SafeTrigger(playerAnimator, Param_HitTrigger);
    }

    // ----------------- Game events -----------------
    private void HandleEnemyKilled()
    {
        if (enemyAnimator)
        {
            SafeSetBool(enemyAnimator, Param_DeadBool, true);
            if (_enemyHpBar) _enemyHpBar.gameObject.SetActive(false);
        }
        StartCoroutine(EnemyDeathThenRespawn());
    }

    private IEnumerator EnemyDeathThenRespawn()
    {
        yield return new WaitForSeconds(0.6f);
        SpawnEnemyFromDef(game.CurrentMonsterDef, game.CurrentIsBoss);          // creates new enemy + new HP bar
        StartApproach();
    }

    private void HandlePlayerKilled()
    {
        if (playerAnimator) SafeSetBool(playerAnimator, Param_DeadBool, true);
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
    private bool IsInState(Animator anim, string stateName)
{
    if (!anim) return false;
    var st = anim.GetCurrentAnimatorStateInfo(0);
    return st.IsName(stateName);
}

}
