using UnityEngine;
using Core.Combat;
using System.Collections;

public class CombatOrchestrator : MonoBehaviour
{
    public BattleVisualController visuals; // assign in inspector
    public GameLoopService gameLoop;       // existing owner of Player/Enemy/Class/XpTable

    CombatEngine _engine;

    private IEnumerator Start()
    {
        // Wait for Firebase and GameLoop initialization
        yield return new WaitUntil(() => gameLoop != null && gameLoop.IsInitialized && FirebaseGate.IsReady);

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
            PlayerAttackRateSec = visuals.playerAttackRate,
            EnemyAttackRateSec  = visuals.enemyAttackRate,
            XpRewardOnKill = 50 // TODO: replace with EnemyStats.XpReward
        };

        _engine = new CombatEngine(p, e, cfg, HandleEvent);

        // Wire animation events to engine OnImpact via existing relay
        visuals.PlayerImpactEvent += () => _engine.OnImpact(Side.Player);
        visuals.EnemyImpactEvent += () => _engine.OnImpact(Side.Enemy);
        visuals.useEngineDrive = true;
        Debug.Log("[CombatOrchestrator] Engine initialized.");

    }

    private void Update()
    {
        if (_engine != null)
            _engine.Tick(Time.deltaTime);
    }
    void HandleEvent(in CombatEvent evt)
    {
        switch (evt.Type)
        {
            case CombatEventType.AttackStarted:
                if (evt.Actor == Side.Player) visuals.TriggerPlayerAttack();
                else                          visuals.TriggerEnemyAttack();
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
                // force CombatUI to update bars
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
                _engine.Enemy.Hp = gameLoop.Enemy.Hp; // keep both in sync
                visuals.OnEnemyRespawned();
            }
            else
            {
                visuals.OnPlayerDied();
                gameLoop.Player.Hp = gameLoop.Player.MaxHp;
                _engine.Player.Hp  = _engine.Player.MaxHp;
                visuals.OnPlayerRespawned();
            }
            break;

            case CombatEventType.XpGained:
                // Push XP into PlayerStats (reuse your existing GainXp logic)
                gameLoop.Player.GainXp(evt.Amount);
                visuals.SetXp(gameLoop.Player.CurrentXp, gameLoop.XpTable.GetXpToNext(gameLoop.Player.Level));
                break;
        }
    }
}
