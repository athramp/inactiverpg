using System;
using System.Collections.Generic;
using System.Linq;
using Firebase.Firestore;
using UnityEngine;

public class ChatService : MonoBehaviour
{
    public static ChatService I { get; private set; }
    FirebaseFirestore db;
    ListenerRegistration reg;

    void Awake()
    {
        if (I != null && I != this) Destroy(gameObject);
        else I = this;
    }

    public async void Init()
    {
        await FirebaseGate.WaitUntilReady();
        db = FirebaseFirestore.DefaultInstance;
    }

    public void Listen(Action<ChatMessage> onMsg)
    {
        reg?.Stop();
        var serverId = PlayerPrefs.GetString("serverId", "default");

        var query = db.Collection("servers").Document(serverId)
                      .Collection("chat")
                      .OrderBy("ts")
                      .LimitToLast(50);

        reg = query.Listen(snap =>
        {
            foreach (var doc in snap.Documents)
            {
                var data = doc.ToDictionary();
                onMsg?.Invoke(new ChatMessage
                {
                    name = data.GetValueOrDefault("name") as string ?? "???",
                    text = data.GetValueOrDefault("text") as string ?? ""
                });
            }
        });
    }

    public async void Send(string text, string name, string uid)
    {
        try
        {
            var serverId = PlayerPrefs.GetString("serverId", "default");
            var col = db.Collection("servers").Document(serverId).Collection("chat");
            await col.AddAsync(new Dictionary<string, object>
            {
                {"text", text},
                {"name", name},
                {"uid", uid},
                {"ts", FieldValue.ServerTimestamp}
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"[ChatService] Send failed: {e}");
        }
    }
}

public class ChatMessage
{
    public string name;
    public string text;
}
