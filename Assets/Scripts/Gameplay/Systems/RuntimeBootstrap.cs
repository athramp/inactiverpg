// Assets/Scripts/Systems/RuntimeBootstrap.cs
using System.Threading.Tasks;
using UnityEngine;
using Firebase.Auth;

public class RuntimeBootstrap : MonoBehaviour
{
    public GameLoopService gameLoop;
    public PlayerPersistenceService persistence;

    string ServerId => PlayerPrefs.GetString("serverId", "WindlessDesert18");

    void Start()
    {
        Debug.Log("=== [RuntimeBootstrap Diagnostics] ===");
        Debug.Log($"GameLoopService count: {FindObjectsOfType<GameLoopService>().Length}");
        Debug.Log($"PlayerPersistenceService count: {FindObjectsOfType<PlayerPersistenceService>().Length}");
        Debug.Log($"RuntimeBootstrap present: {FindObjectOfType<RuntimeBootstrap>() != null}");
        Debug.Log($"FirebaseGate present: {FindObjectOfType<FirebaseGate>() != null}");
        Debug.Log("======================================");
    }

    // Call this after login when a character doc exists (or right after character creation)
    public async Task StartForCurrentUserAsync()
    {
        await FirebaseGate.WaitUntilReady();
        if (gameLoop == null || persistence == null)
        {
            Debug.LogError("[Bootstrap] Missing refs (GameLoop or Persistence). Assign in Inspector.");
            return;
        }

        var uid = FirebaseAuth.DefaultInstance.CurrentUser?.UserId;
        if (uid == null) { Debug.LogWarning("[Bootstrap] No user."); return; }

        // Read the character doc so we get the classId to init the loop
        var data = await CharacterService.GetAsync(ServerId, uid);
        if (data == null) { Debug.LogWarning("[Bootstrap] No character doc."); return; }

        var cls = data.TryGetValue("class", out var v) && v != null ? v.ToString() : "Warrior";

        // Initialize gameplay and then load progress (level/xp/hp)
        if (!gameLoop.IsInitialized) gameLoop.Initialize(cls);
        await persistence.LoadProgressAsync();

        Debug.Log($"[Bootstrap] Ready with class {cls}");
    }
}
