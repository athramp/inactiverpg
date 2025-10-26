// CampPanel.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;
using Firebase.Auth;
using System.Collections.Generic;

public class CampPanel : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text NameText;
    public Image ClassImage;

    [Header("Class Sprites")]
    public Sprite WarriorSprite;
    public Sprite MageSprite;
    public Sprite ArcherSprite;
    public Sprite UnknownSprite;
        // CampPanel.cs (top of class)
    [SerializeField] TMPro.TMP_Text LevelText;
    [SerializeField] TMPro.TMP_Text HPText;
    [SerializeField] TMPro.TMP_Text StatsText;

    string ServerId => PlayerPrefs.GetString("serverId", "WindlessDesert18");

    void OnEnable()
    {
        _ = LoadAndBindAsync();
        InvokeRepeating(nameof(UpdateRuntimeStats), 0.25f, 0.25f);
    }
    void OnDisable()
    {
        CancelInvoke(nameof(UpdateRuntimeStats));
    }
    async Task LoadAndBindAsync()
{
    await FirebaseGate.WaitUntilReady();

    var uid = FirebaseAuth.DefaultInstance.CurrentUser?.UserId;
    if (uid == null)
    {
        if (NameText)   NameText.text = "Not signed in";
        if (ClassImage) ClassImage.sprite = UnknownSprite;
        if (LevelText)  LevelText.text = "";
        if (HPText)     HPText.text = "";
        if (StatsText)  StatsText.text = "";
        return;
    }

    Dictionary<string, object> data = null;
    try
    {
        data = await CharacterService.GetAsync(ServerId, uid);
    }
    catch (System.Exception e)
    {
        Debug.LogError($"CampPanel: failed to load character: {e}");
    }

    if (data == null)
    {
        if (NameText)   NameText.text = "Create a character…";
        if (ClassImage) ClassImage.sprite = UnknownSprite;
        if (LevelText)  LevelText.text = "";
        if (HPText)     HPText.text = "";
        if (StatsText)  StatsText.text = "";
        return;
    }

    string name = TryGetString(data, "name") ?? "Unknown";
    string cls  = TryGetString(data, "class") ?? "warrior";

    if (NameText)   NameText.text = name;
    if (ClassImage) ClassImage.sprite = SpriteForClass(cls);

    // Show saved values immediately (then live values will overwrite below)
    int level = TryGetInt(data, "level") ?? 1;
    int hp    = TryGetInt(data, "hp")    ?? 1;
    if (LevelText) LevelText.text = $"Lv {level}";
    if (HPText)    HPText.text    = $"HP {hp}";

    // Try to bind live values if the loop is running
    UpdateRuntimeStats();
}

void UpdateRuntimeStats()
{
    var gl = FindObjectOfType<GameLoopService>();
    if (gl == null || !gl.IsInitialized || gl.Player == null) return;

    var p = gl.Player;

    if (LevelText) LevelText.text = $"Lv {p.Level}";
    if (HPText)    HPText.text    = $"HP {p.Hp} / {p.MaxHp}";
    if (StatsText) StatsText.text = $"ATK {p.Atk}  •  DEF {p.Def}";
}

int? TryGetInt(System.Collections.Generic.Dictionary<string, object> d, string key)
{
    if (d != null && d.TryGetValue(key, out var v) && v != null)
    {
        try { return System.Convert.ToInt32(v); } catch {}
    }
    return null;
}

    string TryGetString(Dictionary<string, object> dict, string key)
    {
        if (dict != null && dict.TryGetValue(key, out var value) && value != null)
            return value.ToString();
        return null;
    }

    Sprite SpriteForClass(string cls)
    {
        switch ((cls ?? "").ToLowerInvariant())
        {
            case "warrior": return WarriorSprite ? WarriorSprite : UnknownSprite;
            case "mage":    return MageSprite    ? MageSprite    : UnknownSprite;
            case "archer":  return ArcherSprite  ? ArcherSprite  : UnknownSprite;
            default:        return UnknownSprite;
        }
    }
}
