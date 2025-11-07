// CORE — no UnityEngine here
using System.Collections.Generic;
namespace Core.Combat
{
    public enum Side { Player, Enemy }
    public struct CombatActorId { public int Value; } // immutable handle
    public struct TeamId { public byte Value; } // 0=players,1=monsters,...
    public struct FighterState
    {
        public int Level, Hp, MaxHp, Atk, Def, Xp;
        public float PosX;                // NEW: canonical 1D position
        public bool IsDead => Hp <= 0;
        public CombatActorId ActorId; // optional link to higher-level actor
        public TeamId Team;
        public int Shield;             // absorbs incoming damage first
        public float StunTimer;        // >0 means stunned
        public float AtkBuffTimer;     // example: temporary ATK buff
        public float AtkBuffMultiplier;// e.g., 1.25f
    }

    public struct EngineConfig
    {
        public float PlayerAttackRateSec;
        public float EnemyAttackRateSec;
        public int XpRewardOnKill;
    }

    public enum CombatEventType { AttackStarted, AttackImpact, DamageApplied, UnitDied, XpGained, LeveledUp, Respawned, Healed }

    public readonly struct CombatEvent
    {
        public readonly CombatEventType Type;
        public readonly Side Actor;
        public readonly int Amount;          // damage or xp when relevant
        public readonly float ProjectileETA; // seconds; 0 for melee/hitscan

        // Generic ctor (non-projectile use)
        public CombatEvent(CombatEventType t, Side a, int amt = 0)
        { Type = t; Actor = a; Amount = amt; ProjectileETA = 0f; }

        // Projectile impact/spawn moment (includes ETA)
        public CombatEvent(CombatEventType t, Side a, float eta)
        { Type = t; Actor = a; Amount = 0; ProjectileETA = eta; }
    }

    public sealed class CombatEngine
    {
        // If damageOverride != null, use that exact damage (e.g., skills).
        private struct PendingImpact
        {
            public Side side;
            public float time;
            public bool isProjectileArrival;
            public int? damageOverride;
        }
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

                if (!_playerAwaitingImpact && _pTimer >= pPeriod && Player.StunTimer <= 0f)
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
                if (Player.AtkBuffTimer > 0f)
                {
                    Player.AtkBuffTimer -= dt;
                    if (Player.AtkBuffTimer <= 0f) Player.AtkBuffMultiplier = 1f;
                }

                if (Enemy.StunTimer > 0f)
                {
                    Enemy.StunTimer -= dt;
                }

                // === Enemy cadence ===
                _eTimer += dt;
                float ePeriod = (EnemyAttack != null && EnemyAttack.period > 0f)
                    ? EnemyAttack.period
                    : System.Math.Max(0.01f, Config.EnemyAttackRateSec);

                if (!_enemyAwaitingImpact && _eTimer >= ePeriod && Enemy.StunTimer <= 0f)
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
                    // If a skill scheduled an exact damage, use it verbatim:
                    if (imp.damageOverride.HasValue)
                    {
                        int dmg = imp.damageOverride.Value;
                        if (imp.side == Side.Player)
                        {
                            Enemy.Hp = System.Math.Max(0, Enemy.Hp - dmg);
                            _emit(new CombatEvent(CombatEventType.DamageApplied, Side.Player, dmg));
                            if (Enemy.IsDead)
                            {
                                _emit(new CombatEvent(CombatEventType.UnitDied, Side.Enemy));
                                Player.Xp += Config.XpRewardOnKill;
                                _emit(new CombatEvent(CombatEventType.XpGained, Side.Player, Config.XpRewardOnKill));
                            }
                        }
                        else
                        {
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
                        // Existing projectile-arrival behavior (normal attacks)
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
                }
                else
                {
                    // Melee/hitscan impact (unchanged)
                    ResolveImpact(imp.side);
                }
            }
        }
        private int ApplyShieldThenHp(ref FighterState target, int rawDamage)
        {
            int remaining = rawDamage;

            if (target.Shield > 0)
            {
                int absorbed = System.Math.Min(target.Shield, remaining);
                target.Shield -= absorbed;
                remaining -= absorbed;
            }

            if (remaining > 0)
                target.Hp = System.Math.Max(0, target.Hp - remaining);

            return remaining; // how much actually hit HP
        }

        public void ApplyPureDamageToEnemy(int amount)
        {
            if (amount <= 0) return;
            ApplyShieldThenHp(ref Enemy, amount);
            _emit(new CombatEvent(CombatEventType.DamageApplied, Side.Player, amount));
            if (Enemy.IsDead)
            {
                _emit(new CombatEvent(CombatEventType.UnitDied, Side.Enemy));
                Player.Xp += Config.XpRewardOnKill;
                _emit(new CombatEvent(CombatEventType.XpGained, Side.Player, Config.XpRewardOnKill));
            }
        }

