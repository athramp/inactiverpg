using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Gameplay.Equipment;

public class EquipmentSlotView : MonoBehaviour
{
    public GearSlot slotType;

    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private Button slotButton;
    [SerializeField] private GearDetailsPanel detailsPanel;

    private EquipmentSlots equipmentSlots;
    private GearInstance currentGear;

    void Awake()
    {
        if (!equipmentSlots)
            equipmentSlots = FindObjectOfType<EquipmentSlots>();
        if (slotButton)
        {
            slotButton.onClick.RemoveAllListeners();
            slotButton.onClick.AddListener(ShowDetails);
        }
    }

    public void Bind(GearInstance inst)
    {
        currentGear = inst;
        if (icon)
        {
            icon.enabled = inst != null && inst.item != null;
            icon.sprite = inst?.item?.icon;
        }
        if (levelText)
        {
            levelText.gameObject.SetActive(inst != null);
            levelText.text = inst != null ? $"Lv {inst.level}" : string.Empty;
        }
        if (slotButton)
            slotButton.interactable = inst != null;
    }

    private void ShowDetails()
    {
        if (detailsPanel == null) return;
        if (currentGear != null)
            detailsPanel.ShowEquipped(slotType, currentGear, slot => equipmentSlots?.Unequip(slot));
        else
            detailsPanel.Hide();
    }
}
