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

    private CombatEngine _engine;

    public CombatEngine DebugEngine => _engine; // used by debug panels

    private IEnumerator Start()
    {
        // Wait for Firebase and GameLoop initialization
        yield return new WaitUntil(() => gameLoop != null && gameLoop.IsInitialized && FirebaseGate.IsReady);

        visuals.ApplyPlayerClass(gameLoop.Player.ClassId);

        // Seed engine state from your existing entities
        var p = new FighterState {
            Level = gameLoop.Player.Level,
            Hp = gameLoop.Player.Hp,
            MaxHp = gameLoop.Player.MaxHp,
            Atk = gameLoop.Player.Atk,
            Def = gameLoop.Player.Def,
            Xp  = gameLoop.Player.CurrentXp
        };
        var e = new FighterState {
            Level = gameLoop.Enemy.Level,
            Hp = gameLoop.Enemy.Hp,
            MaxHp = gameLoop.Enemy.HpMax,
            Atk = gameLoop.Enemy.Atk,
            Def = gameLoop.Enemy.Def
        };

        var cfg = new EngineConfig {
            PlayerAttackRateSec = playerAttackRate,
            EnemyAttackRateSec  = enemyAttackRate,
            XpRewardOnKill = 50 // TODO: replace with EnemyStats.XpReward
        };

        _engine = new CombatEngine(p, e, cfg, HandleEvent);

        // Wire animation events to engine OnImpact, with range gating + cancel on reject
        visuals.PlayerImpactEvent += () =>
        {
            if (visuals.playerAttackRange && visuals.enemyBody)
            {
                var d = visuals.playerAttackRange.Distance(visuals.enemyBody).distance;
                if (d > 0f)
                {
                    if (enableDebug) Debug.Log("[Combat] Player impact ignored (out of range) -> cancel");
                    _engine.CancelPendingImpact(Side.Player);  // <<< important
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
                    if (enableDebug) Debug.Log("[Combat] Enemy impact ignored (out of range) -> cancel");
                    _engine.CancelPendingImpact(Side.Enemy);   // optional but symmetric
                    return;
                }
            }
            if (enableDebug) Debug.Log("[Combat] Enemy impact accepted");
            _engine.OnImpact(Side.Enemy);
        };

        visuals.useEngineDrive = true;

        if (enableDebug)
            Debug.Log("[CombatOrchestrator] Engine initialized.");
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
                    visuals.OnEnemyRespawned();
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
                gameLoop.Player.GainXp(evt.Amount);
                visuals.SetXp(gameLoop.Player.CurrentXp,
                    gameLoop.XpTable.GetXpToNext(gameLoop.Player.Level));
                break;
        }
    }
}
