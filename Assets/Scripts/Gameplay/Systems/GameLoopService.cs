// Assets/Scripts/Systems/GameLoopService.cs
using UnityEngine;
using Gameplay.Equipment;

public class GameLoopService : MonoBehaviour
{
    [Header("Data")]
    public ClassCatalog classCatalog;
    public XpTable xpTable;

    [Header("Monsters")]
    [Tooltip("Assign 1..N monsters here; will be randomly picked for spawns.")]
    public MonsterDef[] monsterCatalog;

    // Exposed for Orchestrator
    public MonsterDef CurrentMonsterDef { get; private set; }
    public bool CurrentIsBoss { get; private set; }

    // Player
    public string PlayerDisplayName { get; private set; } = "Hero";
    public void SetPlayerDisplayName(string name) =>
        PlayerDisplayName = string.IsNullOrWhiteSpace(name) ? "Hero" : name;

    public PlayerStats Player { get; private set; }

    [Header("Player Equipment")]
    [SerializeField] private Gameplay.Equipment.EquipmentSlots equipmentSlots;

    // Legacy single-enemy snapshot (still used by some UI/scripts)
    // Safe to keep; Orchestrator no longer reads from here for combat.
    public EnemyStats Enemy { get; private set; }

    public XpTable XpTable => xpTable;

    [SerializeField] private PlayerPersistenceService persistence;

    public bool StatsReady { get; private set; }
    public bool IsInitialized { get; private set; }

    // Legacy/optional events (kept for back-compat; can be removed later)
    public event System.Action<int> OnPlayerHit;   // dmg dealt TO enemy
    public event System.Action<int> OnEnemyHit;    // dmg dealt TO player
    public event System.Action OnEnemyKilled;
    public event System.Action OnPlayerKilled;

    [Header("Boss Rules")]
    [SerializeField] private int bossEvery = 5;  // every N spawns becomes a boss
    private int _enemySpawnCount = 0;

    void Awake()
    {
        if (!persistence)
            persistence = FindObjectOfType<PlayerPersistenceService>();
    }

    /// <summary>Call this after login / class selection.</summary>
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
            Debug.LogError("[GameLoop] Missing ClassCatalog / XpTable (assign on GameRoot).");
            return;
        }

        Player = new PlayerStats(classCatalog, xpTable, classId);
        if (!equipmentSlots) equipmentSlots = FindObjectOfType<Gameplay.Equipment.EquipmentSlots>();
        Player.AttachEquipment(equipmentSlots);
        // Seed one enemy definition for initial UI; Orchestrator will actually spawn visuals.
        RollNextEnemy(out var def, out var isBoss);
        BuildLegacyEnemySnapshot(def, isBoss);

        StatsReady = true;
        IsInitialized = true;

        Debug.Log($"[GameLoop] Initialized class={classId}, first enemy='{def?.monsterId ?? "NULL"}', boss={isBoss}");
    }

    // ----------------- Spawn helpers (used by Orchestrator) -----------------

    /// <summary>Return a random MonsterDef from the catalog (no boss logic).</summary>
    public MonsterDef GetRandomMonsterDef()
    {
        if (monsterCatalog == null || monsterCatalog.Length == 0)
        {
            Debug.LogError("[GameLoop] Monster catalog empty!");
            return null;
        }
        int idx = Random.Range(0, monsterCatalog.Length);
        return monsterCatalog[idx];
    }

    /// <summary>Increments internal counter, returns (def, isBoss) for the next spawn.</summary>
    public void RollNextEnemy(out MonsterDef def, out bool isBoss)
    {
        def = GetRandomMonsterDef();
        _enemySpawnCount++;
        isBoss = (bossEvery > 0 && _enemySpawnCount % bossEvery == 0);
        CurrentMonsterDef = def;
        CurrentIsBoss = isBoss;
    }

    /// <summary>
    /// (Legacy) Builds EnemyStats snapshot from def. Kept for UI/back-compat; combat doesn’t use it.
    /// </summary>
    public void BuildLegacyEnemySnapshot(MonsterDef def, bool isBoss)
    {
        if (!def)
        {
            Enemy = new EnemyStats(Mathf.Max(1, Player?.Level ?? 1));
            CurrentMonsterDef = null;
            CurrentIsBoss = false;
            Debug.LogWarning("[GameLoop] Null MonsterDef for legacy snapshot.");
            return;
        }

        int lvl = Mathf.Max(1, Player?.Level ?? 1);
        Enemy = new EnemyStats(lvl)
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

        Debug.Log($"[GameLoop] Legacy enemy snapshot: {Enemy.MonsterId} (Lv{lvl}) HP:{Enemy.Hp}/{Enemy.HpMax} ATK:{Enemy.Atk} DEF:{Enemy.Def} XP:{Enemy.XpReward} boss={isBoss}");
    }

    // ----------------- Legacy method (kept; safe) -----------------

    /// <summary>
    /// Legacy single-enemy method. Keeps CurrentMonsterDef/IsBoss and Enemy snapshot updated
    /// for any UI still reading it. Orchestrator should use RollNextEnemy + SpawnEnemyFromDef.
    /// </summary>
    public void SpawnEnemy()
    {
        RollNextEnemy(out var def, out var isBoss);
        BuildLegacyEnemySnapshot(def, isBoss);
    }
}
