using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Firebase.Firestore;
using Firebase.Auth;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

public class ChatInputBar : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField InputField;
    public Button SendButton;

    [Header("Chat Window")]
    public RectTransform ChatWindow;         // the panel we made (inactive by default)
    public ScrollRect ScrollView;            // the ScrollRect on ScrollView
    public RectTransform MessagesContent;    // the Content under Viewport
    public GameObject ChatLineItemPrefab;    // the TMP_Text prefab (ChatLineItem)

    [Header("Options")]
    public float BottomOffset = 160f;        // matches your input bar offset
    public int FetchLimit = 50;

    FirebaseFirestore db;
    ListenerRegistration reg;
    string serverId => PlayerPrefs.GetString("serverId", "WindlessDesert18");

    async void Start()
    {
        await FirebaseGate.WaitUntilReady();
        db = FirebaseFirestore.DefaultInstance;

        SendButton.onClick.AddListener(OnSendClicked);
        InputField.onSelect.AddListener(_ => ShowWindow()); // open when focused
    }

    void OnDisable()
    {
        StopListening();
    }

    // ---- SEND ----
    async void OnSendClicked()
    {
        string text = InputField.text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        InputField.text = "";

        await FirebaseGate.WaitUntilReady();

        var user = FirebaseAuth.DefaultInstance.CurrentUser;
        string uid = user?.UserId ?? "nouid";

        // Pull character name like in CampPanel (optional: cache the name)
        string name = await GetCharacterNameAsync(uid);

        var doc = new Dictionary<string, object>
        {
            { "text", text },
            { "name", name },
            { "uid", uid },
            { "ts", FieldValue.ServerTimestamp }
        };

        try
        {
            await db.Collection("servers").Document(serverId)
                    .Collection("chat").AddAsync(doc);

            Debug.Log($"[ChatInputBar] Sent as '{name}': {text}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ChatInputBar] Send failed: {e}");
        }
    }

    // ---- WINDOW ----
    public void ShowWindow()
    {
        if (!ChatWindow) return;

        // Position & show
        ChatWindow.gameObject.SetActive(true);
        //var rt = ChatWindow;
        //rt.anchorMin = new Vector2(0, 0);
        //rt.anchorMax = new Vector2(1, 0);
        //rt.pivot = new Vector2(0.5f, 0);
        //rt.anchoredPosition = new Vector2(0, BottomOffset);
        // height via sizeDelta.y (set in Inspector)
        StartListening();
    }

    public void HideWindow()
    {
        if (ChatWindow)
            ChatWindow.gameObject.SetActive(false);

        
        StopListening();
    Debug.Log("[Chat] ChatWindow closed by user.");
    }

    // ---- LISTEN ----
    void StartListening()
    {
        StopListening(); // just in case

        ClearMessages();

        // Most-compatible query: ascending by ts, last N
        var col = db.Collection("servers").Document(serverId).Collection("chat");
        Query query = null;

        // Try LimitToLast if available; otherwise fallback
        try {
            query = col.OrderBy("ts").LimitToLast(FetchLimit);
        } catch {
            query = col.OrderBy("ts");
        }

        reg = query.Listen(snap =>
        {
            ClearMessages();
            // Build list in order (ascending)
            var msgs = new List<(string name, string text)>();
            foreach (var doc in snap.Documents)
            {
                var d = doc.ToDictionary();
                var name = (d.TryGetValue("name", out var n) && n != null) ? n.ToString() : "Unknown";
                var text = (d.TryGetValue("text", out var t) && t != null) ? t.ToString() : "";
                msgs.Add((name, text));
            }

            // If we didn’t have LimitToLast, trim here
            if (msgs.Count > FetchLimit)
                msgs = msgs.Skip(msgs.Count - FetchLimit).ToList();

            foreach (var m in msgs)
                AddLine($"{m.name}: {m.text}");

            ScrollToBottom();
        });

        Debug.Log("[ChatInputBar] Chat listener started.");
    }

    void StopListening()
    {
        reg?.Stop();
        reg = null;
    }

    // ---- UI add & helpers ----
    void ClearMessages()
    {
        if (!MessagesContent) return;
        for (int i = MessagesContent.childCount - 1; i >= 0; i--)
            Destroy(MessagesContent.GetChild(i).gameObject);
    }

    void AddLine(string text)
    {
        if (!ChatLineItemPrefab || !MessagesContent) return;
        var go = Instantiate(ChatLineItemPrefab, MessagesContent);
        var tmp = go.GetComponent<TMP_Text>() ?? go.GetComponentInChildren<TMP_Text>();
        if (tmp) tmp.text = text;
    }

    void ScrollToBottom()
    {
        if (!ScrollView) return;
        Canvas.ForceUpdateCanvases();
        ScrollView.verticalNormalizedPosition = 0f; // 0 = bottom
        Canvas.ForceUpdateCanvases();
    }

    // Reuse your character name source (like CampPanel)
    async Task<string> GetCharacterNameAsync(string uid)
    {
        try
        {
            var d = await CharacterService.GetAsync(serverId, uid);
            if (d != null && d.TryGetValue("name", out var n) && n != null)
                return n.ToString();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ChatInputBar] Failed to load char name: {e}");
        }
        return "Player";
    }
}
