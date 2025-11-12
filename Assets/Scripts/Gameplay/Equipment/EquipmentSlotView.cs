using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Gameplay.Equipment;

public class EquipmentSlotView : MonoBehaviour
{
    public GearSlot slotType;

    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text rarityText;
    [SerializeField] private TMP_Text statsText;
    [SerializeField] private Button unequipButton;

    private EquipmentSlots equipmentSlots;

    void Awake()
    {
        if (!equipmentSlots)
            equipmentSlots = FindObjectOfType<EquipmentSlots>();
        if (unequipButton)
        {
            unequipButton.onClick.RemoveAllListeners();
            unequipButton.onClick.AddListener(() =>
            {
                equipmentSlots?.Unequip(slotType);
            });
        }
    }

    public void Bind(GearInstance inst)
    {
        if (icon) icon.sprite = inst?.item?.icon;
        if (nameText) nameText.text = inst?.item?.displayName ?? $"(Empty {slotType})";
        if (rarityText) rarityText.text = inst != null ? inst.rarity.ToString() : "";
        if (statsText) statsText.text = BuildStatsPreview(inst);
        if (unequipButton) unequipButton.interactable = inst != null;
    }

    private string BuildStatsPreview(GearInstance inst)
    {
        if (inst == null) return "";
        StringBuilder sb = new();
        var stats = inst.TotalStats;
        if (stats.attack != 0) sb.AppendLine($"+{stats.attack} ATK");
        if (stats.defense != 0) sb.AppendLine($"+{stats.defense} DEF");
        if (stats.maxHp != 0) sb.AppendLine($"+{stats.maxHp} HP");
        if (inst.Substats != null)
        {
            foreach (var roll in inst.Substats)
                sb.AppendLine($"+{roll.value:0.##}% {roll.type}");
        }
        return sb.ToString().TrimEnd();
    }
}
