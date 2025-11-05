using UnityEngine;
using Core.Combat;
using System.Collections;

public class CombatOrchestrator : MonoBehaviour
{
    // Singleton instance
    private static CombatOrchestrator _instance;
    public PlayerSpaceCoordinator coordinator;
    public static CombatOrchestrator Instance => _instance;
public float PlayerLogicalX => _engine?.Player.PosX ?? 0f;
    public float EnemyLogicalX => _engine?.Enemy.PosX ?? 10f;
public int   PlayerAtk => _engine?.Player.Atk ?? 0;
[SerializeField] private bool  engineMovesEnemy = true;
[SerializeField] private float desiredGap = 3f;     // units
[SerializeField] private float enemyApproachSpeed = 3f; // units/sec (your old “Approach Speed”)
    void Awake()
    {
        if (_instance && _instance != this)
        {
            Debug.LogWarning($"[CO] Duplicate detected (old={_instance.GetInstanceID()}, new={GetInstanceID()}) — destroying new.");
            Destroy(gameObject);
            return;
        }
        _instance = this;
        // Optional if you change scenes:
        // DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }
//     public void SyncPositionsToEngineNow()
// {
//     // visual → engine
//     var p = _engine.Player;
//     p.PosX = visuals.PlayerPosX;  // your PSC.GetLogicalX()
//     _engine.UpdatePlayer(p);

//     var e = _engine.Enemy;
//     e.PosX = visuals.EnemyPosX;   // however you currently mirror enemy
//     _engine.UpdateEnemy(e);
// }

    // If you use Enter Play Mode Options (domain reload OFF):
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() => _instance = null;

    [Header("References")]
    public BattleVisualController visuals; // assign in inspector
    public GameLoopService gameLoop;       // existing owner of Player/Enemy/Class/XpTable
    public float PlayerX => _engine?.Player.PosX ?? 0f;
    public float EnemyX  => _engine?.Enemy.PosX  ?? 0f;
    [Header("Debug Settings")]
    [SerializeField] private bool enableDebug = true;
    // [SerializeField] private float playerAttackRate = 2.5f;
    // [SerializeField] private float enemyAttackRate = 3.0f;
    // Fields (assign via inspector)
    [Header("Attack Profiles")]
    [SerializeField] private AttackProfile warriorAttackProfile;
    [SerializeField] private AttackProfile mageAttackProfile;
    [SerializeField] private AttackProfile archerAttackProfile;
    [SerializeField] private AttackProfile enemyAttackProfile;
    [SerializeField] PlayerProgression playerProgression;  // <— add
    [SerializeField] private PlayerPersistenceService playerPersistenceService;
    [SerializeField] XpTable xpTable;                      // optional if PlayerProgression has it already
    private CombatEngine _engine;
    [SerializeField] private float enemyDeathDelay  = 3f;
    [SerializeField] private float playerDeathDelay = 3f;
    private bool _enemyRespawning;
    private bool _playerRespawning;
    public CombatEngine DebugEngine => _engine; // used by debug panels
    // Freeze enemy position after death
    private float _enemyDeathX = 0f;   // where the dead body should stay
    public void CO_ApplyDamageToEnemy(int amount) => _engine?.ApplyPureDamageToEnemy(amount);
    public void CO_ApplyDamageToPlayer(int amount) => _engine?.ApplyPureDamageToPlayer(amount);
    public void CO_HealPlayer(int amount)         => _engine?.HealPlayer(amount);
    public void CO_AddShieldToPlayer(int amount)  => _engine?.AddShieldToPlayer(amount);
    public void CO_StunEnemy(float seconds)       => _engine?.StunEnemy(seconds);
    public void CO_KnockbackEnemy(float dx)       => _engine?.KnockbackEnemy(dx);
    private IEnumerator Start()
    {
        // coordinator.AnchorPlayerVisuallyToLeft();
        Debug.Log($"[CO] Started instance id={GetInstanceID()}");
        // Wait for Firebase + GameLoop ready
        yield return new WaitUntil(() =>
    gameLoop != null && gameLoop.IsInitialized && gameLoop.StatsReady && FirebaseGate.IsReady);

        // Apply player class visuals
        visuals.ApplyPlayerClass(gameLoop.Player.ClassId);

        // === Seed engine state from GameLoop data ===
        var p = new FighterState
        {
            Level = gameLoop.Player.Level,
            Hp = gameLoop.Player.Hp,
            MaxHp = gameLoop.Player.MaxHp,
            Atk = gameLoop.Player.Atk,
            Def = gameLoop.Player.Def,
            Xp = gameLoop.Player.CurrentXp
        };
        Debug.Log($"[CombatOrchestrator] Player stats: Lv {p.Level} HP {p.Hp}/{p.MaxHp} ATK {p.Atk} DEF {p.Def} XP {p.Xp}");
        var e = new FighterState
        {
            Level = gameLoop.Enemy.Level,
            Hp = gameLoop.Enemy.Hp,
            MaxHp = gameLoop.Enemy.HpMax,
            Atk = gameLoop.Enemy.Atk,
            Def = gameLoop.Enemy.Def
        };
        Debug.Log($"[CombatOrchestrator] Enemy stats: Lv {e.Level} HP {e.Hp}/{e.MaxHp} ATK {e.Atk} DEF {e.Def}");
        var cfg = new EngineConfig
        {
            // PlayerAttackRateSec = playerAttackRate,
            // EnemyAttackRateSec = enemyAttackRate
        };

        _engine = new CombatEngine(p, e, cfg, HandleEvent);
        // === Assign attack profiles (from the Inspector or resources) ===
        _engine.PlayerAttack = GetPlayerAttackProfile();
        _engine.EnemyAttack = enemyAttackProfile;   // Drag this one in the Inspector
        float pSpeed = _engine.PlayerAttack?.projectileSpeed ?? 5f;
        float eSpeed = _engine.EnemyAttack?.projectileSpeed ?? 5f;
        visuals.SetProjectileSpeeds(pSpeed, eSpeed);
        // Choose your start X
        const float START_X = -2.4f;

        // 1) Seed PSC (gameplay truth)
        coordinator.SetLogicalX(START_X);

        // 2) Push into engine once
        var p0 = _engine.Player;
        p0.PosX = coordinator.GetLogicalX();
        _engine.UpdatePlayer(p0);

        // 3) Place visuals immediately (no 1-frame snap)
        visuals.SetPlayerX(p0.PosX);

        Debug.Log($"[CO] EnemyAttack projectileSpeed={_engine.EnemyAttack?.projectileSpeed}");
        Debug.Log($"[CO] enemyIsRanged set to {(_engine.EnemyAttack?.projectileSpeed ?? 0f) > 0f}");
        visuals.SetPlayerRanged((_engine.PlayerAttack?.projectileSpeed ?? 0f) > 0f);
        visuals.SetEnemyRanged((_engine.EnemyAttack?.projectileSpeed ?? 0f) > 0f);
        SyncEngineFromGameLoop();
        // === XP reward from GameLoop ===
        var xp = gameLoop.Enemy.XpReward;
        _engine.Config.XpRewardOnKill = xp;
        // Spawn visuals for the data-selected def (no randomness in BVC)

        visuals.SpawnEnemyFromDef(gameLoop.CurrentMonsterDef, gameLoop.CurrentIsBoss);
        visuals.SetEnemyHp(gameLoop.Enemy.Hp, gameLoop.Enemy.HpMax);
        // Seed engine logical X to match the visual spawn X (so Update won't snap it back)
        var e0 = _engine.Enemy;
        e0.PosX = visuals.EnemySpawnX;   // uses the helper you added above
        _engine.UpdateEnemy(e0);

        visuals.useEngineDrive = true;
        // ----- Progression wiring -----
        var prog = playerProgression;
        if (prog)
        {
            // Initialize XP bar from progression
            visuals.SetXp(prog.XpIntoLevel, gameLoop.XpTable.GetXpToNextLevel(prog.Level));

            // Handle level-ups
            prog.OnLevelUp += newLevel =>
            {
                Debug.Log($"[Progression] LEVEL UP → {newLevel}");

                // 1) Recalculate player stats for the new level (and heal to full)
                gameLoop.Player.ApplyLevelAndRecalculate(newLevel, healToFull: true);

                // 2) Refresh UI immediately (HP + XP bar)
                visuals.SetPlayerHp(gameLoop.Player.Hp, gameLoop.Player.MaxHp);
                visuals.SetXp(prog.XpIntoLevel, gameLoop.XpTable.GetXpToNextLevel(newLevel));

                // 3) Update the combat engine’s cached player state so combat uses new stats
                var currentPosX = _engine.Player.PosX;        // <— keep position
                _engine.UpdatePlayer(new FighterState
                {
                    Level = gameLoop.Player.Level,
                    Hp = gameLoop.Player.Hp,
                    MaxHp = gameLoop.Player.MaxHp,
                    Atk = gameLoop.Player.Atk,
                    Def = gameLoop.Player.Def,
                    Xp = prog.XpIntoLevel,
                    PosX = currentPosX
                });

                // 4) Persist to Firebase                
                if (playerPersistenceService) _ = playerPersistenceService.SaveProgressAsync();
            };


        }
    }
    
    
    private AttackProfile GetPlayerAttackProfile()
    {
        // Works for enum, string, or int class ids
        var idStr = gameLoop.Player.ClassId != null
            ? gameLoop.Player.ClassId.ToString()
            : string.Empty;

        // Try friendly names first (enum/string), then common numeric ids as fallback
        if (string.Equals(idStr, "Warrior", System.StringComparison.OrdinalIgnoreCase) || idStr == "0")
            return warriorAttackProfile;

        if (string.Equals(idStr, "Mage", System.StringComparison.OrdinalIgnoreCase) || idStr == "1")
            return mageAttackProfile;

        if (string.Equals(idStr, "Archer", System.StringComparison.OrdinalIgnoreCase) || idStr == "2")
            return archerAttackProfile;

        // Default fallback
        return warriorAttackProfile;
    }


    private void SyncEngineFromGameLoop()
    {
        if (_engine == null || gameLoop == null) return;

        // Player
        _engine.UpdatePlayer(new Core.Combat.FighterState
        {
            Level = gameLoop.Player.Level,
            Hp = gameLoop.Player.Hp,
            MaxHp = gameLoop.Player.MaxHp,
            Atk = gameLoop.Player.Atk,
            Def = gameLoop.Player.Def,
            Xp = gameLoop.Player.CurrentXp
        });

        // Enemy
        _engine.UpdateEnemy(new Core.Combat.FighterState
        {
            Level = gameLoop.Enemy.Level,
            Hp = gameLoop.Enemy.Hp,
            MaxHp = gameLoop.Enemy.HpMax,
            Atk = gameLoop.Enemy.Atk,
            Def = gameLoop.Enemy.Def
        });
    }
    private void SyncGameLoopFromEngine()
    {
        if (_engine == null || gameLoop == null) return;

        // Player
        gameLoop.Player.Level = _engine.Player.Level;
        gameLoop.Player.Hp    = _engine.Player.Hp;
        // gameLoop.Player.MaxHp = _engine.Player.MaxHp;
        gameLoop.Player.Atk   = _engine.Player.Atk;
        gameLoop.Player.Def   = _engine.Player.Def;
        // If you store XP on engine, mirror that too:
        gameLoop.Player.CurrentXp = _engine.Player.Xp;

        // Enemy
        gameLoop.Enemy.Level = _engine.Enemy.Level;
        gameLoop.Enemy.Hp    = _engine.Enemy.Hp;
        // gameLoop.Enemy.HpMax = _engine.Enemy.MaxHp;
        gameLoop.Enemy.Atk   = _engine.Enemy.Atk;
        gameLoop.Enemy.Def   = _engine.Enemy.Def;

        // Optional: notify persistence/UI layers
        // gameLoop.OnCombatSynced?.Invoke();
    }
    void Update()
{

        if (_engine == null) return;
    // if ((Time.frameCount % 20) == 0)
    // Debug.Log($"[CO] pX={_engine.Player.PosX:F2} eX={_engine.Enemy.PosX:F2}");
    float dt = Time.deltaTime;

    // 1) Visual -> Engine (player logical from PSC)
    var p = _engine.Player;
    p.PosX = coordinator.GetLogicalX();
    _engine.UpdatePlayer(p);

    // 2) Optional: simple enemy gap AI in logical space (CO only)
    var e = _engine.Enemy;
    if (engineMovesEnemy && !_enemyRespawning)
    {
        float dir = Mathf.Sign(p.PosX - e.PosX);
        if (dir == 0f) dir = 1f;  // tie-breaker to avoid NaN

        // target keeps 'desiredGap' away from player
        float target = p.PosX - dir * desiredGap;

        // clamp movement by approach speed
        float next = Mathf.MoveTowards(e.PosX, target, enemyApproachSpeed * dt);

        // (optional) arena clamp if you have one:
        // next = Mathf.Clamp(next, arenaMinX, arenaMaxX);

        e.PosX = next;
    }
    _engine.UpdateEnemy(e);

    // 3) Run combat logic (casts, projectiles, timers)
    _engine.Tick(dt);

    // 4) Engine -> Visuals (single source of placement)
    visuals.SetPlayerX(_engine.Player.PosX);
    if (_enemyRespawning)
        visuals.SetEnemyX(_enemyDeathX);   // keep the corpse where it fell
    else
        visuals.SetEnemyX(_engine.Enemy.PosX);
}
public void ForceRefreshHpUI_Player()
{
        if (_engine == null || visuals == null) return;
    Debug.Log($"[CO] ForceRefreshHpUI_Player: Hp={_engine.Player.Hp}, MaxHp={_engine.Player.MaxHp}");
    visuals.SetPlayerHp(_engine.Player.Hp, _engine.Player.MaxHp);
}

    private bool IsPlayerRanged() => (_engine.PlayerAttack?.projectileSpeed ?? 0f) > 0f;
    private bool IsEnemyRanged() => (_engine.EnemyAttack?.projectileSpeed ?? 0f) > 0f;
    private void HandleEvent(in CombatEvent evt)
    {
        Debug.Log($"[CO#{GetInstanceID()}] HandleEvent type={evt.Type} actor={evt.Actor}");
        if (enableDebug)
           
        switch (evt.Type)
        {
            case CombatEventType.AttackStarted:
                if (evt.Actor == Side.Player)
                {
                        visuals.TriggerPlayerAttack();
                }
                else
                {
                    visuals.TriggerEnemyAttack();
                }
                break;

            case CombatEventType.DamageApplied:
                // Update HP bars & data models
                SyncGameLoopFromEngine();
                visuals.SetPlayerHp(gameLoop.Player.Hp, gameLoop.Player.MaxHp);
                visuals.SetEnemyHp(gameLoop.Enemy.Hp, gameLoop.Enemy.HpMax);
                    break;
                
            case CombatEventType.AttackImpact:
                    {
                        try
                        {
                            Debug.Log($"[CO] AttackImpact routed: {evt.Actor} ETA={evt.ProjectileETA:F2}s (useEngineDrive={visuals.useEngineDrive})");
                            if (evt.Actor == Side.Player)
                                visuals.OnPlayerAttackImpactWithETA(evt.ProjectileETA);
                            else
                                visuals.OnEnemyAttackImpactWithETA(evt.ProjectileETA);
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogError($"[CO] Exception in AttackImpact handling: {ex}");
                        }
                        break;
                    }
                case CombatEventType.Healed:
                if (evt.Actor == Side.Player)
                {
                    visuals.SetPlayerHp(_engine.Player.Hp, _engine.Player.MaxHp);
                }
                else
                {
                    visuals.SetEnemyHp(_engine.Enemy.Hp, _engine.Enemy.MaxHp);
                }
                break;
            case CombatEventType.UnitDied:
                    if (evt.Actor == Side.Enemy)
                    {
                        if (_enemyRespawning) break;
                        _enemyRespawning = true;
                        // Visuals: play death state now
                        _enemyDeathX = _engine.Enemy.PosX; // freeze position
                        visuals.SetEnemyHp(gameLoop.Enemy.Hp, gameLoop.Enemy.HpMax);
                        visuals.SetEnemyRanged((_engine.EnemyAttack?.projectileSpeed ?? 0f) > 0f);
                        visuals.OnEnemyDied();

                        // Optional: reflect that HP hit zero on the bar while it’s visible

                        // Delay the respawn logic so death anim can play
                        StartCoroutine(EnemyRespawnRoutine());
                    }
                    else // Player died
                    {
                        if (_playerRespawning) break;
                        _playerRespawning = true;

                        // Visuals: play death state now
                        visuals.OnPlayerDied();

                        // Delay the respawn logic so death anim can play
                        StartCoroutine(PlayerRespawnRoutine());
                    }
                    break;

                case CombatEventType.XpGained:
                    {
                        var prog = playerProgression;
                        if (prog)
                        {
                            prog.AddXp(evt.Amount);
                            visuals.SetXp(prog.XpIntoLevel, gameLoop.XpTable.GetXpToNextLevel(prog.Level));
                        }
                        else
                        {
                            // Fallback if progression missing (keeps current behavior)
                            gameLoop.Player.GainXp(evt.Amount);
                            visuals.SetXp(gameLoop.Player.CurrentXp,
                                gameLoop.XpTable.GetXpToNextLevel(gameLoop.Player.Level));
                        }

                        // Persist (cheap; you also autosave)
                        var ps = playerPersistenceService;
                        if (ps)
                        {
                            // ensure saved values reflect progression (level/xp)
                            var pprog = prog; // capture
                            if (pprog != null)
                            {
                                gameLoop.Player.Level = pprog.Level;
                                gameLoop.Player.CurrentXp = pprog.XpIntoLevel; // keep old schema
                            }
                            _ = ps.SaveProgressAsync();
                        }
                        break;
                    }
                }
                }
    private System.Collections.IEnumerator EnemyRespawnRoutine()
    {
        // Wait death phase
        yield return new UnityEngine.WaitForSeconds(enemyDeathDelay);

        // Pick next enemy and rebuild engine snapshot (exactly as before)
        gameLoop.SpawnEnemy(); // choose new def + boss flag; sets gameLoop.Enemy/* and CurrentMonsterDef/IsBoss */

        _engine.Config.XpRewardOnKill = gameLoop.Enemy.XpReward;

        var ne = new FighterState {
            Level = gameLoop.Enemy.Level,
            Hp    = gameLoop.Enemy.Hp,
            // MaxHp = gameLoop.Enemy.HpMax,
            Atk   = gameLoop.Enemy.Atk,
            Def   = gameLoop.Enemy.Def
            // PosX will be fed from visuals in Update() as usual
        };
        _engine.RespawnEnemy(ne);
        SyncGameLoopFromEngine(); // keep GameLoop in sync
        // Spawn visuals for the newly selected def (keeps stats & visuals in sync)
        visuals.SpawnEnemyFromDef(gameLoop.CurrentMonsterDef, gameLoop.CurrentIsBoss);
        visuals.SetEnemyHp(gameLoop.Enemy.Hp, gameLoop.Enemy.HpMax);
        // Keep engine and visuals aligned on the same starting X
        var e2 = _engine.Enemy;
        e2.PosX = visuals.EnemySpawnX;
        _engine.UpdateEnemy(e2);

        _enemyRespawning = false;
        _enemyDeathX = 0f;
    }

    private System.Collections.IEnumerator PlayerRespawnRoutine()
    {
        yield return new UnityEngine.WaitForSeconds(playerDeathDelay);

        // Reset player HP and engine snapshot (your previous logic)
        gameLoop.Player.Hp = gameLoop.Player.MaxHp;

        // If you manually touched _engine.Player.Hp before, that’s no longer needed here,
        // RespawnPlayer() will ensure the engine-side snapshot is alive:
        _engine.RespawnPlayer();
        SyncGameLoopFromEngine();
        visuals.OnPlayerRespawned();

        _playerRespawning = false;
    }
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
private void AssertEngineIsAuthoritative()
{
    // Example heuristic: engine values must match gameLoop right after a sync-tick
    if (gameLoop.Player.Hp != _engine.Player.Hp || gameLoop.Enemy.Hp != _engine.Enemy.Hp)
        Debug.LogWarning("[CO] Detected desync between engine and game loop — check for direct writes.");
}
}
