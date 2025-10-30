// CORE — no UnityEngine here
using System.Collections.Generic;
namespace Core.Combat
{
    public enum Side { Player, Enemy }
    
    public struct FighterState
    {
        public int Level, Hp, MaxHp, Atk, Def, Xp;
        public float PosX;                // NEW: canonical 1D position
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
        private struct PendingImpact { public Side side; public float time; public bool isProjectileArrival; }
        public AttackProfile PlayerAttack; // NEW
        public AttackProfile EnemyAttack;  // NEW
        public FighterState Player;
        public FighterState Enemy;
        public EngineConfig Config;
        // === Time & cadence ===
        private float _now;                 // engine time (seconds)
        private float _pTimer;              // accumulates time until Player period elapses
        private float _eTimer;              // accumulates time until Enemy period elapses
        private bool _playerAwaitingImpact; // between AttackStarted and impact resolution
        private bool _enemyAwaitingImpact;
        private float Distance1D() => (float)System.Math.Abs(Player.PosX - Enemy.PosX);
        // private struct PendingImpact { public Side side; public float time; }
        private readonly List<PendingImpact> _impacts = new List<PendingImpact>();

        // awaiting-impact guards
        float _pAwaitTimer, _eAwaitTimer;
        // === NEW: impact scheduler ===
        public delegate void EventSink(in CombatEvent e);
        readonly EventSink _emit;

        public CombatEngine(FighterState player, FighterState enemy, EngineConfig cfg, EventSink sink)
        { Player = player; Enemy = enemy; Config = cfg; _emit = sink; }

        public void Tick(float dt)
        {
            _now += dt;

            // 1) Advance cadence and schedule impacts after windup (only if both alive)
            if (!Player.IsDead && !Enemy.IsDead)
            {
                // === Player cadence ===
                _pTimer += dt;
                float pPeriod = (PlayerAttack != null && PlayerAttack.period > 0f)
                    ? PlayerAttack.period
                    : System.Math.Max(0.01f, Config.PlayerAttackRateSec);

                if (!_playerAwaitingImpact && _pTimer >= pPeriod)
                {
                    float reach = (PlayerAttack != null ? PlayerAttack.reach : 1.5f);
                    bool inStartRange = Distance1D() <= reach;

                    if (!inStartRange)
                    {
                        // keep the timer "charged" so it will start the instant we come into range
                        _pTimer = pPeriod;
                    }
                    else
                    {
                        _pTimer -= pPeriod;
                        _playerAwaitingImpact = true;
                        _pAwaitTimer = 0f;

                        float tImpact = _now + (PlayerAttack != null ? System.Math.Max(0f, PlayerAttack.windup) : 0f);
                        _impacts.Add(new PendingImpact { side = Side.Player, time = tImpact, isProjectileArrival = false });
                        _emit(new CombatEvent(CombatEventType.AttackStarted, Side.Player));
                    }
                }

                // === Enemy cadence ===
                _eTimer += dt;
                float ePeriod = (EnemyAttack != null && EnemyAttack.period > 0f)
                    ? EnemyAttack.period
                    : System.Math.Max(0.01f, Config.EnemyAttackRateSec);

                if (!_enemyAwaitingImpact && _eTimer >= ePeriod)
                {
                    float reach = (EnemyAttack != null ? EnemyAttack.reach : 1.5f);
                    bool inStartRange = Distance1D() <= reach;

                    if (!inStartRange)
                    {
                        _eTimer = ePeriod; // arm it; will start as soon as we enter range
                    }
                    else
                    {
                        _eTimer -= ePeriod;
                        _enemyAwaitingImpact = true;
                        _eAwaitTimer = 0f;

                        float tImpact = _now + (EnemyAttack != null ? System.Math.Max(0f, EnemyAttack.windup) : 0f);
                        _impacts.Add(new PendingImpact { side = Side.Enemy, time = tImpact, isProjectileArrival = false });
                        _emit(new CombatEvent(CombatEventType.AttackStarted, Side.Enemy));
                    }
                }
            }

            // 2) Optional timeouts (prevent stuck awaits) — make them windup-aware
            if (_playerAwaitingImpact)
            {
                _pAwaitTimer += dt;
                // Allow at least windup + small slack; never cancel early
                float pAllowed = ((PlayerAttack?.windup) ?? 0f) + 0.5f;        // 0.5s slack
                // Also guard against extreme stalls: e.g., > 10s
                float pHardCap = System.Math.Max(10f, pAllowed * 4f);

                if (_pAwaitTimer > pHardCap)
                {
                    // Only then cancel the stuck await; DO NOT use tiny timeouts
                    _playerAwaitingImpact = false;
                    _pAwaitTimer = 0f;
                    // Debug.Log("[Engine] Player await timed out (hard cap)"); // optional
                }
            }

            if (_enemyAwaitingImpact)
            {
                _eAwaitTimer += dt;
                float eAllowed = ((EnemyAttack?.windup) ?? 0f) + 0.5f;
                float eHardCap = System.Math.Max(10f, eAllowed * 4f);

                if (_eAwaitTimer > eHardCap)
                {
                    _enemyAwaitingImpact = false;
                    _eAwaitTimer = 0f;
                    // Debug.Log("[Engine] Enemy await timed out (hard cap)"); // optional
                }
            }


            // 3) Resolve due impacts (deterministic order: time then side)
            if (_impacts.Count > 1)
                _impacts.Sort((a, b) => a.time != b.time ? a.time.CompareTo(b.time) : a.side.CompareTo(b.side));

            int i = 0;
            while (i < _impacts.Count && _impacts[i].time <= _now)
            {
                var imp = _impacts[i];
                _impacts.RemoveAt(i);

                if (imp.isProjectileArrival)
                {
                    // Apply delayed projectile damage on arrival
                    if (imp.side == Side.Player)
                    {
                        int dmg = DamageCalculator.ComputeDamage(Player.Atk, Enemy.Def);
                        Enemy.Hp = System.Math.Max(0, Enemy.Hp - dmg);
                        _emit(new CombatEvent(CombatEventType.DamageApplied, Side.Player, dmg));
                        if (Enemy.IsDead)
                        {
                            _emit(new CombatEvent(CombatEventType.UnitDied, Side.Enemy));
                            Player.Xp += Config.XpRewardOnKill;
                            _emit(new CombatEvent(CombatEventType.XpGained, Side.Player, Config.XpRewardOnKill));
                        }
                    }
                    else // Enemy projectile arrival
                    {
                        int dmg = DamageCalculator.ComputeDamage(Enemy.Atk, Player.Def);
                        Player.Hp = System.Math.Max(0, Player.Hp - dmg);
                        _emit(new CombatEvent(CombatEventType.DamageApplied, Side.Enemy, dmg));
                        if (Player.IsDead)
                        {
                            _emit(new CombatEvent(CombatEventType.UnitDied, Side.Player));
                        }
                    }
                }
                else
                {
                    // Melee/hitscan impact (or projectile spawn moment handled in ResolveImpact)
                    ResolveImpact(imp.side);
                }
            }
        }


