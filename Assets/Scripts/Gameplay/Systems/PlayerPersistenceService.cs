// Assets/Scripts/Systems/PlayerPersistenceService.cs
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using Firebase.Auth;
using Firebase.Firestore;
using System.Collections.Generic;
using Gameplay.Equipment;
using Gameplay.Loot;

public class PlayerPersistenceService : MonoBehaviour
{
    [Header("Refs")]
    public GameLoopService gameLoop;
    [SerializeField] private EquipmentInventory inventory;
    [SerializeField] private EquipmentSlots equipmentSlots;
    [SerializeField] private LootTable lootTable;

    [Header("Saving")]
    public float autosaveIntervalSec = 15f;

    string ServerId => PlayerPrefs.GetString("serverId", "WindlessDesert18");
    FirebaseFirestore db;

    void Awake() => DontDestroyOnLoad(gameObject);
    void EnsureRefs()
    {
        if (!inventory) inventory = FindObjectOfType<EquipmentInventory>();
        if (!equipmentSlots) equipmentSlots = FindObjectOfType<EquipmentSlots>();
        if (!lootTable)
        {
            var lamp = FindObjectOfType<Gameplay.Loot.LampProgressionService>();
            if (lamp) lootTable = lamp.LootTableAsset;
        }
    }

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

            EnsureRefs();
            LoadGearFromSave(d);

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

            EnsureRefs();
            SaveGearToMap(data);

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

    void LoadGearFromSave(Dictionary<string, object> data)
    {
        if (inventory == null || equipmentSlots == null || lootTable == null) return;

        var loaded = new List<GearInstance>();
        if (data.TryGetValue("inventory", out var invObj))
        {
            foreach (var entry in EnumerateArray(invObj))
            {
                if (entry is Dictionary<string, object> dict)
                {
                    var inst = DeserializeGear(dict);
                    if (inst != null)
                        loaded.Add(inst);
                }
            }
        }
        inventory.ReplaceAll(loaded);

        var equipMap = new Dictionary<GearSlot, GearInstance>();
        if (data.TryGetValue("equipment", out var eqObj))
        {
            foreach (var kv in EnumerateMap(eqObj))
            {
                if (!System.Enum.TryParse(kv.Key, out GearSlot slot)) continue;
                var inst = inventory.Find(kv.Value?.ToString());
                if (inst != null)
                    equipMap[slot] = inst;
            }
        }
        equipmentSlots.ApplyLoadout(equipMap);
    }

    void SaveGearToMap(Dictionary<string, object> data)
    {
        if (inventory == null || equipmentSlots == null || lootTable == null) return;

        var invList = new List<Dictionary<string, object>>();
        foreach (var inst in inventory.Items)
        {
            var dict = SerializeGear(inst);
            if (dict != null) invList.Add(dict);
        }
        data["inventory"] = invList;

        var equipDict = new Dictionary<string, object>();
        foreach (var (slot, gear) in equipmentSlots.EnumerateSlots())
        {
            if (gear != null)
                equipDict[slot.ToString()] = gear.instanceId;
        }
        data["equipment"] = equipDict;
    }

    Dictionary<string, object> SerializeGear(GearInstance inst)
    {
        if (inst?.item == null) return null;
        var dict = new Dictionary<string, object>
        {
            ["id"] = inst.instanceId,
            ["item"] = inst.item.name,
            ["rarity"] = inst.rarity.ToString(),
            ["level"] = inst.level
        };
        var subs = new List<Dictionary<string, object>>();
        if (inst.Substats != null)
        {
            foreach (var roll in inst.Substats)
            {
                subs.Add(new Dictionary<string, object>
                {
                    ["type"] = roll.type.ToString(),
                    ["value"] = roll.value
                });
            }
        }
        dict["substats"] = subs;
        return dict;
    }

    GearInstance DeserializeGear(Dictionary<string, object> dict)
    {
        if (dict == null || lootTable == null) return null;
        string itemName = dict.TryGetValue("item", out var itemVal) ? itemVal?.ToString() : null;
        var item = lootTable.FindItem(itemName);
        if (!item)
        {
            Debug.LogWarning($"[Progress] Missing GearItem '{itemName}' in loot table.");
            return null;
        }
        string id = dict.TryGetValue("id", out var idVal) ? idVal?.ToString() : null;
        string rarityStr = dict.TryGetValue("rarity", out var rVal) ? rVal?.ToString() : GearRarity.Normal.ToString();
        if (!System.Enum.TryParse(rarityStr, out GearRarity rarity)) rarity = GearRarity.Normal;
        int level = dict.TryGetValue("level", out var lvVal) ? System.Convert.ToInt32(lvVal) : 1;
        var substats = new List<GearSubstatRoll>();
        if (dict.TryGetValue("substats", out var subsObj) && subsObj is object[] subsArr)
        {
            foreach (var entry in subsArr)
            {
                if (entry is Dictionary<string, object> subDict)
                {
                    var typeStr = subDict.TryGetValue("type", out var tVal) ? tVal?.ToString() : null;
                    if (!System.Enum.TryParse(typeStr, out GearSubstatType type)) continue;
                    float value = subDict.TryGetValue("value", out var vVal) ? System.Convert.ToSingle(vVal) : 0f;
                    substats.Add(new GearSubstatRoll { type = type, value = value });
                }
            }
        }
        return GearInstance.Restore(id, item, rarity, level, substats);
    }

    IEnumerable<object> EnumerateArray(object raw)
    {
        if (raw == null) yield break;
        if (raw is object[] arr)
        {
            foreach (var o in arr) yield return o;
        }
        else if (raw is System.Collections.IEnumerable list)
        {
            foreach (var o in list) yield return o;
        }
    }

    IEnumerable<KeyValuePair<string, object>> EnumerateMap(object raw)
    {
        if (raw == null) yield break;
        if (raw is Dictionary<string, object> dict)
        {
            foreach (var kv in dict) yield return kv;
        }
        else if (raw is System.Collections.IDictionary map)
        {
            foreach (System.Collections.DictionaryEntry entry in map)
            {
                yield return new KeyValuePair<string, object>(entry.Key?.ToString() ?? "", entry.Value);
            }
        }
    }
}
