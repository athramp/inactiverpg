using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Gameplay.Equipment;

public class CampPanel : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] TMP_Text NameText;
    [SerializeField] Image ClassImage;

    [Header("Class Sprites")]
    [SerializeField] Sprite WarriorSprite;
    [SerializeField] Sprite MageSprite;
    [SerializeField] Sprite ArcherSprite;
    [SerializeField] Sprite UnknownSprite;

    [Header("Player Stats (Live)")]
    [SerializeField] TMP_Text LevelText;
    [SerializeField] TMP_Text HPText;
    [SerializeField] TMP_Text StatsText;
    [SerializeField] TMP_Text GearStatsText;

    [Header("Sources")]
    [SerializeField] CombatOrchestrator orchestrator;

    void Awake()
    {
        if (!orchestrator) orchestrator = FindObjectOfType<CombatOrchestrator>();
    }

    void OnEnable()
    {
        PrimeHeaderFromGameLoop();
        InvokeRepeating(nameof(UpdateRuntimeStats), 0.2f, 0.2f);
    }

    void OnDisable()
    {
        CancelInvoke(nameof(UpdateRuntimeStats));
    }

    void PrimeHeaderFromGameLoop()
    {
        var loop = orchestrator ? orchestrator.gameLoop : null;
        if (loop == null) return;

        if (NameText)
        {
            var displayName = string.IsNullOrEmpty(loop.PlayerDisplayName) ? "Hero" : loop.PlayerDisplayName;
            NameText.text = displayName;
        }

        if (ClassImage && loop.Player != null)
            ClassImage.sprite = SpriteForClass(loop.Player.ClassId);
    }

    void UpdateRuntimeStats()
    {
        if (!orchestrator) return;

        var eng  = orchestrator.DebugEngine;
        var loop = orchestrator.gameLoop;
        var prog = FindObjectOfType<PlayerProgression>();

        if (loop?.Player == null) return;

        if (NameText)
        {
            var n = string.IsNullOrEmpty(loop.PlayerDisplayName) ? "Hero" : loop.PlayerDisplayName;
            if (NameText.text != n) NameText.text = n;
        }

        int level = prog != null ? prog.Level : loop.Player.Level;
        if (LevelText) LevelText.text = $"Lv {level}";

        var ps = loop.Player;
        if (eng != null && HPText)   HPText.text   = $"HP {eng.Player.Hp}/{eng.Player.MaxHp}";
        if (StatsText) StatsText.text = $"ATK {ps.Atk}  •  DEF {ps.Def}";
        if (GearStatsText)
        {
            var gear = ps.EquipmentBonus;
            GearStatsText.text =
                $"Gear Bonus\n" +
                $"ATK +{gear.attack}\n" +
                $"DEF +{gear.defense}\n" +
                $"HP +{gear.maxHp}\n" +
                $"Crit {ps.GetSubstat(GearSubstatType.CritChance):0.##}%  " +
                $"Evasion {ps.GetSubstat(GearSubstatType.Evasion):0.##}%";
        }
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