        public void UpdateEnemy(FighterState s) { Enemy = s; }
        private void ResolveImpact(Side side)
        {
            if (Player.IsDead || Enemy.IsDead) return;

            if (side == Side.Player)
            {
                _playerAwaitingImpact = false;
                _pAwaitTimer = 0f;

                float reach = (PlayerAttack != null ? PlayerAttack.reach : 1.5f);
                bool inReach = Distance1D() <= reach;
                if (!inReach) return; // silent whiff

                float projSpeed = (PlayerAttack != null ? PlayerAttack.projectileSpeed : 0f);
                if (projSpeed > 0f)
                {
                    // Spawn projectile VFX now
                    _emit(new CombatEvent(CombatEventType.AttackImpact, Side.Player));
                    // Schedule damage on arrival
                    float flight = Distance1D() / System.Math.Max(0.01f, projSpeed);
                    _impacts.Add(new PendingImpact { side = Side.Player, time = _now + flight, isProjectileArrival = true });
                    return;
                }

                // Melee / hitscan: apply now
                _emit(new CombatEvent(CombatEventType.AttackImpact, Side.Player));
                int dmg = DamageCalculator.ComputeDamage(Player.Atk, Enemy.Def);
                Enemy.Hp = System.Math.Max(0, Enemy.Hp - dmg);
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
                _enemyAwaitingImpact = false;
                _eAwaitTimer = 0f;

                float reach = (EnemyAttack != null ? EnemyAttack.reach : 1.5f);
                bool inReach = Distance1D() <= reach;
                if (!inReach) return;

                float projSpeed = (EnemyAttack != null ? EnemyAttack.projectileSpeed : 0f);
                if (projSpeed > 0f)
                {
                    _emit(new CombatEvent(CombatEventType.AttackImpact, Side.Enemy));
                    float flight = Distance1D() / System.Math.Max(0.01f, projSpeed);
                    _impacts.Add(new PendingImpact { side = Side.Enemy, time = _now + flight, isProjectileArrival = true });
                    return;
                }

                _emit(new CombatEvent(CombatEventType.AttackImpact, Side.Enemy));
                int dmg = DamageCalculator.ComputeDamage(Enemy.Atk, Player.Def);
                Player.Hp = System.Math.Max(0, Player.Hp - dmg);
                _emit(new CombatEvent(CombatEventType.DamageApplied, Side.Enemy, dmg));
                if (Player.IsDead)
                {
                    _emit(new CombatEvent(CombatEventType.UnitDied, Side.Player));
                }
            }
        }

        public void UpdatePlayer(FighterState s) { Player = s; }
        public void CancelPendingImpact(Side s)
        {
            if (s == Side.Player) _playerAwaitingImpact = false;
            else _enemyAwaitingImpact = false;
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
