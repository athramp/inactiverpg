namespace Battle.Core
{
    public enum Team { Player, Enemy }

    public sealed class Actor
    {
        public string Id;
        public Team Team;
        public int Hp;
        public int MaxHp;
        public StatBlock Stats;

        public bool IsDead => Hp <= 0;
        public void ResetToFull() { Hp = MaxHp = Stats.Hp; }
    }
}
