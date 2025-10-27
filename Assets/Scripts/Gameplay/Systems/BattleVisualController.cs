// Assets/Scripts/Gameplay/Systems/BattleVisualController.cs
using System.Collections;
using System.Reflection;
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
    [SerializeField] private GameObject[] enemyPrefabs;   // Eye, Goblin, Mushroom, Orc, Skeleton, etc.
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
    public string Enemy_IdleState   = "Idle";     // recommend unifying to simple names
    public string Enemy_WalkState   = "Walk";
    public string Enemy_AttackState = "Attack";
    public string Enemy_HitState    = "Hurt";
    public string Enemy_DeathState  = "Death";

    // ---- Runtime caches ----
    private int _killCount;
    private GameObject _currentEnemy;
    private SpriteRenderer _currentEnemySR;
    private Animator _currentEnemyAnimator;
    private Vector3 _enemyBaseScale = Vector3.one;
    private SimpleHpBar2D _enemyHpBar;

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
        _enemyHpBar = null; // clear old ref
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
    _currentEnemySR = _currentEnemy.GetComponentInChildren<SpriteRenderer>();
    _currentEnemyAnimator = _currentEnemy.GetComponentInChildren<Animator>();
    enemyRoot = _currentEnemy.transform;
    enemyAnimator = _currentEnemyAnimator;
    enemyBody = _currentEnemy.GetComponentInChildren<Collider2D>();

    if (enemyAnimator == null)
    {
        Debug.LogError("[Battle] Spawned enemy missing Animator.");
        return;
    }

    // Reset visuals
    if (_currentEnemySR) _currentEnemySR.color = Color.white;
    _enemyBaseScale = _currentEnemy.transform.localScale;
    _currentEnemy.transform.localScale = _enemyBaseScale;

    // Boss check
    bool isBoss = bossEvery > 0 && _killCount > 0 && _killCount % bossEvery == 0;

    // Apply MonsterDef → runtime stats + visual (scale/tint)
    InitEnemyFromDef(_currentEnemy, isBoss);

    // HP BAR: cache it *after* the prefab exists, then init *after* stats are applied
    _enemyHpBar = _currentEnemy.GetComponentInChildren<SimpleHpBar2D>(true);
    UpdateEnemyHpBarFromRuntime();

    // Reset animator to Idle
    SafePlay(enemyAnimator, Enemy_IdleState, 0f);
}


    /// <summary>
    /// Reads MonsterTag/MonsterDef from the spawned prefab, sets visual scale/tint,
    /// and pushes stats into the game's enemy runtime (via reflection so your current
    /// GameLoopService shape keeps compiling).
    /// </summary>
    // --- Updates the enemy HP bar (SimpleHpBar2D) based on runtime HP values ---
