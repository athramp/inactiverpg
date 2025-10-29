// Assets/Scripts/Systems/PlayerPersistenceService.cs
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using Firebase.Auth;
using Firebase.Firestore;

public class PlayerPersistenceService : MonoBehaviour
{
    public GameLoopService gameLoop;
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
        if (gameLoop == null) { Debug.LogError("[Progress] 'gameLoop' not assigned on PlayerPersistenceService."); return; }
        var docRef = db.Collection("servers").Document(ServerId)
                       .Collection("characters").Document(user.UserId);

        try
        {
            var snap = await docRef.GetSnapshotAsync();
            if (!snap.Exists) { Debug.Log("[Progress] No doc yet."); return; }

            var d = snap.ToDictionary();

            string classId = d.TryGetValue("class", out var c) ? c?.ToString() : "Warrior";
            int level = d.TryGetValue("level", out var lv) && lv != null ? System.Convert.ToInt32(lv) : 1;
            int xp    = d.TryGetValue("xp", out var xpv) && xpv != null ? System.Convert.ToInt32(xpv) : 0;
            int hp    = d.TryGetValue("hp", out var hpv) && hpv != null ? System.Convert.ToInt32(hpv) : 0;

            // Initialize loop if not yet, then apply progress
            if (!gameLoop.IsInitialized)
            {
                if (string.IsNullOrEmpty(classId)) classId = "Warrior";
                gameLoop.Initialize(classId);
            }
            gameLoop.Player.ApplyProgress(classId, level, xp, hp);
            // NEW: set display name from the document if present
            string displayName = "Hero";
            if (d.TryGetValue("name", out var n) && n is string s && !string.IsNullOrWhiteSpace(s))
                displayName = s;
            Debug.Log($"[Persistence] Loaded name: {displayName}");
            gameLoop.SetPlayerDisplayName(displayName);
            // Convert saved "into-level XP" to total XP expected by PlayerProgression
            var progression = FindObjectOfType<PlayerProgression>();
            if (progression)
            {
                int totalXp = xp + gameLoop.XpTable.GetXpToReachLevel(level);
                progression.InitializeFromSave(level, totalXp);
            }
            gameLoop.Player.ApplyLevelAndRecalculate(level, healToFull: false);
            gameLoop.MarkStatsReady();
            Debug.Log($"[Progress] Loaded: {classId} L{level} xp:{xp} hp:{hp}");
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
            if (user == null || gameLoop == null || gameLoop.Player == null) return;
            var prog = FindObjectOfType<PlayerProgression>();
            if (prog != null && gameLoop?.Player != null)
            {
                gameLoop.Player.Level     = prog.Level;
                gameLoop.Player.CurrentXp = prog.XpIntoLevel; // keep "into-level" XP in Firestore
            }
            var data = gameLoop.Player.ToFirestoreProgress();
            await docRef.SetAsync(data, SetOptions.MergeAll);
            
            Debug.Log("[Progress] Saved");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Progress] Save failed: {e}");
        }
    }

    void OnApplicationPause(bool paused) { if (paused) _ = SaveProgressAsync(); }
    void OnApplicationQuit() { _ = SaveProgressAsync(); }

    IEnumerator Start()
    {
        // Optional autosave loop
        var wait = new WaitForSeconds(autosaveIntervalSec);
        while (true) { yield return wait; _ = SaveProgressAsync(); }
    }
}
