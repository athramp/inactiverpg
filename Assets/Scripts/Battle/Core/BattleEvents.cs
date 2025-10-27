namespace Battle.Core
{
    public sealed class BattleEvents
    {
        // target = who took damage, dmg = amount, crit = was critical hit?
        public System.Action<Actor,int,bool> OnDamage;

        // fired whenever an actor's HP changes (after damage, heals, etc.)
        public System.Action<Actor> OnHpChanged;

        // fired when an actor reaches 0 HP
        public System.Action<Actor> OnDeath;

        // optional, available for future use (turn/round start, etc.)
        public System.Action OnRoundStarted;
    }
}
