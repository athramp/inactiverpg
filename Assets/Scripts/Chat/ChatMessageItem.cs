using TMPro;
using UnityEngine;

public class ChatMessageItem : MonoBehaviour
{
    public TMP_Text NameText;
    public TMP_Text BodyText;

    public void Bind(string name, string text)
    {
        NameText.text = $"{name}:";
        BodyText.text = text;
    }
}
