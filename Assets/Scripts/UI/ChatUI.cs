using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChatUI : MonoBehaviour
{
    [Header("UI Refs")]
    public TMP_InputField InputField;
    public Button SendButton;
    public Transform MessagesContent;
    public GameObject MessagePrefab;

    async void Start()
    {
        await FirebaseGate.WaitUntilReady();
        ChatService.I.Init();

        // Start listening
        ChatService.I.Listen(AddMessage);

        SendButton.onClick.AddListener(Send);
        InputField.onSubmit.AddListener(_ => Send());
    }

    void Send()
    {
        var text = InputField.text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        var user = Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser;
        var name = PlayerPrefs.GetString("charName", user?.Email ?? "Player");
        ChatService.I.Send(text, name, user?.UserId ?? "nouid");

        InputField.text = "";
    }

    void AddMessage(ChatMessage msg)
    {
        var go = Instantiate(MessagePrefab, MessagesContent);
        go.GetComponent<ChatMessageItem>().Bind(msg.name, msg.text);
    }
}
