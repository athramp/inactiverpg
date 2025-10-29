// CORE — no UnityEngine here
namespace Core.Combat
{
    public enum Side { Player, Enemy }
    // timers

    public struct FighterState
    {
        public int Level, Hp, MaxHp, Atk, Def, Xp;
        public bool IsDead => Hp <= 0;
    }

    public struct EngineConfig
    {
        public float PlayerAttackRateSec;
        public float EnemyAttackRateSec;
        public int XpRewardOnKill;
    }

    public enum CombatEventType { AttackStarted, AttackImpact, DamageApplied, UnitDied, XpGained, LeveledUp, Respawned }

    public readonly struct CombatEvent
    {
        public readonly CombatEventType Type;
        public readonly Side Actor;
        public readonly int Amount; // damage or xp when relevant
        public CombatEvent(CombatEventType t, Side a, int amt = 0)
        { Type = t; Actor = a; Amount = amt; }
    }

    public sealed class CombatEngine
    {
        public FighterState Player;
        public FighterState Enemy;
        public EngineConfig Config;

        float _pTimer, _eTimer;
        // awaiting-impact guards

        float _pAwaitTimer, _eAwaitTimer;

        // NEW: allow exactly one impact per scheduled attack
        bool _playerAwaitingImpact, _enemyAwaitingImpact;
        const float AWAIT_WINDOW = 1.0f;

        public delegate void EventSink(in CombatEvent e);
        readonly EventSink _emit;

        public CombatEngine(FighterState player, FighterState enemy, EngineConfig cfg, EventSink sink)
        { Player = player; Enemy = enemy; Config = cfg; _emit = sink; }

        public void Tick(float dt)
        {
            // schedule attacks
            if (!Player.IsDead && !Enemy.IsDead)
            {
                _pTimer += dt;
                if (_pTimer >= Config.PlayerAttackRateSec && !_playerAwaitingImpact)
                {
                    _pTimer -= Config.PlayerAttackRateSec;
                    _playerAwaitingImpact = true;
                    _pAwaitTimer = 0f;                     // reset window
                    _emit(new CombatEvent(CombatEventType.AttackStarted, Side.Player));
                }

                _eTimer += dt;
                if (_eTimer >= Config.EnemyAttackRateSec && !_enemyAwaitingImpact)
                {
                    _eTimer -= Config.EnemyAttackRateSec;
                    _enemyAwaitingImpact = true;
                    _eAwaitTimer = 0f;                     // reset window
                    _emit(new CombatEvent(CombatEventType.AttackStarted, Side.Enemy));
                }
            }

            // impact window timeout (prevents permanent stalls if impact never arrives)
            if (_playerAwaitingImpact)
            {
                _pAwaitTimer += dt;
                if (_pAwaitTimer > AWAIT_WINDOW)
                {
                    _playerAwaitingImpact = false;
                    _pAwaitTimer = 0f;
                    // (optional) _emit(new CombatEvent(CombatEventType.AttackCanceled, Side.Player));
                }
            }
            if (_enemyAwaitingImpact)
            {
                _eAwaitTimer += dt;
                if (_eAwaitTimer > AWAIT_WINDOW)
                {
                    _enemyAwaitingImpact = false;
                    _eAwaitTimer = 0f;
                    // (optional) _emit(new CombatEvent(CombatEventType.AttackCanceled, Side.Enemy));
                }
            }
        }
        public void UpdatePlayer(FighterState s) { Player = s; }
                public void CancelPendingImpact(Side s)
        {
            if (s == Side.Player) _playerAwaitingImpact = false;
            else _enemyAwaitingImpact = false;
        }
        // Call when the animation's hit frame happens:
        public void OnImpact(Side attacker)
        {
            if (attacker == Side.Player)
            {
                if (!_playerAwaitingImpact || Player.IsDead || Enemy.IsDead) return;
                _playerAwaitingImpact = false; // consume token
                _pAwaitTimer = 0f;
                // DEBUG: print exact numbers engine is using
                UnityEngine.Debug.Log($"[DMGDBG] Player atk={Player.Atk}  vs Enemy def={Enemy.Def}");
                int dmg = DamageCalculator.ComputeDamage(Player.Atk, Enemy.Def);
                Enemy.Hp = System.Math.Max(0, Enemy.Hp - dmg); 
                _emit(new CombatEvent(CombatEventType.AttackImpact, Side.Player));
                _emit(new CombatEvent(CombatEventType.DamageApplied, Side.Player, dmg));
                if (Enemy.IsDead)
                {
                    _emit(new CombatEvent(CombatEventType.UnitDied, Side.Enemy));
                    Player.Xp += Config.XpRewardOnKill;
                    _emit(new CombatEvent(CombatEventType.XpGained, Side.Player, Config.XpRewardOnKill));
                }
            }
            else // Enemy
            {
                if (!_enemyAwaitingImpact || Enemy.IsDead || Player.IsDead) return;
                _enemyAwaitingImpact = false; // consume token
                _eAwaitTimer = 0f;
                UnityEngine.Debug.Log($"[DMGDBG] Enemy atk={Enemy.Atk}  vs Player def={Player.Def}");
                int dmg = DamageCalculator.ComputeDamage(Enemy.Atk, Player.Def);
                Player.Hp = System.Math.Max(0, Player.Hp - dmg);
                _emit(new CombatEvent(CombatEventType.AttackImpact, Side.Enemy));
                _emit(new CombatEvent(CombatEventType.DamageApplied, Side.Enemy, dmg));
                if (Player.IsDead)
                {
                    _emit(new CombatEvent(CombatEventType.UnitDied, Side.Player));
                }
            }
        }

        public void RespawnEnemy(FighterState newEnemy)
        {
            Enemy = newEnemy;
            if (Enemy.Hp <= 0) Enemy.Hp = Enemy.MaxHp;

            // reset cadence
            _enemyAwaitingImpact = false; _eTimer = 0f;
            _playerAwaitingImpact = false; _pTimer = 0f;

            _emit(new CombatEvent(CombatEventType.Respawned, Side.Enemy));
        }

        public void RespawnPlayer()
        {
            if (Player.Hp <= 0) Player.Hp = Player.MaxHp;

            _playerAwaitingImpact = false; _pTimer = 0f;
            _enemyAwaitingImpact = false; _eTimer = 0f;

            _emit(new CombatEvent(CombatEventType.Respawned, Side.Player));
        }
    }
}