        public void ApplyPureDamageToPlayer(int amount)
        {
            if (amount <= 0) return;
            ApplyShieldThenHp(ref Player, amount);
            _emit(new CombatEvent(CombatEventType.DamageApplied, Side.Enemy, amount));
            if (Player.IsDead)
                _emit(new CombatEvent(CombatEventType.UnitDied, Side.Player));
        }

        public void HealPlayer(int amount)
        {
            if (amount <= 0) return;

            Player.Hp = System.Math.Min(Player.MaxHp, Player.Hp + amount);

            // Optional event if you add one; otherwise omit.
            _emit(new CombatEvent(CombatEventType.Healed, Side.Player, amount));
        }
        public void HealEnemy(int amount)
        {
            if (amount <= 0) return;

            Enemy.Hp = System.Math.Min(Enemy.MaxHp, Enemy.Hp + amount);

            // Optional event if you add one; otherwise omit.
            _emit(new CombatEvent(CombatEventType.Healed, Side.Enemy, amount));
        }

        public void AddShieldToPlayer(int amount)
        {
            if (amount <= 0) return;
            Player.Shield += amount;
        }
        public void AddShieldToEnemy(int amount)
        {
            if (amount <= 0) return;
            Enemy.Shield += amount;
        }

        public void StunEnemy(float seconds)
        {
            if (seconds <= 0f) return;
            Enemy.StunTimer = System.Math.Max(Enemy.StunTimer, seconds);
            CancelPendingImpact(Side.Enemy);
        }
        public void StunPlayer(float seconds)
        {
            if (seconds <= 0f) return;
            Player.StunTimer = System.Math.Max(Player.StunTimer, seconds);
            CancelPendingImpact(Side.Player);
        }

        public void KnockbackEnemy(float dx)
        {
            Enemy.PosX += dx;
            // visuals will pick up Enemy.PosX in your Orchestrator
        }


        /// <summary>
        /// Apply damage that is not tied to the normal attack cadence.
        /// etaSec > 0 delays the hit; otherwise it lands immediately.
        /// </summary>
        public void ApplyPureDamage(Side source, float multiplier, int flatBonus, float etaSec = 0f)
        {
            // Decide attacker/defender
            ref FighterState atk = ref (source == Side.Player ? ref Player : ref Enemy);
            ref FighterState def = ref (source == Side.Player ? ref Enemy  : ref Player);

            // Base damage via your existing calculator
            int baseDmg  = DamageCalculator.ComputeDamage(atk.Atk, def.Def);
            int finalDmg = System.Math.Max(0, (int)System.Math.Round(baseDmg * multiplier) + flatBonus);

            if (etaSec <= 0f)
            {
                // Apply now (reuse the same path you use when a normal hit lands)
                def.Hp = System.Math.Max(0, def.Hp - finalDmg);
                _emit(new CombatEvent(CombatEventType.DamageApplied, source, finalDmg));

                // Death + XP (mirror your existing logic)
                if ((source == Side.Player && def.IsDead))
                {
                    _emit(new CombatEvent(CombatEventType.UnitDied, Side.Enemy));
                    Player.Xp += Config.XpRewardOnKill;
                    _emit(new CombatEvent(CombatEventType.XpGained, Side.Player, Config.XpRewardOnKill));
                }
                else if (source == Side.Enemy && def.IsDead)
                {
                    _emit(new CombatEvent(CombatEventType.UnitDied, Side.Player));
                }
            }
            else
            {
                // Schedule for later; we mark isProjectileArrival=true to reuse the arrival slot,
                // but we carry damageOverride so we don't recompute damage at resolution.
                _impacts.Add(new PendingImpact
                {
                    side = source,
                    time = _now + etaSec,
                    isProjectileArrival = true,
                    damageOverride = finalDmg
                });
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
                if (Player.IsDead || Player.StunTimer > 0f) return;

                float reach = (PlayerAttack != null ? PlayerAttack.reach : 1.5f);
                bool inReach = Distance1D() <= reach;
                if (!inReach) return; // silent whiff

                float projSpeed = (PlayerAttack != null ? PlayerAttack.projectileSpeed : 0f);
                if (projSpeed > 0f)
                {
                    // Spawn projectile VFX now (with ETA)
                    float flight = Distance1D() / System.Math.Max(0.01f, projSpeed);
                    _emit(new CombatEvent(CombatEventType.AttackImpact, Side.Player, (float)flight));

                    // Schedule damage on arrival
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
                if (Enemy.IsDead || Enemy.StunTimer > 0f) return;
                float reach = (EnemyAttack != null ? EnemyAttack.reach : 1.5f);
                bool inReach = Distance1D() <= reach;
                if (!inReach) return;

                float projSpeed = (EnemyAttack != null ? EnemyAttack.projectileSpeed : 0f);
                if (projSpeed > 0f)
                {
                    float flight = Distance1D() / System.Math.Max(0.01f, projSpeed);
                    _emit(new CombatEvent(CombatEventType.AttackImpact, Side.Enemy, (float)flight));
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
