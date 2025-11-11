// CORE — no UnityEngine here
using System.Collections.Generic;
namespace Core.Combat
{
    public enum Side { Player, Enemy }

    public struct FighterState
    {
        public int Level, Hp, MaxHp, Atk, Def, Xp;
        public float PosX;
        public bool IsDead => Hp <= 0;
        public int Shield;
        public float StunTimer;
        public float AtkBuffTimer;
        public float AtkBuffMultiplier;
    }

    public struct EngineConfig
    {
        public float PlayerAttackRateSec;   // if 0, defaults to 1.0
        public int   XpRewardOnKill;
    }

    public enum CombatEventType { AttackStarted, AttackImpact, DamageApplied, UnitDied, XpGained, Healed }

    public readonly struct CombatEvent
    {
        public readonly CombatEventType Type;
        public readonly Side Actor;
        public readonly int Amount;          // damage/xp
        public readonly float ProjectileETA;
        public CombatEvent(CombatEventType t, Side a, int amt = 0) { Type=t; Actor=a; Amount=amt; ProjectileETA=0f; }
        public CombatEvent(CombatEventType t, Side a, float eta)   { Type=t; Actor=a; Amount=0;   ProjectileETA=eta; }
    }

    public sealed class CombatEngine
    {
        public delegate void EventSink(in CombatEvent e);
        private readonly EventSink _emit;

        // Player
        public FighterState Player;
        public EngineConfig Config;

        // Proxy enemy X for player cadence/range gate (set by orchestrator each frame)
        public float EnemyProxyX { get; set; }

        // Multi-enemy state
        private readonly Dictionary<int, FighterState> _enemies = new();
        private int _nextEnemyId = 1;
        public int  EnemyCount => _enemies.Count;
        public bool HasEnemy(int enemyId) => _enemies.ContainsKey(enemyId);

        // Cadence (player basic)
        private float _now, _pTimer;
        public float PlayerReach = 1.6f; // set by orchestrator
        public float PlayerPeriodSec = 1.0f;   // set by orchestrator if you have AttackProfile.period
        // ---- DoT model ----
        private struct Dot
        {
            public int dmgPerTick;
            public float tickEvery;
            public float nextTick;
            public float remaining;
        }
        private readonly List<Dot> _playerDots = new();
        private readonly Dictionary<int, List<Dot>> _enemyDots = new();

        // Public API
        public void ApplyDotToEnemy(int enemyId, int dmgPerTick, float duration, float tickEvery)
        {
            if (dmgPerTick <= 0 || duration <= 0f || tickEvery <= 0f) return;
            if (!_enemies.ContainsKey(enemyId)) return;
            if (!_enemyDots.TryGetValue(enemyId, out var list)) { list = new List<Dot>(); _enemyDots[enemyId] = list; }
            list.Add(new Dot { dmgPerTick = dmgPerTick, tickEvery = tickEvery, nextTick = tickEvery, remaining = duration });
        }
        public void ApplyDotToPlayer(int dmgPerTick, float duration, float tickEvery)
        {
            if (dmgPerTick <= 0 || duration <= 0f || tickEvery <= 0f) return;
            _playerDots.Add(new Dot { dmgPerTick = dmgPerTick, tickEvery = tickEvery, nextTick = tickEvery, remaining = duration });
        }

        public CombatEngine(FighterState player, EngineConfig cfg, EventSink sink)
        {
            Player = player;
            Config = cfg;
            _emit  = sink;
            Player.AtkBuffMultiplier = Player.AtkBuffMultiplier == 0f ? 1f : Player.AtkBuffMultiplier;
        }

        // ----- Multi-enemy API -----
        public int AddEnemy(FighterState state)
        {
            int id = _nextEnemyId++;
            _enemies[id] = state;
            return id;
        }
        public bool RemoveEnemy(int enemyId) => _enemies.Remove(enemyId);
        public bool TryGetEnemy(int enemyId, out FighterState state) => _enemies.TryGetValue(enemyId, out state);
        public bool UpdateEnemy(int enemyId, in FighterState state)
        { if (!_enemies.ContainsKey(enemyId)) return false; _enemies[enemyId] = state; return true; }
        public IEnumerable<(int id, FighterState state)> AllEnemies()
        {
            foreach (var kv in new List<KeyValuePair<int, FighterState>>(_enemies))
                yield return (kv.Key, kv.Value);
        }

