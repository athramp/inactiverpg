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
    private bool playerIsRanged;
private bool enemyIsRanged;

public void SetPlayerRanged(bool v) => playerIsRanged = v;
public void SetEnemyRanged(bool v)  => enemyIsRanged  = v;

    // ===== Engine / Orchestrator hooks (assigned by CombatOrchestrator) =====
    [Header("Engine/Orchestrator Hooks")]
    public bool useEngineDrive = false; // when true, the engine controls attack timing

    // Orchestrator receives animation hit frames via these:
    // public Action PlayerImpactEvent; // invoked when player's attack anim hits
    // public Action EnemyImpactEvent;  // invoked when enemy's attack anim hits
    [Header("Approach Tuning")]
    [SerializeField] private float playerEnemyGap = 0.5f; // meters to stop at (tune this)
    [SerializeField] private float approachSpeed = 0.2f;  // m/s
    [Header("Enemy HP Bar (UI)")]
    [SerializeField] private GameObject enemyHpBarPrefab;   // assign a small UI Slider prefab (no handle)
    private Slider _enemyHpBar;                         // runtime instance

    [Header("Colliders (engagement & validation)")]
    public Collider2D playerEngageZone;    // already there
    public Collider2D playerAttackRange;   // already there
    public Collider2D playerBody;          // NEW (assign in Inspector; if none, assign playerEngageZone)
    [Header("Ranged VFX")]
    [SerializeField] private GameObject playerProjectilePrefab; // arrow/fireball
    [SerializeField] private Transform  playerMuzzle;           // where it spawns from
    [SerializeField] private GameObject enemyProjectilePrefab;
    [SerializeField] private Transform  enemyMuzzle;
    [SerializeField] private float      projectileTrailSpeed = 50f; // purely visual
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
    // ----------------- Public properties -----------------
    public float PlayerPosX => playerRoot ? playerRoot.position.x : 0f;
    public float EnemyPosX => _currentEnemy
    ? _currentEnemy.transform.position.x
    : (enemySpawnPoint ? enemySpawnPoint.position.x : (enemyRoot ? enemyRoot.position.x : 0f));
    // ----------------- Unity lifecycle -----------------
    void Awake()
    {
        if (!game) game = FindObjectOfType<GameLoopService>();
        if (!playerAnimator) Debug.LogWarning("[BVC] Player Animator not assigned.");

        // Ensure a valid, unscaled parent for enemies
        enemyRoot = enemyParent ? enemyParent : transform;
        enemyRoot.localScale = Vector3.one;
    }
    // ----------------- Player visuals API (called by orchestrator) -----------------
        public void TriggerPlayerAttack()
    {   
            Debug.Log("[BVC] TriggerPlayerAttack");
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

    public void OnEnemyDied() { StartCoroutine(PlayEnemyDeath()); }

    private IEnumerator PlayEnemyDeath()
    {
        if (enemyAnimator) SafeSetBool(enemyAnimator, Param_DeadBool, true);
        if (_enemyHpBar) _enemyHpBar.gameObject.SetActive(false);
        yield return new WaitForSeconds(0.6f); // allow death anim to show
        // Do NOT respawn here — Orchestrator will spawn the next enemy
    }
    public void OnPlayerDied()
    {
        // Visuals only — DO NOT start any respawn or clear Dead here
        if (playerAnimator) SafeSetBool(playerAnimator, Param_DeadBool, true);
    }
    public void OnPlayerRespawned()
    {
        if (playerAnimator) SafeSetBool(playerAnimator, Param_DeadBool, false);
        if (HasState(playerAnimator, Player_IdleState))
            SafePlay(playerAnimator, Player_IdleState, 0f);
    }

    public void ApplyPlayerClass(string classId)
    {
        var aoc = playerVisuals ? playerVisuals.Get(classId) : null;
        if (aoc && playerAnimator)
        {
            playerAnimator.runtimeAnimatorController = aoc;
            if (HasState(playerAnimator, Player_IdleState)) SafePlay(playerAnimator, Player_IdleState, 0f);
        }
    }
    // ----------------- Projectile VFX -----------------
    private void SpawnAndFlyProjectile(Transform from, Vector3 to, GameObject prefab, float speed)
    {
        if (!prefab || from == null) return;
        var go = Instantiate(prefab, from.position, Quaternion.identity, transform);
        StartCoroutine(Fly(go.transform, to, speed));
    }

    private System.Collections.IEnumerator Fly(Transform t, Vector3 to, float speed)
    {
        while (t && (t.position - to).sqrMagnitude > 0.0004f)
        {
            t.position = Vector3.MoveTowards(t.position, to, speed * Time.deltaTime);
            yield return null;
        }
        if (t) Destroy(t.gameObject);
    }
    // ----------------- Spawning -----------------
        public void SpawnEnemyFromDef(MonsterDef def, bool isBoss)
        {
            if (!def || !def.prefab)
            {
                Debug.LogError("[BVC] Missing MonsterDef or prefab.");
                return;
            }

            // --- Ensure a valid parent for enemies ---
            if (!enemyRoot)
            {
                enemyRoot = enemyParent ? enemyParent : transform;
                enemyRoot.localScale = Vector3.one;
            }

            // --- Destroy old enemy (visual only) ---
            if (_currentEnemy)
            {
                Destroy(_currentEnemy);
                _currentEnemy = null;
            }

            // --- Instantiate new enemy under enemyRoot ---
            var spawnPos = enemySpawnPoint ? enemySpawnPoint.position : enemyRoot.position;
            _currentEnemy = Instantiate(def.prefab, enemyRoot);

            // Reset local transform so no inherited scale/rotation messes it up
            var t = _currentEnemy.transform;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale    = Vector3.one;

            // Apply intended visual scale from MonsterDef (and boss multiplier)
            float scaleMul = isBoss ? bossScaleFactor : 1f;
            t.localScale *= def.baseScale * scaleMul;

            // Place enemy at world spawn position
            t.position = spawnPos;

            // --- Cache components ---
            enemyAnimator    = _currentEnemy.GetComponentInChildren<Animator>();
            _currentEnemySR  = _currentEnemy.GetComponentInChildren<SpriteRenderer>(true);
            enemyBody        = _currentEnemy.GetComponentInChildren<Collider2D>(true);
            _currentEnemyAnimator = enemyAnimator; // optional: keep ref for hit reactions

            // --- Apply tint (boss vs normal) ---
            if (_currentEnemySR)
                _currentEnemySR.color = isBoss ? bossTint : def.tint;

            // --- Create HP bar ---
            if (enemyHpBarPrefab)
            {
                if (_enemyHpBar)
                    Destroy(_enemyHpBar.gameObject);

                var barGo = Instantiate(enemyHpBarPrefab, _currentEnemy.transform);
                _enemyHpBar = barGo.GetComponentInChildren<Slider>(true);
                _enemyHpBar.maxValue = game.Enemy.HpMax;
                _enemyHpBar.value    = game.Enemy.Hp;
            }

            // --- Start approach movement (purely visual) ---
            StartApproach();

            // --- Debug log to verify spawn transform ---
            Debug.Log($"[BVC] Spawned enemy '{def.name}' at {t.position}, " +
                    $"localScale {t.localScale}, parent={enemyRoot.name}, " +
                    $"root scale={enemyRoot.lossyScale}");
        }

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
        if (!playerEngageZone || !enemyBody)
        {
            Debug.LogWarning("[BVC] Missing colliders for approach; stopping.");
            return; // <-- was `yield break;` (illegal in void)
        }

        Debug.Log("[BVC] Starting enemy approach routine.");
        if (approachCo != null) StopCoroutine(approachCo);
        approachCo = StartCoroutine(ApproachRoutine());
    }


    private IEnumerator ApproachRoutine()
    {
        if (_currentEnemy == null || playerRoot == null) yield break;
        var enemyT = _currentEnemy.transform;

        while (true)
        {
            float gap = Mathf.Abs(playerRoot.position.x - enemyT.position.x);
            // Debug.Log($"[BVC] Approach gap: {gap:F2}m");
            if (gap <= playerEnemyGap) yield break;

            float dir = Mathf.Sign(playerRoot.position.x - enemyT.position.x);
            enemyT.position += new Vector3(dir * approachSpeed * Time.deltaTime, 0f, 0f);
            yield return null;
        }
    }

    // ----------------- Animation Events -----------------

        public void OnPlayerAttackImpact()
    {
        Debug.Log($"[BVC#{GetInstanceID()}] ENTRY OnPlayerAttackImpact drive={useEngineDrive}");
            if (!useEngineDrive) return;

            // If ranged (projectileSpeed > 0), spawn projectile; otherwise keep your “Hit” reaction
            // We can detect “ranged” by presence of prefab/muzzle
            if (playerProjectilePrefab && playerMuzzle&& playerIsRanged)
        {
                Debug.Log("[BVC] OnPlayerAttackImpact - spawning projectile");
                var target = _currentEnemy ? _currentEnemy.transform.position : enemySpawnPoint.position;
                SpawnAndFlyProjectile(playerMuzzle, target, playerProjectilePrefab, projectileTrailSpeed);
            }
            else
            {
            // melee VFX: cause target hit reaction immediately
            if (enemyAnimator) { SafeTrigger(enemyAnimator, Param_HitTrigger); Debug.Log("[BVC] Triggered enemy hit reaction."); }
            }
        }

        public void OnEnemyAttackImpact()
    {
        Debug.Log($"[BVC#{GetInstanceID()}] ENTRY OnEnemyAttackImpact drive={useEngineDrive}");
            if (!useEngineDrive) return;

            if (enemyProjectilePrefab && enemyMuzzle && enemyIsRanged)
        {
                Debug.Log("[BVC] OnEnemyAttackImpact - spawning projectile");
                var target = playerRoot ? playerRoot.position : transform.position;
                SpawnAndFlyProjectile(enemyMuzzle, target, enemyProjectilePrefab, projectileTrailSpeed);
            }
            else
            {
                if (playerAnimator) {SafeTrigger(playerAnimator, Param_HitTrigger); Debug.Log("[BVC] Triggered player hit reaction."); }
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
        Debug.Log($"[BVC] SafeTrigger: {trigger}");
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
