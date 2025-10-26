// CharacterService.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;

public static class CharacterService
{
    static async Task<FirebaseFirestore> DBAsync()
    {
        await FirebaseGate.WaitUntilReady();
        return FirebaseFirestore.DefaultInstance;
    }

    static async Task<DocumentReference> DocAsync(string serverId, string uid)
    {
        var db = await DBAsync();
        return db.Collection("servers").Document(serverId)
                 .Collection("characters").Document(uid);
    }

    public static async Task<Dictionary<string, object>> GetAsync(string serverId, string uid)
    {
        var doc = await DocAsync(serverId, uid);
        var snap = await doc.GetSnapshotAsync();
        return snap.Exists ? snap.ToDictionary() : null;
    }

    public static async Task CreateAsync(string serverId, string uid, string name, string @class)
    {
        var doc = await DocAsync(serverId, uid);
        var data = new Dictionary<string, object> {
            { "name", name },
            { "class", @class },
            { "level", 1 },
            { "createdAt", FieldValue.ServerTimestamp },
            { "lastLogin", FieldValue.ServerTimestamp },
        };
        await doc.SetAsync(data, SetOptions.Overwrite);
    }

    public static async Task TouchLoginAsync(string serverId, string uid)
    {
        var doc = await DocAsync(serverId, uid);
        await doc.UpdateAsync("lastLogin", FieldValue.ServerTimestamp);
    }

    public class CharacterData
    {
        public string userId;
        public string name;
        public string classId;
        public int level;
    }

    public static async Task<CharacterData> GetTypedAsync(string serverId, string uid)
    {
        var doc = await DocAsync(serverId, uid);
        var snap = await doc.GetSnapshotAsync();
        if (!snap.Exists) return null;

        var d = snap.ToDictionary();

        string S(params string[] keys)
        {
            foreach (var k in keys)
                if (d.TryGetValue(k, out var v) && v != null)
                    return v.ToString();
            return null;
        }
        int I(params string[] keys)
        {
            foreach (var k in keys)
                if (d.TryGetValue(k, out var v) && v != null)
                {
                    if (v is long l) return (int)l;
                    if (v is int i)  return i;
                    if (v is double f) return (int)f;
                    if (int.TryParse(v.ToString(), out var n)) return n;
                }
            return 1;
        }

        return new CharacterData {
            userId  = uid,
            name    = S("name") ?? "Unknown",
            classId = (S("class") ?? "warrior").ToLowerInvariant(),
            level   = I("level")
        };
    }

    public static async Task UpdateNameAsync(string serverId, string uid, string newName) =>
        await (await DocAsync(serverId, uid)).UpdateAsync("name", newName);

    public static async Task UpdateClassAsync(string serverId, string uid, string newClassId) =>
        await (await DocAsync(serverId, uid)).UpdateAsync("class", newClassId);
}
