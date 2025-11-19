using UnityEngine;
using Core.Combat;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using Gameplay.Systems;

public class CombatOrchestrator : MonoBehaviour
{
    private static CombatOrchestrator _instance;
    public static CombatOrchestrator Instance => _instance;

    [Header("Refs")]
    public PlayerSpaceCoordinator coordinator;
    public BattleVisualController visuals;
    public GameLoopService gameLoop;
    public PlayerProgression playerProgression;
    [Header("Enemy UI")]
    [SerializeField] private Slider enemyHpBarPrefab;
    [SerializeField] private float enemyHpBarScale = 0.1f;

    [Header("Player Auto-attack")]
    [SerializeField] private float playerAttackPeriod = 1.0f;   // seconds
    [SerializeField] private float playerMeleeReach = 1.6f;
    
    [Header("Player Attack Profiles")]
    [SerializeField] private AttackProfile warriorProfile;
    [SerializeField] private AttackProfile mageProfile;
    [SerializeField] private AttackProfile archerProfile;
    private AttackProfile playerAttackProfile; // assign Warrior/Mage/Archer profile

    [Header("Enemy Movement")]
    [SerializeField] private float multiEnemyApproachSpeed = 0.6f; // units/sec
    [SerializeField] private float multiDesiredGap = 1.2f;
    [SerializeField] private Vector2 chaseDelayRange = new(0.2f, 0.6f);
    [Header("Player Movement Tracking")]
    float _lastPlayerX;
    float _playerSpeed;
    
    [SerializeField] float playerRespawnDelay = 1.0f;
    [SerializeField] float playerRespawnX     = -2.4f;
    bool _playerRespawning;

    [Header("Debug")]
    [SerializeField] private bool enableDebug = false;

    // Engine + runtime
    private CombatEngine _engine;
    public PlayerCombatStats CombatStats { get; private set; }
    private readonly Dictionary<int, EnemyUnit> _enemyViews = new();
    public  IReadOnlyDictionary<int, EnemyUnit> EnemyViews => _enemyViews;
    public  EnemyUnit CurrentTarget { get; private set; }
    private int _pendingTargetId = -1;

public CombatEngine DebugEngine => _engine;
public IEnumerable<EnemyUnit> Enemies => _enemyViews.Values;

// Old helper used by SkillGate
public float EnemyLogicalX() => CurrentTarget != null ? CurrentTarget.posX : (_engine != null ? _engine.EnemyProxyX : 0f);

// Old helper used by EnemySkillRunner
public bool CanEnemyAct(EnemyUnit u) {
    if (u == null) return false;
    return u.hp > 0 && u.stunTimer <= 0f; // expand if you have respawn flags, etc.
}
    // cache
    public int PlayerAtk => gameLoop?.Player?.Atk ?? 0;
    public float PlayerX  => coordinator ? coordinator.GetLogicalX() : 0f;

