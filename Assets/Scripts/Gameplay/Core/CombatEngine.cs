// CORE — no UnityEngine here
namespace Core.Combat
{
    public enum Side { Player, Enemy }

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

        public delegate void EventSink(in CombatEvent e);
        readonly EventSink _emit;

        public CombatEngine(FighterState player, FighterState enemy, EngineConfig cfg, EventSink sink)
        { Player = player; Enemy = enemy; Config = cfg; _emit = sink; }

        public void Tick(float dt)
        {
            if (!Player.IsDead)
            {
                _pTimer += dt;
                if (_pTimer >= Config.PlayerAttackRateSec) { _pTimer = 0; _emit(new CombatEvent(CombatEventType.AttackStarted, Side.Player)); }
            }
            if (!Enemy.IsDead)
            {
                _eTimer += dt;
                if (_eTimer >= Config.EnemyAttackRateSec) { _eTimer = 0; _emit(new CombatEvent(CombatEventType.AttackStarted, Side.Enemy)); }
            }
        }

        // Call when the animation's hit frame happens:
        public void OnImpact(Side attacker)
        {
            if (attacker == Side.Player && !Player.IsDead && !Enemy.IsDead)
            {
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
            else if (attacker == Side.Enemy && !Enemy.IsDead && !Player.IsDead)
            {
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
            if (Enemy.Hp <= 0)
                Enemy.Hp = Enemy.MaxHp;
            _emit(new CombatEvent(CombatEventType.Respawned, Side.Enemy));
        }

        public void RespawnPlayer()
        {
            if (Player.Hp <= 0)
                Player.Hp = Player.MaxHp;
            _emit(new CombatEvent(CombatEventType.Respawned, Side.Player));
        }
    }
}
