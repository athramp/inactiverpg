using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CharacterCreatePanel : MonoBehaviour
{
    [Header("Inputs")]
    public TMP_InputField NameInput;

    [Header("Buttons")]
    public Button WarriorBtn;
    public Button MageBtn;
    public Button ArcherBtn;
    public Button CreateButton;

    [Header("Visual State")]
    public Image WarriorImage;
    public Image MageImage;
    public Image ArcherImage;
    public Color SelectedColor = new Color(1f, 1f, 1f, 1f);
    public Color UnselectedColor = new Color(1f, 1f, 1f, 0.4f);

    [Header("Feedback")]
    public TMP_Text ErrorText;

    string selectedClass = null;
    string ServerId => PlayerPrefs.GetString("serverId", "WindlessDesert18");

    void Awake() {
        // Wire clicks (or do this in Inspector)
        WarriorBtn.onClick.AddListener(() => Pick("Warrior"));
        MageBtn.onClick.AddListener(() => Pick("Mage"));
        ArcherBtn.onClick.AddListener(() => Pick("Archer"));
        UpdateVisuals();
    }

    void Pick(string cls) {
        selectedClass = cls;
        UpdateVisuals();
    }

    void UpdateVisuals() {
        SetImage(WarriorImage, selectedClass == "Warrior");
        SetImage(MageImage, selectedClass == "Mage");
        SetImage(ArcherImage, selectedClass == "Archer");
    }
    void SetImage(Image img, bool sel) {
        if (!img) return;
        img.color = sel ? SelectedColor : UnselectedColor;
        img.transform.localScale = sel ? Vector3.one * 1.06f : Vector3.one;
    }

    public async void OnCreateClicked() {
        CreateButton.interactable = false;
        ErrorText.text = "";
        try {
            var name = NameInput.text.Trim();
            if (string.IsNullOrEmpty(name)) { ErrorText.text = "Enter a name."; goto done; }
            if (selectedClass == null)     { ErrorText.text = "Pick a class."; goto done; }

            var user = EmailAuth.I.CachedUser();
            if (user == null) { ErrorText.text = "Not signed in."; goto done; }

            await CharacterService.CreateAsync(ServerId, user.UserId, name, selectedClass);
            var boot = FindObjectOfType<RuntimeBootstrap>();
            if (boot != null) await boot.StartForCurrentUserAsync();
            gameObject.SetActive(false);

            // Continue to the main game / load scene here
            // e.g., SceneManager.LoadScene("Game");
        }
        catch (System.Exception e) { ErrorText.text = e.Message; }
        done:
        CreateButton.interactable = true;
    }
}
