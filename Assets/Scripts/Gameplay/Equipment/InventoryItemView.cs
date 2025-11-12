using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Gameplay.Equipment;

public class InventoryItemView : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text rarityText;
    [SerializeField] private TMP_Text statsText;
    [SerializeField] private Button equipButton;

    private GearInstance boundItem;
    private System.Action<GearInstance> onEquip;

    public void Bind(GearInstance item, bool isEquipped, System.Action<GearInstance> onEquipCallback)
    {
        boundItem = item;
        onEquip = onEquipCallback;
        if (icon) icon.sprite = item?.item?.icon;
        if (nameText) nameText.text = item != null ? item.item.displayName : "-";
        if (rarityText) rarityText.text = item != null ? item.rarity.ToString() : "";
        if (statsText) statsText.text = BuildStatsPreview(item);
        if (equipButton)
        {
            equipButton.onClick.RemoveAllListeners();
            equipButton.onClick.AddListener(() => {
                Debug.Log($"Equip clicked for {item?.item?.displayName}");
                onEquip?.Invoke(boundItem);
            });
            equipButton.interactable = item != null && !isEquipped;
        }
    }

    private string BuildStatsPreview(GearInstance inst)
    {
        if (inst == null) return "";
        System.Text.StringBuilder sb = new();
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
