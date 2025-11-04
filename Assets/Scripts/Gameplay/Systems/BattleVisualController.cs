// Assets/Scripts/Gameplay/Systems/BattleVisualController.cs
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public partial class BattleVisualController : MonoBehaviour
{
    [Header("World Shift")]
    [SerializeField] Transform worldRoot; // parent of scenery + enemyRoot (NOT UI, NOT playerRoot)
    [System.Serializable] public struct ParallaxLayer { public Transform t; [Range(0f,1f)] public float factor; }
    [SerializeField] ParallaxLayer[] parallax; // back-most factor≈0.2, mid≈0.5, fore≈0.8; worldRoot itself is factor=1.0
    float worldOffsetX = 0f;
    [SerializeField] Transform playerMover;  // under World
    // Optional clamps to avoid scrolling forever (tweak as you like)
    [SerializeField] float worldMinOffset = -20f;
    [SerializeField] float worldMaxOffset =  20f;
    [SerializeField] Transform enemyMover;  // the transform that should equal engineX (parent under World)
    [SerializeField] Transform enemyModel;  // the sprite child (localPosition.x must be 0)
    [Header("Scene Refs")]
    public Transform playerRoot;           // where your player sprite sits
    public Animator playerAnimator;        // player's Animator
    public PlayerClassVisualMap playerVisuals;
    [Header("Debug")]
    [SerializeField] private bool enableDebug = false;
    private bool playerIsRanged;
    private int _cachedEnemyHp;
    private int _cachedEnemyHpMax;
    [Header("Engine drive")]
public bool engineDrivesPositions = true;  // default true
private bool enemyIsRanged;
public float EnemySpawnX => enemySpawnPoint ? enemySpawnPoint.position.x : 0f;
public void SetPlayerRanged(bool v) => playerIsRanged = v;
public void SetEnemyRanged(bool v)  => enemyIsRanged  = v;

    // ===== Engine / Orchestrator hooks (assigned by CombatOrchestrator) =====
    [Header("Engine/Orchestrator Hooks")]
    public bool useEngineDrive = true; // when true, the engine controls attack timing

    // [Header("Approach Tuning")]
    // [SerializeField] private float playerEnemyGap = 0.5f; // meters to stop at (tune this)
    // [SerializeField] private float approachSpeed = 0.2f;  // m/s
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
    [SerializeField] private float projectileTrailSpeed = 50f; // purely visual
    [SerializeField] private float playerProjectileTrailSpeed = 5f;
    [SerializeField] private float enemyProjectileTrailSpeed = 5f;
    [Header("Enemy Spawning")]   
    [SerializeField] private Transform enemySpawnPoint; // spawn location (usually right side)
    [SerializeField] private Transform enemyParent;     // optional parent for cleanliness

    [Header("Boss Settings")]
    // [SerializeField] private int bossEvery = 10;          // every N kills spawn a boss
    [SerializeField] private float bossScaleFactor = 1.25f;
    [SerializeField] private Color bossTint = Color.red;

    [Header("Animator Parameters (Triggers/Bools)")]
    public string Param_AttackTrigger = "Attack";
    public string Param_HitTrigger = "Hit";
    public string Param_DeadBool = "Dead";
    public string Param_WalkTrigger = "Walk";

    public void SetPlayerX(float x)
{
    if (!engineDrivesPositions) return; // guard
    if (!playerMover) return;
    var lp = playerMover.localPosition;
    lp.x = x;
    playerMover.localPosition = lp;
}

public void SetEnemyX(float x)
{
    if (!engineDrivesPositions) return; // guard
    if (!enemyMover) return;
    var lp = enemyMover.localPosition;
    lp.x = x;
    enemyMover.localPosition = lp;
}
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

    [SerializeField] private Transform enemyRoot;  
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

    public void OnPlayerAttackImpactWithETA(float etaSec) 
{
    if (!useEngineDrive) return;
    if (playerProjectilePrefab && playerMuzzle && playerIsRanged && etaSec > 0f)
    {
        SpawnWithETA(playerMuzzle, _currentEnemy ? _currentEnemy.transform : null,
                     playerProjectilePrefab, etaSec);
    }
    else
    {
        // melee/hitscan: just show hit reaction
        if (enemyAnimator) SafeTrigger(enemyAnimator, Param_HitTrigger);
    }
}

public void OnEnemyAttackImpactWithETA(float etaSec)
{
    if (!useEngineDrive) return;
    if (enemyProjectilePrefab && enemyMuzzle && enemyIsRanged && etaSec > 0f)
    {
        SpawnWithETA(enemyMuzzle, playerRoot, enemyProjectilePrefab, etaSec);
    }
    else
    {
        if (playerAnimator) SafeTrigger(playerAnimator, Param_HitTrigger);
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

    public void SetProjectileSpeeds(float playerSpeed, float enemySpeed)
{
    playerProjectileTrailSpeed = playerSpeed;
    enemyProjectileTrailSpeed = enemySpeed;
}
    public void SetPlayerHp(int hp, int maxHp) { /* optional: wire a player bar here */ }

    public void SetEnemyHp(int hp, int max)
    {
        _cachedEnemyHp = hp;
        _cachedEnemyHpMax = max;
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
    private void SpawnAndFlyProjectile(Transform from, Transform target, GameObject prefab, float speed)
{
    if (!prefab || !from) return;
    // Parent under worldRoot so it follows WorldShifter
    var parent = worldRoot ? worldRoot : transform;
    var go = Instantiate(prefab, from.position, Quaternion.identity, parent);
    StartCoroutine(Fly(go.transform, target, speed));
}
private void SpawnWithETA(Transform from, Transform to, GameObject prefab, float etaSec)
{
    if (!prefab || !from || !to) return;

    var parent = worldRoot ? worldRoot : transform;

    // Freeze the target position at fire time (anchor under worldRoot so world shifts carry it)
    var anchor = new GameObject("ProjectileTargetAnchor").transform;
    anchor.SetParent(parent, worldPositionStays: true);
    anchor.position = to.position;

    // Spawn projectile exactly at the muzzle (what the player sees)
    var proj = Instantiate(prefab, from.position, Quaternion.identity, parent).transform;

    // Derive visual speed so arrival time == engine ETA
    float visualDist = Vector3.Distance(from.position, anchor.position);
    float speed = (etaSec > 0f) ? (visualDist / etaSec) : playerProjectileTrailSpeed;

    Debug.Log($"[BVC] VFX spawn (ETA): muzzleX={from.position.x:F2} targetX={anchor.position.x:F2} " +
              $"eta={etaSec:F2}s vDist={visualDist:F2} vSpeed={speed:F2}");

    StartCoroutine(FlyToAnchor(proj, anchor, speed));
}

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
    private IEnumerator Fly(Transform proj, Transform target, float speed)
{
    if (!proj) yield break;
    Vector3 lastKnown = target ? target.position : proj.position;

    while (proj)
    {
        // If we have a live target, re-acquire its current world position each frame
        var to = target ? target.position : lastKnown;
        lastKnown = to;

        // Move in world units/second
        var done = (proj.position - to).sqrMagnitude <= 0.0004f;
        if (done) break;

        proj.position = Vector3.MoveTowards(proj.position, to, speed * Time.deltaTime);
        yield return null;
    }
    if (proj) Destroy(proj.gameObject);
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

            // Wire under the mover (mover lives under worldRoot)
            WireEnemyInstance(_currentEnemy.transform);

            // Reset the MODEL local transform (stays centered under the mover)
            var t = _currentEnemy.transform;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale    = Vector3.one;

            // Apply intended visual scale on the MODEL
            float scaleMul = isBoss ? bossScaleFactor : 1f;
            t.localScale *= def.baseScale * scaleMul;

            // Position the **mover** in world space at spawn X (keeps model centered)
            if (enemyMover)
            {
                var m = enemyMover.position;
                m.x = spawnPos.x;          // edge/off-screen spawn lives on mover.x
                enemyMover.position = m;
            }


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
                _enemyHpBar.maxValue = _cachedEnemyHpMax > 0 ? _cachedEnemyHpMax : 1;
                _enemyHpBar.value    = _cachedEnemyHpMax > 0 ? _cachedEnemyHp     : 1;
            }

            // --- Start approach movement (purely visual) ---
            // StartApproach();

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
    // private void StartApproach()
    // {
    //     if (!playerEngageZone || !enemyBody)
    //     {
    //         Debug.LogWarning("[BVC] Missing colliders for approach; stopping.");
    //         return; // <-- was `yield break;` (illegal in void)
    //     }

    //     Debug.Log("[BVC] Starting enemy approach routine.");
    //     // if (approachCo != null) StopCoroutine(approachCo);
    //     // approachCo = StartCoroutine(ApproachRoutine());
    // }


    // private IEnumerator ApproachRoutine()
    // {
    //     return;
    //     /*
    //     if (_currentEnemy == null || playerRoot == null) yield break;
    //     var enemyT = _currentEnemy.transform;

    //     while (true)
    //     {
    //         float gap = Mathf.Abs(playerRoot.position.x - enemyT.position.x);
    //         // Debug.Log($"[BVC] Approach gap: {gap:F2}m");
    //         if (gap <= playerEnemyGap) yield break;

    //         float dir = Mathf.Sign(playerRoot.position.x - enemyT.position.x);
    //         enemyT.position += new Vector3(dir * approachSpeed * Time.deltaTime, 0f, 0f);
    //         yield return null;
    //     }
    //     */
    // }

    // ----------------- Animation Events -----------------

        public void OnPlayerAttackImpact() // legacy (no ETA)
    {
        Debug.Log($"[BVC#{GetInstanceID()}] ENTRY OnPlayerAttackImpact drive={useEngineDrive}");
            if (!useEngineDrive) return;

            // If ranged (projectileSpeed > 0), spawn projectile; otherwise keep your “Hit” reaction
            // We can detect “ranged” by presence of prefab/muzzle
            if (playerProjectilePrefab && playerMuzzle&& playerIsRanged)
        {
                Debug.Log("[BVC] OnPlayerAttackImpact - spawning projectile");
            var target = _currentEnemy ? _currentEnemy.transform : null;
                Debug.Log($"[BVC] → Projectile speed: {playerProjectileTrailSpeed}");
                float pX = CombatOrchestrator.Instance.PlayerLogicalX;
                float eX = CombatOrchestrator.Instance.EnemyLogicalX;
                SpawnAndFlyProjectile_LockedToLogicalX(playerRoot, _currentEnemy.transform, pX, eX,
                    playerProjectilePrefab, playerProjectileTrailSpeed);
            }
            else
            {
            // melee VFX: cause target hit reaction immediately
            if (enemyAnimator) { SafeTrigger(enemyAnimator, Param_HitTrigger); Debug.Log("[BVC] Triggered enemy hit reaction."); }
            }
        }

    public void OnEnemyAttackImpact() // legacy (no ETA)
    {
        Debug.Log($"[BVC#{GetInstanceID()}] ENTRY OnEnemyAttackImpact drive={useEngineDrive}");
        if (!useEngineDrive) return;

        if (enemyProjectilePrefab && enemyMuzzle && enemyIsRanged)
        {
            Debug.Log("[BVC] OnEnemyAttackImpact - spawning projectile");
            var target = playerRoot ? playerRoot : null;
            float pX = CombatOrchestrator.Instance.PlayerLogicalX;
            float eX = CombatOrchestrator.Instance.EnemyLogicalX;
            SpawnAndFlyProjectile_LockedToLogicalX(_currentEnemy.transform, playerRoot, eX, pX,
                enemyProjectilePrefab, enemyProjectileTrailSpeed);
        }
        else
        {
            if (playerAnimator) { SafeTrigger(playerAnimator, Param_HitTrigger); Debug.Log("[BVC] Triggered player hit reaction."); }
        }
    }


    
    // ----------------- Animator helpers -----------------
    private static Vector3 WithX(Vector3 v, float x) => new Vector3(x, v.y, v.z);

private void SpawnAndFlyProjectile_LockedToLogicalX(
    Transform fromRoot, Transform toRoot,
    float fromLogicalX, float toLogicalX,
    GameObject prefab, float speed)
{
    if (!prefab || !fromRoot || !toRoot) return;

    // parent under world root so world shifts carry both projectile and target anchor
    var parent = worldRoot ? worldRoot : transform;

    // build start/target using the SAME X the engine used (logicalX)
    Vector3 start  = WithX(fromRoot.position, fromLogicalX);
    Vector3 target = WithX(toRoot.position,  toLogicalX);

    // static target anchor (so world shifting doesn't break the aim point)
    var anchor = new GameObject("ProjectileTargetAnchor").transform;
    anchor.SetParent(parent, worldPositionStays: true);
    anchor.position = target;

    // spawn projectile at logical start X
    var proj = Instantiate(prefab, start, Quaternion.identity, parent).transform;

    // (optional) one-time debug to compare with engine logs
    Debug.Log($"[BVC] spawn (locked) startX={start.x:F2} targetX={target.x:F2} speed={speed:F2}");

    StartCoroutine(FlyToAnchor(proj, anchor, speed));
}

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
    // Call this immediately after you instantiate the enemy prefab.
public void WireEnemyInstance(Transform spawned)
{
    if (!worldRoot) { Debug.LogError("[BVC] worldRoot not set."); return; }
    if (!spawned)   { Debug.LogError("[BVC] WireEnemyInstance: spawned is null"); return; }

    // Create the mover once
    if (!enemyMover)
    {
        var moverGO = new GameObject("EnemyMover");
        enemyMover = moverGO.transform;
        enemyMover.SetParent(worldRoot, worldPositionStays: false);
        enemyMover.localPosition = Vector3.zero;
        enemyMover.localRotation = Quaternion.identity;
        enemyMover.localScale    = Vector3.one;
    }

    // Decide which transform is the visual model
    enemyModel = spawned;
    if (!enemyModel.GetComponent<SpriteRenderer>() && !enemyModel.GetComponent<Animator>())
    {
        var sr = enemyModel.GetComponentInChildren<SpriteRenderer>(true);
        if (sr) enemyModel = sr.transform;
    }

    // *** IMPORTANT PART ***
    // Parent the spawned ROOT to the mover and ZERO its local transform
    spawned.SetParent(enemyMover, worldPositionStays: false);   // <-- false
    spawned.localPosition = Vector3.zero;
    spawned.localRotation = Quaternion.identity;
    spawned.localScale    = Vector3.one;

    // Keep the model centered as well (safety)
    if (enemyModel)
    {
        var lp = enemyModel.localPosition; lp.x = 0f; enemyModel.localPosition = lp;
    }

    Debug.Log($"[BVC] Wired enemy: mover={enemyMover.name} model={enemyModel.name}");
}

}
