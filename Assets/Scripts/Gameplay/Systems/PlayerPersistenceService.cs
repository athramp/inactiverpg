// Assets/Scripts/Systems/PlayerPersistenceService.cs
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using Firebase.Auth;
using Firebase.Firestore;

public class PlayerPersistenceService : MonoBehaviour
{
    [Header("Refs")]
    public GameLoopService gameLoop;

    [Header("Saving")]
    public float autosaveIntervalSec = 15f;

    string ServerId => PlayerPrefs.GetString("serverId", "WindlessDesert18");
    FirebaseFirestore db;

    void Awake() => DontDestroyOnLoad(gameObject);

    public async Task LoadProgressAsync()
    {
        await FirebaseGate.WaitUntilReady();
        db ??= FirebaseFirestore.DefaultInstance;

        var user = FirebaseAuth.DefaultInstance.CurrentUser;
        if (user == null) { Debug.LogWarning("[Progress] No user."); return; }
        if (gameLoop == null) { Debug.LogError("[Progress] 'gameLoop' not assigned."); return; }

        var docRef = db.Collection("servers").Document(ServerId)
                       .Collection("characters").Document(user.UserId);

        try
        {
            var snap = await docRef.GetSnapshotAsync();
            if (!snap.Exists) { Debug.Log("[Progress] No character doc yet."); return; }

            var d = snap.ToDictionary();

            string classId = d.TryGetValue("class", out var c) ? (c?.ToString() ?? "Warrior") : "Warrior";
            int level = d.TryGetValue("level", out var lv) && lv != null ? System.Convert.ToInt32(lv) : 1;
            int xpIntoLevel = d.TryGetValue("xp", out var xpv) && xpv != null ? System.Convert.ToInt32(xpv) : 0;
            int hp = d.TryGetValue("hp", out var hpv) && hpv != null ? System.Convert.ToInt32(hpv) : 0;
            string displayName = (d.TryGetValue("name", out var n) && n is string nameStr && !string.IsNullOrWhiteSpace(nameStr)) ? nameStr : "Hero";

            if (!gameLoop.IsInitialized)
                gameLoop.Initialize(string.IsNullOrEmpty(classId) ? "Warrior" : classId);

            gameLoop.SetPlayerDisplayName(displayName);

            // Apply to runtime
            gameLoop.Player.ApplyProgress(classId, level, xpIntoLevel, hp);
            var progression = FindObjectOfType<PlayerProgression>();
            if (progression)
            {
                int totalXp = xpIntoLevel + gameLoop.XpTable.GetXpToReachLevel(level);
                progression.InitializeFromSave(level, totalXp);
            }
            gameLoop.Player.ApplyLevelAndRecalculate(level, healToFull: false);

            // Restore skill loadout (optional)
            var loadout = FindObjectOfType<SkillLoadout>();
            if (loadout && loadout.catalog && d.TryGetValue("skillLoadout", out var arr) && arr is object[] saved)
            {
                for (int i = 0; i < loadout.slots.Length && i < saved.Length; i++)
                {
                    var skillName = saved[i]?.ToString();
                    var skillProfile = loadout.catalog.all.Find(p => p && p.name == skillName); // renamed var (no shadowing)
                    loadout.SetSlot(i, skillProfile);
                }
            }

            Debug.Log($"[Progress] Loaded: name='{displayName}' class={classId} L{level} xpInto={xpIntoLevel} hp={hp}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Progress] Load failed: {e}");
        }
    }

    public async Task SaveProgressAsync()
    {
        await FirebaseGate.WaitUntilReady();
        db ??= FirebaseFirestore.DefaultInstance;

        var user = FirebaseAuth.DefaultInstance.CurrentUser;
        if (user == null || gameLoop?.Player == null) return;

        var docRef = db.Collection("servers").Document(ServerId)
                       .Collection("characters").Document(user.UserId);

        try
        {
            // Sync PlayerProgression → Player before saving
            var prog = FindObjectOfType<PlayerProgression>();
            if (prog != null)
            {
                gameLoop.Player.Level     = prog.Level;
                gameLoop.Player.CurrentXp = prog.XpIntoLevel; // store into-level XP
            }

            // Base map from your existing mapper
            var data = gameLoop.Player.ToFirestoreProgress();

            // Add skill loadout (optional)
            var loadout = FindObjectOfType<SkillLoadout>();
            if (loadout && loadout.catalog)
            {
                var ids = new string[loadout.slots.Length];
                for (int i = 0; i < ids.Length; i++)
                    ids[i] = loadout.slots[i] ? loadout.slots[i].name : "";
                data["skillLoadout"] = ids;
            }

            await docRef.SetAsync(data, SetOptions.MergeAll);
            Debug.Log("[Progress] Saved");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Progress] Save failed: {e}");
        }
    }

    void OnApplicationPause(bool paused) { if (paused) _ = SaveProgressAsync(); }
    void OnApplicationQuit()             { _ = SaveProgressAsync(); }

    IEnumerator Start()
    {
        var wait = new WaitForSeconds(Mathf.Max(3f, autosaveIntervalSec));
        while (true) { yield return wait; _ = SaveProgressAsync(); }
    }
}
