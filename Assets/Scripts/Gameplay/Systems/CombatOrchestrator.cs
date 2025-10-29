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
    [SerializeField] private float enemyAttackRate  = 3.0f;
    // Fields (assign via inspector)
    [SerializeField] PlayerProgression playerProgression;  // <— add
    [SerializeField] XpTable xpTable;                      // optional if PlayerProgression has it already
    private CombatEngine _engine;

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
        SyncEnginePlayerFromGameLoop();
        // === XP reward from GameLoop ===
        var xp = gameLoop.Enemy.XpReward;
        _engine.Config.XpRewardOnKill = xp;

        if (enableDebug)
            Debug.Log($"[CombatOrchestrator] Engine initialized (XP reward {xp})");
            // Spawn visuals for the data-selected def (no randomness in BVC)
            visuals.SpawnEnemyFromDef(gameLoop.CurrentMonsterDef, gameLoop.CurrentIsBoss);

        // === Hook animation impacts ===
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

        visuals.useEngineDrive = true;
        // ----- Progression wiring -----
        var prog = FindObjectOfType<PlayerProgression>();
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
                _engine.UpdatePlayer(new FighterState {
                    Level = gameLoop.Player.Level,
                    Hp    = gameLoop.Player.Hp,
                    MaxHp = gameLoop.Player.MaxHp,
                    Atk   = gameLoop.Player.Atk,
                    Def   = gameLoop.Player.Def,
                    Xp    = prog.XpIntoLevel
                });

                // 4) Persist to Firebase
                var ps = FindObjectOfType<PlayerPersistenceService>();
                if (ps) _ = ps.SaveProgressAsync();
            };


        }
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
        if (_engine != null)
            _engine.Tick(Time.deltaTime);
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
                if (visuals.playerAttackRange && visuals.enemyBody)
                {
                    var d = visuals.playerAttackRange.Distance(visuals.enemyBody).distance;
                    if (d > 0f)
                    {
                        if (enableDebug) Debug.Log("[Combat] Player attack skipped (enemy not in range)");
                        _engine.CancelPendingImpact(Side.Player);
                        break; // don’t trigger animation
                    }
                }
                visuals.TriggerPlayerAttack();
            }
            else
            {
                if (visuals.playerEngageZone && visuals.enemyBody)
                {
                    var d = visuals.playerEngageZone.Distance(visuals.enemyBody).distance;
                    if (d > 0f)
                    {
                        if (enableDebug) Debug.Log("[Combat] Enemy attack skipped (player not in range)");
                        _engine.CancelPendingImpact(Side.Enemy);
                        break;
                    }
                }
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

            case CombatEventType.UnitDied:
                if (evt.Actor == Side.Enemy)
                {
                    visuals.OnEnemyDied();
                    visuals.SetEnemyHp(gameLoop.Enemy.Hp, gameLoop.Enemy.HpMax);
                    gameLoop.SpawnEnemy(); // create new enemy
                    var ne = new FighterState {
                        Level = gameLoop.Enemy.Level,
                        Hp = gameLoop.Enemy.Hp,
                        MaxHp = gameLoop.Enemy.HpMax,
                        Atk = gameLoop.Enemy.Atk,
                        Def = gameLoop.Enemy.Def
                    };
                    _engine.RespawnEnemy(ne);
                    // _engine.Config.XpRewardOnKill = gameLoop.Enemy.XpReward;
                    // Spawn visuals for the newly selected def (keeps stats & visuals in sync)
                    visuals.SpawnEnemyFromDef(gameLoop.CurrentMonsterDef, gameLoop.CurrentIsBoss);
                }
                else
                {
                    visuals.OnPlayerDied();
                    gameLoop.Player.Hp = gameLoop.Player.MaxHp;
                    _engine.Player.Hp  = _engine.Player.MaxHp;
                    _engine.RespawnPlayer();
                    visuals.OnPlayerRespawned();
                }
                break;

            case CombatEventType.XpGained:
            {
                var prog = FindObjectOfType<PlayerProgression>();
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
                var ps = FindObjectOfType<PlayerPersistenceService>();
                if (ps)
                {
                    // ensure saved values reflect progression (level/xp)
                    var pprog = prog; // capture
                    if (pprog != null)
                    {
                        gameLoop.Player.Level    = pprog.Level;
                        gameLoop.Player.CurrentXp = pprog.XpIntoLevel; // keep old schema
                    }
                    _ = ps.SaveProgressAsync();
                }
                break;
            }
        }
    }
}
