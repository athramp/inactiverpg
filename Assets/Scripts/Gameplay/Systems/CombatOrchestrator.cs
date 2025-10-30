using UnityEngine;
using Core.Combat;
using System.Collections;

public class CombatOrchestrator : MonoBehaviour
{
    [Header("References")]
    public BattleVisualController visuals; // assign in inspector
    public GameLoopService gameLoop;       // existing owner of Player/Enemy/Class/XpTable

    [Header("Debug Settings")]
    [SerializeField] private bool enableDebug = true;
    [SerializeField] private float playerAttackRate = 2.5f;
    [SerializeField] private float enemyAttackRate = 3.0f;
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

    private IEnumerator Start()
    {
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
        var e = new FighterState {
            Level  = gameLoop.Enemy.Level,
            Hp     = gameLoop.Enemy.Hp,
            MaxHp  = gameLoop.Enemy.HpMax,
            Atk    = gameLoop.Enemy.Atk,
            Def    = gameLoop.Enemy.Def
        };
        Debug.Log($"[CombatOrchestrator] Enemy stats: Lv {e.Level} HP {e.Hp}/{e.MaxHp} ATK {e.Atk} DEF {e.Def}");
        var cfg = new EngineConfig {
            PlayerAttackRateSec = playerAttackRate,
            EnemyAttackRateSec  = enemyAttackRate
        };

        _engine = new CombatEngine(p, e, cfg, HandleEvent);
        // === Assign attack profiles (from the Inspector or resources) ===
        _engine.PlayerAttack = GetPlayerAttackProfile();
        _engine.EnemyAttack  = enemyAttackProfile;   // Drag this one in the Inspector
        SyncEnginePlayerFromGameLoop();
        // === XP reward from GameLoop ===
        var xp = gameLoop.Enemy.XpReward;
        _engine.Config.XpRewardOnKill = xp;

        if (enableDebug)
        {
            Debug.Log($"[CombatOrchestrator] Engine initialized (XP reward {xp})");
        }
            
        // Spawn visuals for the data-selected def (no randomness in BVC)
        visuals.SpawnEnemyFromDef(gameLoop.CurrentMonsterDef, gameLoop.CurrentIsBoss);

        // === Hook animation impacts ===
        /* REMOVED: 
        visuals.PlayerImpactEvent += () =>
        {
            if (visuals.playerAttackRange && visuals.enemyBody)
            {
                var d = visuals.playerAttackRange.Distance(visuals.enemyBody).distance;
                if (d > 0f)
                {
                    if (enableDebug) Debug.Log("[Combat] Player impact ignored (out of range) → cancel");
                    _engine.CancelPendingImpact(Side.Player);
                    return;
                }
            }
            if (enableDebug) Debug.Log("[Combat] Player impact accepted");
            _engine.OnImpact(Side.Player);
        };

        visuals.EnemyImpactEvent += () =>
        {
            if (visuals.playerEngageZone && visuals.enemyBody)
            {
                var d = visuals.playerEngageZone.Distance(visuals.enemyBody).distance;
                if (d > 0f)
                {
                    if (enableDebug) Debug.Log("[Combat] Enemy impact ignored (out of range) → cancel");
                    _engine.CancelPendingImpact(Side.Enemy);
                    return;
                }
            }
            if (enableDebug) Debug.Log("[Combat] Enemy impact accepted");
            _engine.OnImpact(Side.Enemy);
        };
*/
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
                _engine.UpdatePlayer(new FighterState {
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


    private void SyncEnginePlayerFromGameLoop()
{
    if (_engine == null || gameLoop == null) return;
    _engine.UpdatePlayer(new Core.Combat.FighterState {
        Level = gameLoop.Player.Level,
        Hp    = gameLoop.Player.Hp,
        MaxHp = gameLoop.Player.MaxHp,
        Atk   = gameLoop.Player.Atk,
        Def   = gameLoop.Player.Def,
        Xp    = gameLoop.Player.CurrentXp // or prog.XpIntoLevel if you prefer
    });
}
    private void Update()
    {
        if (_engine == null || visuals == null) return;
        {
            // === Feed current positions into the engine ===
            var player = _engine.Player;
            player.PosX = visuals.PlayerPosX;
            _engine.UpdatePlayer(player);

            var enemy = _engine.Enemy;
            enemy.PosX = visuals.EnemyPosX;
            _engine.UpdateEnemy(enemy);
            // === Tick the engine ===
            _engine.Tick(Time.deltaTime);
        }
    }

    private void HandleEvent(in CombatEvent evt)
    {
        if (enableDebug)
            Debug.Log($"[EVT] {Time.time:F2} {evt.Type} {evt.Actor} amt={evt.Amount}");

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
                if (evt.Actor == Side.Player)
                {
                    gameLoop.Enemy.Hp = _engine.Enemy.Hp;
                    visuals.SetEnemyHp(_engine.Enemy.Hp, _engine.Enemy.MaxHp);
                }
                else
                {
                    gameLoop.Player.Hp = _engine.Player.Hp;
                    visuals.SetPlayerHp(_engine.Player.Hp, _engine.Player.MaxHp);
                }
                break;
            case CombatEventType.AttackImpact:
                if (evt.Actor == Side.Player) visuals.OnPlayerAttackImpact();
                else visuals.OnEnemyAttackImpact();
                break;
            /* old unit died
            case CombatEventType.UnitDied:
                if (evt.Actor == Side.Enemy)
                {
                    if (_enemyRespawning) break;
                    _enemyRespawning = true;
                    visuals.OnEnemyDied();
                    visuals.SetEnemyHp(gameLoop.Enemy.Hp, gameLoop.Enemy.HpMax);
                    gameLoop.SpawnEnemy(); // create new enemy
                    _engine.Config.XpRewardOnKill = gameLoop.Enemy.XpReward;
                    var ne = new FighterState {
                        Level = gameLoop.Enemy.Level,
                        Hp = gameLoop.Enemy.Hp,
                        MaxHp = gameLoop.Enemy.HpMax,
                        Atk = gameLoop.Enemy.Atk,
                        Def = gameLoop.Enemy.Def
                    };
                    _engine.RespawnEnemy(ne);                    
                    // Spawn visuals for the newly selected def (keeps stats & visuals in sync)
                    visuals.SpawnEnemyFromDef(gameLoop.CurrentMonsterDef, gameLoop.CurrentIsBoss);
                }
                else
                {
                    if (_playerRespawning) break;
                    _playerRespawning = true;
                    visuals.OnPlayerDied();
                    gameLoop.Player.Hp = gameLoop.Player.MaxHp;
                    _engine.Player.Hp  = _engine.Player.MaxHp;
                    _engine.RespawnPlayer();
                    visuals.OnPlayerRespawned();
                }
                break;
*/
            case CombatEventType.UnitDied:
                if (evt.Actor == Side.Enemy)
                {
                    if (_enemyRespawning) break;
                    _enemyRespawning = true;

                    // Visuals: play death state now
                    visuals.OnEnemyDied();

                    // Optional: reflect that HP hit zero on the bar while it’s visible
                    visuals.SetEnemyHp(gameLoop.Enemy.Hp, gameLoop.Enemy.HpMax);

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
            MaxHp = gameLoop.Enemy.HpMax,
            Atk   = gameLoop.Enemy.Atk,
            Def   = gameLoop.Enemy.Def
            // PosX will be fed from visuals in Update() as usual
        };
        _engine.RespawnEnemy(ne);

        // Spawn visuals for the newly selected def (keeps stats & visuals in sync)
        visuals.SpawnEnemyFromDef(gameLoop.CurrentMonsterDef, gameLoop.CurrentIsBoss);

        _enemyRespawning = false;
    }

    private System.Collections.IEnumerator PlayerRespawnRoutine()
    {
        yield return new UnityEngine.WaitForSeconds(playerDeathDelay);

        // Reset player HP and engine snapshot (your previous logic)
        gameLoop.Player.Hp = gameLoop.Player.MaxHp;

        // If you manually touched _engine.Player.Hp before, that’s no longer needed here,
        // RespawnPlayer() will ensure the engine-side snapshot is alive:
        _engine.RespawnPlayer();

        visuals.OnPlayerRespawned();

        _playerRespawning = false;
    }

}