private void UpdateEnemyHpBarFromRuntime()
{
    if (_enemyHpBar == null || game == null) return;

    object enemyRuntime = null;

    // Try to locate your runtime enemy object in GameLoopService
    var f = game.GetType().GetField("enemy", 
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
    if (f != null) enemyRuntime = f.GetValue(game);

    if (enemyRuntime == null)
    {
        var p = game.GetType().GetProperty("Enemy", 
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (p != null) enemyRuntime = p.GetValue(game, null);
    }

    if (enemyRuntime == null) return;

    // --- Try to read max HP ---
    int maxHp = 1;
    var final = TryGetMember(enemyRuntime, "Final");
    if (final is System.Collections.IDictionary dict && dict.Contains("hp"))
        maxHp = Mathf.Max(1, System.Convert.ToInt32(dict["hp"]));
    else
        maxHp = Mathf.Max(1, System.Convert.ToInt32(TryGetMember(enemyRuntime, "HP") ?? 1));

    // --- Try to read current HP ---
    int curHp = Mathf.Max(0, System.Convert.ToInt32(TryGetMember(enemyRuntime, "CurrentHP") ?? maxHp));

    _enemyHpBar.SetMax(maxHp);
    _enemyHpBar.SetCurrent(curHp);
}

    private void InitEnemyFromDef(GameObject enemyGO, bool isBoss)
    {
        if (!enemyGO) return;

        var tag = enemyGO.GetComponentInChildren<MonsterTag>();
        if (!tag || !tag.def)
        {
            Debug.LogWarning("[Battle] Missing MonsterTag/MonsterDef on enemy prefab; using existing runtime values.");
        }
        else
        {
            var d = tag.def;

            // Visuals
            _enemyBaseScale = enemyGO.transform.localScale;
            float scaleMul = isBoss ? bossScaleFactor : 1f;
            enemyGO.transform.localScale = Vector3.one * d.baseScale * scaleMul; // respect def scale baseline

            if (_currentEnemySR) _currentEnemySR.color = isBoss ? bossTint : d.tint;

            // Try to push stats into GameLoopService.enemy (reflection-based to avoid hard coupling)
            if (game != null)
            {
                object enemyRuntime = null;

                // 1) Look for a field named "enemy"
                var f = game.GetType().GetField("enemy", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null) enemyRuntime = f.GetValue(game);

                // 2) If not found, look for a property named "Enemy"
                if (enemyRuntime == null)
                {
                    var p = game.GetType().GetProperty("Enemy", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (p != null) enemyRuntime = p.GetValue(game, null);
                }

                if (enemyRuntime != null)
                {
                    TrySetMember(enemyRuntime, "MonsterId", d.monsterId);

                    // XP reward (boss multiplier)
                    int xp = isBoss ? Mathf.RoundToInt(d.xpReward * 3.0f) : d.xpReward;
                    TrySetMember(enemyRuntime, "XpReward", xp);

                    // Final stat map (supports Dictionary<string,float> or your StatMap)
                    var final = TryGetMember(enemyRuntime, "Final");
                    if (final is System.Collections.IDictionary dict)
                    {
                        dict["hp"]          = isBoss ? Mathf.RoundToInt(d.hp  * 3.0f) : d.hp;
                        dict["atk"]         = isBoss ? Mathf.RoundToInt(d.atk * 1.8f) : d.atk;
                        dict["def"]         = isBoss ? Mathf.RoundToInt(d.def * 2.0f) : d.def;
                        dict["crit_chance"] = d.critChance;
                        dict["crit_mult"]   = d.critMult;
                    }
                    else
                    {
                        // Fallback: individual fields if you used simple ints/floats
                        TrySetMember(enemyRuntime, "HP",  isBoss ? Mathf.RoundToInt(d.hp  * 3.0f) : d.hp);
                        TrySetMember(enemyRuntime, "ATK", isBoss ? Mathf.RoundToInt(d.atk * 1.8f) : d.atk);
                        TrySetMember(enemyRuntime, "DEF", isBoss ? Mathf.RoundToInt(d.def * 2.0f) : d.def);
                        TrySetMember(enemyRuntime, "CritChance", d.critChance);
                        TrySetMember(enemyRuntime, "CritMult",   d.critMult);
                    }

                    // Initialize CurrentHP if your runtime has it
                    var initHpMethod = enemyRuntime.GetType().GetMethod("InitHP",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (initHpMethod != null) initHpMethod.Invoke(enemyRuntime, null);
                }
                else
                {
                    Debug.LogWarning("[Battle] Could not find game.enemy runtime via reflection. Stats were not pushed.");
                }
            }
        }

        // If it wasn't a boss but you still want def.tint applied (already done above), nothing else needed.
    }

    // Reflection helpers
    private static void TrySetMember(object obj, string name, object value)
    {
        if (obj == null) return;
        var t = obj.GetType();

        var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null)
        {
            if (value != null && f.FieldType != value.GetType())
            {
                value = ConvertValue(value, f.FieldType);
            }
            f.SetValue(obj, value);
            return;
        }

        var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null && p.CanWrite)
        {
            if (value != null && p.PropertyType != value.GetType())
            {
                value = ConvertValue(value, p.PropertyType);
            }
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

    private static object ConvertValue(object value, System.Type targetType)
    {
        try
        {
            if (targetType == typeof(int))   return System.Convert.ToInt32(value);
            if (targetType == typeof(float)) return System.Convert.ToSingle(value);
            if (targetType == typeof(double))return System.Convert.ToDouble(value);
            if (targetType == typeof(string))return System.Convert.ToString(value);
            return value;
        }
        catch { return value; }
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
        UpdateEnemyHpBarFromRuntime();

        // play enemy hurt
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

        // play player hurt
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
    yield return new WaitForSeconds(0.6f);

    _killCount++;
    SpawnEnemyRandom();

    // ensure the new enemy’s bar shows full HP
    UpdateEnemyHpBarFromRuntime();

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
