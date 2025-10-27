using UnityEngine;

namespace Battle.Core
{
    public sealed class BattleEngine
    {
        public Actor Player { get; private set; }
        public Actor Enemy  { get; private set; }
        public BattleEvents Events { get; } = new BattleEvents();

        // Optional auto-attack timing (we'll keep your anim events, but this lets you go fully auto later)
        float _playerPeriod = 1.0f, _enemyPeriod = 1.2f;
        float _pTimer, _eTimer;
        bool _autoAttack = false;

        public void StartEncounter(Actor player, Actor enemy, float playerAttackRateSec, float enemyAttackRateSec, bool autoAttack = false)
        {
            Player = player;
            Enemy  = enemy;

            _autoAttack = autoAttack;
            _playerPeriod = Mathf.Max(0.05f, playerAttackRateSec);
            _enemyPeriod  = Mathf.Max(0.05f, enemyAttackRateSec);
            _pTimer = _eTimer = 0f;

            Player.ResetToFull();
            Enemy.ResetToFull();

            Events.OnHpChanged?.Invoke(Player);
            Events.OnHpChanged?.Invoke(Enemy);
        }

        public void Tick(float dt)
        {
            if (!_autoAttack) return;
            if (Player.IsDead || Enemy.IsDead) return;

            _pTimer += dt; _eTimer += dt;

            if (_pTimer >= _playerPeriod) { _pTimer -= _playerPeriod; ResolveAttack(Player, Enemy); }
            if (_eTimer >= _enemyPeriod)  { _eTimer -= _enemyPeriod;  ResolveAttack(Enemy, Player);  }
        }

        // Call these from animation events for perfect timing
        public void PlayerAttackOnce() => ResolveAttack(Player, Enemy);
        public void EnemyAttackOnce()  => ResolveAttack(Enemy, Player);

        void ResolveAttack(Actor attacker, Actor defender)
        {
            if (attacker == null || defender == null || attacker.IsDead || defender.IsDead) return;

            int baseDmg = Mathf.Max(1, attacker.Stats.Atk - defender.Stats.Def);
            bool crit = Random.value < attacker.Stats.CritChance;
            int dmg = Mathf.Max(1, Mathf.RoundToInt(baseDmg * (crit ? attacker.Stats.CritMult : 1f)));

            defender.Hp = Mathf.Max(0, defender.Hp - dmg);

            Events.OnDamage?.Invoke(defender, dmg, crit);
            Events.OnHpChanged?.Invoke(defender);

            if (defender.Hp == 0)
            {
                Events.OnDeath?.Invoke(defender);
            }
        }
    }
}
