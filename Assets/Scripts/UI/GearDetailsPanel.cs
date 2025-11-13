using System.Text;
using Gameplay.Equipment;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GearDetailsPanel : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text rarityText;
    [SerializeField] private TMP_Text slotText;
    [SerializeField] private TMP_Text statsText;
    [SerializeField] private Button equipButton;
    [SerializeField] private Button unequipButton;

    private System.Action<GearInstance> _equipAction;
    private System.Action<GearSlot> _unequipAction;
    private GearInstance _current;
    private GearSlot _slot;

    void Awake()
    {
        if (panelRoot) panelRoot.SetActive(false);
        if (equipButton) equipButton.onClick.AddListener(OnEquipClicked);
        if (unequipButton) unequipButton.onClick.AddListener(OnUnequipClicked);
    }

    public void ShowEquipped(GearSlot slot, GearInstance inst, System.Action<GearSlot> onUnequip)
    {
        _slot = slot;
        _current = inst;
        _equipAction = null;
        _unequipAction = onUnequip;
        Populate(inst, slot);
        if (equipButton) equipButton.gameObject.SetActive(false);
        if (unequipButton) unequipButton.gameObject.SetActive(inst != null && onUnequip != null);
        if (panelRoot) panelRoot.SetActive(true);
    }

    public void ShowInventory(GearInstance inst, System.Action<GearInstance> onEquip)
    {
        _slot = inst?.item != null ? inst.item.slot : default;
        _current = inst;
        _equipAction = onEquip;
        _unequipAction = null;
        Populate(inst, _slot);
        if (equipButton) equipButton.gameObject.SetActive(onEquip != null && inst != null);
        if (unequipButton) unequipButton.gameObject.SetActive(false);
        if (panelRoot) panelRoot.SetActive(true);
    }

    public void Hide()
    {
        if (panelRoot) panelRoot.SetActive(false);
        _equipAction = null;
        _unequipAction = null;
        _current = null;
    }

    private void Populate(GearInstance inst, GearSlot slot)
    {
        if (iconImage) iconImage.sprite = inst?.item?.icon;
        if (nameText) nameText.text = inst?.item ? inst.item.displayName : "Empty";
        if (rarityText) rarityText.text = inst != null ? inst.rarity.ToString() : "";
        if (slotText) slotText.text = slot.ToString();
        if (statsText) statsText.text = BuildStats(inst);
    }

    private string BuildStats(GearInstance inst)
    {
        if (inst == null) return "No gear equipped.";
        var stats = inst.TotalStats;
        StringBuilder sb = new();
        sb.AppendLine($"Lv {inst.level}");
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

    private void OnEquipClicked()
    {
        if (_current != null)
            _equipAction?.Invoke(_current);
        Hide();
    }

    private void OnUnequipClicked()
    {
        _unequipAction?.Invoke(_slot);
        Hide();
    }
}
