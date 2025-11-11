using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SkillMenuPanel : MonoBehaviour
{
    [Header("Refs")]
    public SkillLoadout loadout;
    public Transform knownListRoot;      // parent for buttons (scroll content)
    public Button knownSkillButtonPrefab;

    public Transform slotsRoot;          // parent with N children (slot buttons)
    public Button slotButtonPrefab;      // if you want to spawn; or wire existing buttons

    public TMP_Text detailName;
    public TMP_Text detailDesc;
    public Image    detailIcon;

    SkillProfile _selectedKnown;
    int _selectedSlot = -1;

    void OnEnable()
    {
        BuildKnownList();
        BuildSlotsUI();
        ShowDetails(null);
    }

    void BuildKnownList()
    {
        foreach (Transform c in knownListRoot) Destroy(c.gameObject);
        if (!loadout || !loadout.catalog) return;

        foreach (var s in loadout.catalog.all)
        {
            if (!s) continue;
            var b = Instantiate(knownSkillButtonPrefab, knownListRoot);
            b.GetComponentInChildren<TMP_Text>(true).text = s.name;
            var icon = b.GetComponentInChildren<Image>(true);
            if (icon) icon.sprite = s.icon; // if SkillProfile has icon
            b.onClick.AddListener(() => { _selectedKnown = s; ShowDetails(s); });
        }
    }

    void BuildSlotsUI()
    {
        // assume slotsRoot already has N buttons; otherwise instantiate from prefab
        var n = loadout.slots.Length;
        for (int i=0; i<n; i++)
        {
            Button b;
            if (i < slotsRoot.childCount) b = slotsRoot.GetChild(i).GetComponent<Button>();
            else b = Instantiate(slotButtonPrefab, slotsRoot);

            int idx = i;
            UpdateSlotVisual(idx, b);
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() =>
            {
                _selectedSlot = idx;
                if (_selectedKnown != null) { loadout.SetSlot(idx, _selectedKnown); UpdateSlotVisual(idx, b); }
            });
        }
    }

    void UpdateSlotVisual(int idx, Button b)
    {
        var s = loadout.GetSlot(idx);
        var label = b.GetComponentInChildren<TMP_Text>(true);
        var icon  = b.GetComponentInChildren<Image>(true);
        if (label) label.text = s ? s.name : $"Slot {idx+1}";
        if (icon)  icon.sprite = s ? s.icon : null;
    }

    void ShowDetails(SkillProfile s)
    {
        if (detailName) detailName.text = s ? s.name : "Select a skill";
        if (detailDesc) detailDesc.text = s ? s.description : "";
        if (detailIcon) detailIcon.sprite = s ? s.icon : null;
    }
}
