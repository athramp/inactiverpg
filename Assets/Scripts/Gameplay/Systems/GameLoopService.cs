// Assets/Scripts/Systems/GameLoopService.cs
using System.Collections;
using UnityEngine;

public class GameLoopService : MonoBehaviour
{
    [Header("Data")]
    public ClassCatalog classCatalog;
    public XpTable xpTable;
    [Header("Monsters")]
    public MonsterDef[] monsterCatalog;         // assign in Inspector
    public MonsterDef CurrentMonsterDef { get; private set; }
    public bool CurrentIsBoss { get; private set; }
    public string PlayerDisplayName { get; private set; } = "Hero";
    public void SetPlayerDisplayName(string name) => PlayerDisplayName = string.IsNullOrWhiteSpace(name) ? "Hero" : name;
    public PlayerStats Player { get; private set; }
    public EnemyStats  Enemy  { get; private set; }
    public XpTable XpTable => xpTable;
        [SerializeField] private PlayerPersistenceService persistence;
    public bool StatsReady { get; private set; } = false;
    public void MarkStatsReady() => StatsReady = true;
    public bool IsInitialized { get; private set; }

    public event System.Action<int> OnPlayerHit;   // dmg dealt to enemy
    public event System.Action<int> OnEnemyHit;    // dmg dealt to player
    public event System.Action OnEnemyKilled;
    public event System.Action OnPlayerKilled;
    public bool IsEngaged { get; private set; }
    [Header("Boss Rules")]
    [SerializeField] private int bossEvery = 5;  // spawn boss every 10 enemies
    private int _enemySpawnCount = 0;
    public void BeginEngagement()
{
    if (IsEngaged) return;
    IsEngaged = true;
    Debug.Log("[GameLoop] Engagement started");
}

private void Awake()
{
    if (!persistence)
        persistence = FindObjectOfType<PlayerPersistenceService>();
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
    StatsReady = false;
    IsInitialized = true;
    
    Debug.Log($"[GameLoop] Initialized with class {classId}");
}

    public void SpawnEnemy()
    {
        int enemyLevel = Mathf.Max(1, Player.Level);

        // 1) Pick a MonsterDef (for now: random). Later you’ll pick by stage.
        if (monsterCatalog == null || monsterCatalog.Length == 0)
        {
            Debug.LogError("[GameLoop] No MonsterDefs assigned.");
            CurrentMonsterDef = null;
            Enemy = new EnemyStats(enemyLevel); // fallback
            return;
        }
        var def = monsterCatalog[Random.Range(0, monsterCatalog.Length)];
        CurrentMonsterDef = def;

        // 2) Boss rule (optional)
        _enemySpawnCount++;
        bool isBoss = (bossEvery > 0 && _enemySpawnCount % bossEvery == 0);

        // 3) Build EnemyStats from the def (source of truth)
        Enemy = new EnemyStats(enemyLevel)
        {
            MonsterId  = def.monsterId,
            HpMax      = isBoss ? Mathf.RoundToInt(def.hp  * 3.0f) : def.hp,
            Hp         = isBoss ? Mathf.RoundToInt(def.hp  * 3.0f) : def.hp,
            Atk        = isBoss ? Mathf.RoundToInt(def.atk * 1.8f) : def.atk,
            Def        = isBoss ? Mathf.RoundToInt(def.def * 2.0f) : def.def,
            CritChance = def.critChance,
            CritMult   = def.critMult,
            XpReward   = isBoss ? Mathf.RoundToInt(def.xpReward * 3.0f) : def.xpReward,
        };
        CurrentMonsterDef = def;
        CurrentIsBoss = isBoss;
        Debug.Log($"[GameLoop] Spawned enemy {Enemy.MonsterId} Lv{Enemy.Level} HP:{Enemy.Hp}/{Enemy.HpMax} ATK:{Enemy.Atk} DEF:{Enemy.Def} XP:{Enemy.XpReward}");
    }
}
