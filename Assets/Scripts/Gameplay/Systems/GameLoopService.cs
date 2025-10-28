// Assets/Scripts/Systems/GameLoopService.cs
using System.Collections;
using UnityEngine;

public class GameLoopService : MonoBehaviour
{
    [Header("Data")]
    public ClassCatalog classCatalog;
    public XpTable xpTable;

    public PlayerStats Player { get; private set; }
    public EnemyStats  Enemy  { get; private set; }
    public XpTable     XpTable => xpTable;

    public bool IsInitialized { get; private set; }

    public event System.Action<int> OnPlayerHit;   // dmg dealt to enemy
    public event System.Action<int> OnEnemyHit;    // dmg dealt to player
    public event System.Action OnEnemyKilled;
    public event System.Action OnPlayerKilled;
    public bool IsEngaged { get; private set; }
    public void BeginEngagement()
{
    if (IsEngaged) return;
    IsEngaged = true;
    Debug.Log("[GameLoop] Engagement started");
}
// after damage is applied

// Call this from animation event or a timed coroutine
public int PlayerAttackOnce()
{
    if (!IsInitialized || Enemy == null) return 0;
    int dmg = DamageCalculator.ComputeDamage(Player.Atk, Enemy.Def);
    Enemy.Hp -= dmg;
    OnPlayerHit?.Invoke(dmg);

    if (Enemy.Hp <= 0)
    {
        Player.GainXp(50);
        OnEnemyKilled?.Invoke();
        SpawnEnemy();                 // prepare next enemy (Enemy is replaced)
        IsEngaged = false;            // re-approach next enemy
    }
    return dmg;
}

public int EnemyAttackOnce()
{
    if (!IsInitialized || Player == null) return 0;
    int dmg = DamageCalculator.ComputeDamage(Enemy.Atk, Player.Def);
    Player.Hp -= dmg;
    OnEnemyHit?.Invoke(dmg);

    if (Player.Hp <= 0)
    {
        OnPlayerKilled?.Invoke();
        Player.Hp = Player.MaxHp;     // simple respawn for now
    }
    return dmg;
}
    // NEW: call this after login/character selection
public void Initialize(string classId)
{
    if (IsInitialized) return;

    if (string.IsNullOrEmpty(classId))
    {
        Debug.LogWarning("[GameLoop] classId null/empty; defaulting to 'Warrior'");
        classId = "Warrior";
    }
    if (classCatalog == null || xpTable == null)
    {
        Debug.LogError("[GameLoop] Missing ClassCatalog / XpTable (assign them on GameRoot).");
        return;
    }

    Player = new PlayerStats(classCatalog, xpTable, classId);
    SpawnEnemy();
    IsInitialized = true;
    Debug.Log($"[GameLoop] Initialized with class {classId}");
}

    public void SpawnEnemy()
    {
        int enemyLevel = Mathf.Max(1, Player.Level);
        Enemy = new EnemyStats(enemyLevel);
        Debug.Log($"Spawned enemy Lv{Enemy.Level} HP:{Enemy.Hp}");
    }
}