    void Awake()
    {
        if (_instance && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }
    void OnDestroy()
    {
        if (_instance == this) _instance = null;
        if (gameLoop?.Player != null)
            gameLoop.Player.OnStatsChanged -= HandlePlayerStatsChanged;
    }

    private IEnumerator Start()
    {
        yield return new WaitUntil(() => gameLoop && gameLoop.IsInitialized && gameLoop.StatsReady && FirebaseGate.IsReady);
        visuals.ApplyPlayerClass(gameLoop.Player.ClassId);
        playerAttackProfile = SelectProfile(gameLoop.Player.ClassId);
        if (!playerProgression)
        {
            playerProgression = FindObjectOfType<PlayerProgression>();
            if (!playerProgression)
                Debug.LogWarning("[CombatOrchestrator] PlayerProgression not assigned; XP gains will be skipped.");
        }
        // Seed engine
        var p = new FighterState
        {
            Level = gameLoop.Player.Level,
            Hp    = gameLoop.Player.Hp,
            MaxHp = gameLoop.Player.MaxHp,
            Atk   = gameLoop.Player.Atk,
            Def   = gameLoop.Player.Def,
            PosX  = PlayerX
        };
        var cfg = new EngineConfig { PlayerAttackRateSec = playerAttackPeriod, XpRewardOnKill = gameLoop.Enemy.XpReward };
        _engine = new CombatEngine(p, cfg, HandleEvent);
        _engine.PlayerReach = playerAttackProfile ? playerAttackProfile.reach : playerMeleeReach; // fallback
        _lastPlayerX = _engine.Player.PosX;
        _playerSpeed = 0f;
        UpdatePlayerCadence();


        // Spawn first enemy from GameLoop
        var spawnX = visuals.EnemySpawnX;
        float gap = 1.4f;
        for (int i = 0; i < 3; i++)
        {
            gameLoop.RollNextEnemy(out var def, out var isBoss); // NEW: per-spawn roll
            var pos = new Vector3(spawnX + gap * i, 0f, 0f);
            SpawnEnemyFromDef(def, isBoss, pos);
        }
        Retarget();
        visuals.useEngineDrive = true;

        if (gameLoop?.Player != null)
        {
            gameLoop.Player.OnStatsChanged += HandlePlayerStatsChanged;
            HandlePlayerStatsChanged();
        }
    }

    void Update()
    {
        if (_engine == null) return;
        float dt = Time.deltaTime;

        // Player logical X from PSC → engine
        var pl = _engine.Player; pl.PosX = PlayerX; _engine.Player = pl;
        visuals.SetPlayerX(pl.PosX);
        // NEW IMPROVEMENT: Player speed tracking for anims
        // compute horizontal speed (units/sec)
        float vx = (pl.PosX - _lastPlayerX) / Time.deltaTime;
        _lastPlayerX = pl.PosX;

        // damp to avoid flicker
        _playerSpeed = Mathf.Lerp(_playerSpeed, Mathf.Abs(vx), 0.25f);

        // push to animator
        var pa = visuals.playerAnimator;
        if (pa) pa.SetFloat("Speed", _playerSpeed);

        // Enemy approach (engine is source of truth for positions)
        var snapshot = new List<KeyValuePair<int, EnemyUnit>>(_enemyViews);
        foreach (var kv in snapshot)
        {
            int eid = kv.Key; var u = kv.Value;
            if (u == null || u.hp <= 0 || !u.view) continue;

            if (!_engine.TryGetEnemy(eid, out var fs)) continue;

            // countdown stun & chase delay
            if (u.stunTimer > 0f) { u.stunTimer -= dt; if (u.stunTimer < 0f) u.stunTimer = 0f; }
            if (u.chaseDelayTimer > 0f) u.chaseDelayTimer -= dt;

            // move if not stunned/delayed
            if (u.stunTimer <= 0f && u.chaseDelayTimer <= 0f)
            {
                float dir  = Mathf.Sign(PlayerX - fs.PosX); if (dir == 0f) dir = 1f;
                float stop = PlayerX - dir * multiDesiredGap;
                fs.PosX = Mathf.MoveTowards(fs.PosX, stop, multiEnemyApproachSpeed * dt);
            }

            _engine.UpdateEnemy(eid, fs);
            // sync view
            var lp = u.view.localPosition; lp.x = fs.PosX; u.view.localPosition = lp;
            u.posX = fs.PosX;
            float evx = (fs.PosX - u.lastX) / dt;
            u.lastX = fs.PosX;
            u.animSpeed = Mathf.Lerp(u.animSpeed, Mathf.Abs(evx), 0.25f);
            if (u.animator) u.animator.SetFloat("Speed", u.animSpeed);
            if (u.hp != fs.Hp)
            {
                u.hp = fs.Hp;
                if (u.hpBar)
                {
                    float normalized = (u.maxHp > 0) ? Mathf.Clamp01((float)u.hp / u.maxHp) : 0f;
                    u.hpBar.value = normalized;
                }
            }
            if (!u.deathStarted && fs.Hp <= 0)
            {
                u.deathStarted = true;                      // mark once
                _enemyViews[eid] = u;                       // IMPORTANT if EnemyUnit is a struct
                StartCoroutine(CoRemoveEnemy(eid, u));
                continue;
            }
        }

        // Keep engine proxy range target at current target X (or disable if none)
        _engine.EnemyProxyX = (CurrentTarget != null) ? CurrentTarget.posX : (_engine.Player.PosX + 9999f);

        // Engine tick (auto-attack cadence, stuns decay)
        _engine.Tick(dt);

        // Keep target fresh
        Retarget();
    }

    // ---------- Spawning / registration ----------
    public EnemyUnit SpawnEnemyFromDef(MonsterDef def, bool isBoss, Vector3 worldPos)
    {
        int hp = isBoss ? Mathf.RoundToInt(def.hp * 3.0f) : def.hp;
        int atk = isBoss ? Mathf.RoundToInt(def.atk * 1.8f) : def.atk;
        int df = isBoss ? Mathf.RoundToInt(def.def * 2.0f) : def.def;

        var fs = new FighterState { Level = Mathf.Max(1, gameLoop.Player.Level), Hp = hp, MaxHp = hp, Atk = atk, Def = df, PosX = worldPos.x };
        int eid = _engine.AddEnemy(fs);

        var mover = visuals.SpawnEnemyView(def, isBoss, worldPos);
        if (!mover) { _engine.RemoveEnemy(eid); return null; }

        var unit = new EnemyUnit
        {
            enemyId = eid,
            view = mover,
            def = def,
            posX = worldPos.x,
            hp = hp,
            maxHp = hp,
            atk = atk,
            defStat = df,
            hpBar = SpawnEnemyHpBar(mover),
            chaseDelayTimer = Random.Range(chaseDelayRange.x, chaseDelayRange.y)
        };
        unit.deathStarted = false;
        unit.lastX = worldPos.x;
        unit.animator = mover.GetComponentInChildren<Animator>(true);
        if (unit.hpBar)
        {
            unit.hpBar.minValue = 0f;
            unit.hpBar.maxValue = 1f;
            unit.hpBar.value = 1f;
        }

        var esr = mover.GetComponentInChildren<EnemySkillRunner>(true);
        if (esr) { esr.unit = unit; esr.enabled = true; }

        _enemyViews[eid] = unit;
        return unit;
    }

    private Slider SpawnEnemyHpBar(Transform parent)
    {
        if (!enemyHpBarPrefab || !parent) return null;
        var bar = Instantiate(enemyHpBarPrefab, parent);
        var rect = bar.transform as RectTransform;
        rect.localScale = Vector3.one * Mathf.Max(0.0001f, enemyHpBarScale);
        rect.localPosition = new Vector3(0f, 0.2f, 0f);
        bar.gameObject.SetActive(true);
        return bar;
    }
    private AttackProfile SelectProfile(string classId)
    {
        if (string.IsNullOrEmpty(classId)) return warriorProfile;
        switch (classId.ToLowerInvariant())
        {
            case "warrior": return warriorProfile ? warriorProfile : playerAttackProfile;
            case "mage":    return mageProfile    ? mageProfile    : playerAttackProfile;
            case "archer":  return archerProfile  ? archerProfile  : playerAttackProfile;
            default:        return warriorProfile ? warriorProfile : playerAttackProfile;
        }
    }
    private IEnumerator Co_PlayerImpactFromProfile(int lockedEnemyId)
    {
        var prof = playerAttackProfile;
        if (prof == null) yield break;

        // windup
        if (prof.windup > 0f) yield return new WaitForSeconds(prof.windup);
        
        // resolve target
        EnemyUnit u = null;
        if (lockedEnemyId != -1) _enemyViews.TryGetValue(lockedEnemyId, out u);
        if (u == null) u = CurrentTarget;
        if (u == null || u.view == null) yield break;

        // reach gate (for melee/hitscan)
        float px = PlayerX;
        float dx = Mathf.Abs(u.posX - px);
        if ((prof.kind == AttackKind.Melee || prof.kind == AttackKind.Hitscan) && dx > prof.reach) yield break;

        // projectile path (spawn trail, wait ETA)
        if (prof.kind == AttackKind.Projectile)
        {
            var from = visuals.PlayerMuzzle ? visuals.PlayerMuzzle : visuals.playerRoot; // fallback
            var to   = u.view;
            float dist = Mathf.Abs(to.position.x - from.position.x);
            float eta  = (prof.projectileSpeed > 0.001f) ? dist / prof.projectileSpeed : 0.15f;

            // use prefab from the AttackProfile
            visuals.SpawnSkillProjectile(from, to, prof.projectilePrefab, eta);
            yield return new WaitForSeconds(eta);
        }

        // apply damage
        ApplyDamageToEnemy(u.enemyId, PlayerAtk);

        // small hit anim on enemy
        var ea = u.view.GetComponentInChildren<Animator>(true);
        if (ea) ea.SetTrigger(visuals.Param_HitTrigger);
    }


    // ---------- Targeting ----------
    public void Retarget() => CurrentTarget = PickNearestAlive();
    public EnemyUnit PickNearestAlive()
    {
        float px = PlayerX; EnemyUnit best = null; float bestD = float.MaxValue;
        foreach (var u in _enemyViews.Values)
        {
            if (u == null || u.hp <= 0) continue;
            float d = Mathf.Abs(u.posX - px);
            if (d < bestD) { bestD = d; best = u; }
        }
        return best;
    }

    public float   GetEnemyLogicalX(EnemyUnit u) => u?.posX ?? 0f;
    public float   GetPlayerLogicalX() => PlayerX;
    public Transform GetTargetEnemyTransform() => CurrentTarget?.view;
    public Vector3   GetTargetEnemyWorldPos() => (CurrentTarget?.view != null) ? CurrentTarget.view.position : visuals.GetEnemyWorldPosition();

    // ---------- Damage/CC (engine-centered) ----------
    public void ApplyDamageToEnemy(int enemyId, int raw)
    {
        if (!_enemyViews.TryGetValue(enemyId, out var u)) return;
        int mitigated = Mathf.Max(0, raw - u.defStat);
        if (u.shield > 0) { var blk = Mathf.Min(u.shield, mitigated); u.shield -= blk; mitigated -= blk; }
        if (mitigated <= 0) return;

        _engine.DealDamageToEnemy(enemyId, mitigated);
        if (_engine.TryGetEnemy(enemyId, out var fs))
        {
            u.hp = fs.Hp;
            if (u.hpBar) { u.hpBar.maxValue = u.maxHp; u.hpBar.value = u.hp; }
            if (fs.Hp == 0) StartCoroutine(CoRemoveEnemy(enemyId, u));
        }
    }
    public void ApplyDamageToEnemy(EnemyUnit u, int raw) => ApplyDamageToEnemy(u.enemyId, raw);

    public void CO_ApplyDamageToPlayer(int amount)
    {
        if (_playerRespawning || _engine == null) return;
        if (_engine.Player.Hp <= 0) return; // already dead
        _engine.DealDamageToPlayer(amount);
        ForceRefreshHpUI_Player();
    }
    public void ApplyDotToEnemy(int enemyId, int dmgPerTick, float duration, float tickEvery)
    => _engine?.ApplyDotToEnemy(enemyId, dmgPerTick, duration, tickEvery);

    public void ApplyDotToEnemy(EnemyUnit u, int dmgPerTick, float duration, float tickEvery)
    { if (u != null) ApplyDotToEnemy(u.enemyId, dmgPerTick, duration, tickEvery); }

    public void ApplyDotToPlayer(int dmgPerTick, float duration, float tickEvery)
        => _engine?.ApplyDotToPlayer(dmgPerTick, duration, tickEvery);
    public void CO_HealPlayer(int amount)         { _engine?.HealPlayer(amount); }
    public void CO_AddShieldToPlayer(int amount)  { _engine?.AddShieldToPlayer(amount); }
    public void CO_StunPlayer(float seconds)      { _engine?.StunPlayer(seconds); }

    public void CO_StunEnemy(EnemyUnit u, float seconds) { if (u == null || seconds <= 0f) return; u.stunTimer = Mathf.Max(u.stunTimer, seconds); }
    public void KnockbackEnemy(EnemyUnit u, float dx)
    {
        if (u == null) return;
        if (_engine.TryGetEnemy(u.enemyId, out var fs))
        {
            fs.PosX += dx;
            _engine.UpdateEnemy(u.enemyId, fs);
            u.posX = fs.PosX;
            // smooth to avoid flicker
        }
    }
    public void KnockbackPlayer(float dx)
    {
        if (coordinator == null) return;
        coordinator.RequestPlayerDisplacement(dx);
    }

    // ---------- Engine event sink ----------
    private void HandleEvent(in CombatEvent evt)
    {
        switch (evt.Type)
        {
            case CombatEventType.AttackStarted:
                if (evt.Actor == Side.Player)
                {
                    _pendingTargetId = (CurrentTarget != null) ? CurrentTarget.enemyId : -1;
                    visuals.TriggerPlayerAttack();
                    if (playerAttackProfile)
                        StartCoroutine(Co_PlayerImpactFromProfile(_pendingTargetId));
                }
                break;

            case CombatEventType.AttackImpact:
                if (evt.Actor == Side.Player)
                {
                    if (!playerAttackProfile) // legacy instant hit
                    {
                        EnemyUnit tgt = null;
                        if (_pendingTargetId != -1) _enemyViews.TryGetValue(_pendingTargetId, out tgt);
                        if (tgt == null) tgt = CurrentTarget;
                        _pendingTargetId = -1;
                        if (tgt != null) ApplyDamageToEnemy(tgt.enemyId, PlayerAtk);
                    }
                }
                break;

            case CombatEventType.DamageApplied:
                if (evt.Actor == Side.Enemy)
                    visuals.TriggerPlayerHit();
                    ForceRefreshHpUI_Player();
                break;

            case CombatEventType.UnitDied:
                if (evt.Actor == Side.Player)
                    if (!_playerRespawning)
                    StartCoroutine(Co_PlayerRespawn());
                break;

        }
    }
    IEnumerator Co_PlayerRespawn()
    {
        if (_playerRespawning) yield break;
        _playerRespawning = true;

        // stop further deaths during delay
        var p = _engine.Player;
        p.Hp = 0;                 // mark dead once
        _engine.Player = p;

        visuals.OnPlayerDied();   // sets Dead = true

        yield return new WaitForSeconds(playerRespawnDelay);

        // revive + reset
        p = _engine.Player;
        p.Hp = p.MaxHp;
        p.StunTimer = 0f;
        p.PosX = playerRespawnX;
        _engine.Player = p;

        coordinator.SetLogicalX(playerRespawnX);
        visuals.OnPlayerRespawn();  // sets Dead=false and clears triggers
        visuals.SetPlayerX(playerRespawnX);
        ForceRefreshHpUI_Player();

        _playerRespawning = false;
    }

    // ---------- Cleanup ----------
    private IEnumerator CoRemoveEnemy(int enemyId, EnemyUnit u)
    {
        // death anim
        var anim = u.view ? u.view.GetComponentInChildren<Animator>(true) : null;
        if (anim) anim.SetBool(visuals.Param_DeadBool, true);

        // XP: grant via PlayerProgression (single source of truth)
        int xp = u.def ? u.def.xpReward : 0;
        if (xp > 0 && playerProgression)
        {
            playerProgression.AddXp(xp);
        }
        else if (xp > 0 && !playerProgression)
        {
            Debug.LogWarning("[CombatOrchestrator] Cannot award XP because PlayerProgression is missing.");
        }

        yield return new WaitForSeconds(0.6f);

        _engine.RemoveEnemy(enemyId);
        _enemyViews.Remove(enemyId);
        if (u.view) Destroy(u.view.gameObject);
        Retarget();
    }

    // ---------- UI ----------
    public void ForceRefreshHpUI_Player()
    {
        // if you have a UI widget, update here; left blank intentionally
    }

    public void ApplyCombatStats(PlayerCombatStats stats)
    {
        CombatStats = stats;
        UpdatePlayerCadence();
    }

    private void UpdatePlayerCadence()
    {
        if (_engine == null) return;
        float basePeriod = playerAttackProfile ? playerAttackProfile.period : playerAttackPeriod;
        float multiplier = Mathf.Max(0.1f, 1f + CombatStats.attackSpeedAA);
        _engine.PlayerPeriodSec = basePeriod / multiplier;
    }

    private void HandlePlayerStatsChanged()
    {
        if (_engine == null || gameLoop?.Player == null) return;
        var ps = gameLoop.Player;
        var enginePlayer = _engine.Player;
        enginePlayer.MaxHp = ps.MaxHp;
        enginePlayer.Hp = Mathf.Min(enginePlayer.Hp, enginePlayer.MaxHp);
        enginePlayer.Atk = ps.Atk;
        enginePlayer.Def = ps.Def;
        _engine.Player = enginePlayer;
    }
}