        // ----- Central damage paths -----
        public void DealDamageToEnemy(int targetEnemyId, int amount)
        {
            if (amount <= 0) return;
            if (!_enemies.TryGetValue(targetEnemyId, out var fs)) return;

            amount = ApplyShieldThenHp(ref fs, amount);
            _enemies[targetEnemyId] = fs;

            _emit?.Invoke(new CombatEvent(CombatEventType.DamageApplied, Side.Player, amount));
            if (fs.Hp == 0)
            {
                _emit?.Invoke(new CombatEvent(CombatEventType.UnitDied, Side.Enemy));
            }
        }

        public void DealDamageToPlayer(int amount)
        {
            if (amount <= 0) return;
            amount = ApplyShieldThenHp(ref Player, amount);
            _emit?.Invoke(new CombatEvent(CombatEventType.DamageApplied, Side.Enemy, amount));
            if (Player.IsDead)
                _emit?.Invoke(new CombatEvent(CombatEventType.UnitDied, Side.Player));
        }

        public void HealPlayer(int amount)
        {
            if (amount <= 0) return;
            Player.Hp = System.Math.Min(Player.MaxHp, Player.Hp + amount);
            _emit?.Invoke(new CombatEvent(CombatEventType.Healed, Side.Player, amount));
        }
        public void AddShieldToPlayer(int amount)
        { if (amount > 0) Player.Shield += amount; }

        public void StunPlayer(float seconds)
        {
            if (seconds <= 0f) return;
            Player.StunTimer = System.Math.Max(Player.StunTimer, seconds);
        }

        // ----- Tick (buffs/cc + player auto cadence) -----
        public void Tick(float dt)
        {
            _now += dt;

            // decay player timers
            if (Player.AtkBuffTimer > 0f)
            {
                Player.AtkBuffTimer -= dt;
                if (Player.AtkBuffTimer <= 0f) Player.AtkBuffMultiplier = 1f;
            }
            if (Player.StunTimer > 0f) Player.StunTimer -= dt;

            // decay enemy stuns
            if (_enemies.Count != 0)
            {
                var snapshot = new List<int>(_enemies.Keys);
                foreach (var id in snapshot)
                {
                    var fs = _enemies[id];
                    if (fs.StunTimer > 0f) { fs.StunTimer -= dt; if (fs.StunTimer < 0f) fs.StunTimer = 0f; _enemies[id] = fs; }
                }
            }

            // basic player auto attack cadence vs proxy X
            float period = PlayerPeriodSec > 0f ? PlayerPeriodSec : (Config.PlayerAttackRateSec > 0f ? Config.PlayerAttackRateSec : 1f);
            if (!Player.IsDead && _enemies.Count > 0)
            {
                _pTimer += dt;
                // range check vs proxy X; orchestrator locks proxy to current target’s X
                bool inRange = System.Math.Abs(Player.PosX - EnemyProxyX) <= PlayerReach;
                if (_pTimer >= period && Player.StunTimer <= 0f && inRange)
                {
                    _pTimer -= period;
                    _emit?.Invoke(new CombatEvent(CombatEventType.AttackStarted, Side.Player));
                    // hitscan: emit immediate impact (no per-enemy id here; orchestrator routes to target)
                    _emit?.Invoke(new CombatEvent(CombatEventType.AttackImpact, Side.Player, 0f));
                }
            }
            // --- Tick DoTs on player ---
            for (int i = _playerDots.Count - 1; i >= 0; i--)
            {
                var d = _playerDots[i];
                d.remaining -= dt; d.nextTick -= dt;
                if (d.nextTick <= 0f) { DealDamageToPlayer(d.dmgPerTick); d.nextTick += d.tickEvery; }
                if (d.remaining <= 0f) _playerDots.RemoveAt(i); else _playerDots[i] = d;
            }

            // --- Tick DoTs on enemies ---
            if (_enemyDots.Count > 0)
            {
                var keys = new List<int>(_enemyDots.Keys);
                foreach (var eid in keys)
                {
                    var list = _enemyDots[eid];
                    for (int i = list.Count - 1; i >= 0; i--)
                    {
                        var d = list[i];
                        d.remaining -= dt; d.nextTick -= dt;
                        if (d.nextTick <= 0f) { DealDamageToEnemy(eid, d.dmgPerTick); d.nextTick += d.tickEvery; }
                        if (d.remaining <= 0f) list.RemoveAt(i); else list[i] = d;
                    }
                    if (list.Count == 0) _enemyDots.Remove(eid);
                }
            }
        }

        // helpers
        private static int ApplyShieldThenHp(ref FighterState target, int raw)
        {
            int remaining = raw;
            if (target.Shield > 0)
            {
                int absorbed = System.Math.Min(target.Shield, remaining);
                target.Shield -= absorbed;
                remaining -= absorbed;
            }
            if (remaining > 0)
                target.Hp = System.Math.Max(0, target.Hp - remaining);
            return remaining;
        }
    }
}
