using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    [Header("Sources")]
    [SerializeField] CombatOrchestrator orchestrator; // assign in Inspector

    void Awake()
    {
        if (!orchestrator) orchestrator = FindObjectOfType<CombatOrchestrator>();
    }

    void OnEnable()
    {
        // try to show immediate info from GameLoop (no waiting on network)
        PrimeHeaderFromGameLoop();
        // keep the panel live, like CombatDebugPanel
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

        // Name (if you store one in GameLoop; else leave blank or “Hero”)
        if (NameText)
        {
            var displayName = string.IsNullOrEmpty(loop.PlayerDisplayName) ? "Hero" : loop.PlayerDisplayName;
            NameText.text = displayName;
        }

        // Class sprite from current player class
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
        // Show level from PROGRESSION if available, otherwise from GameLoop
        int level = prog != null ? prog.Level : loop.Player.Level;
        if (LevelText) LevelText.text = $"Lv {level}";

        // Live HP/ATK/DEF from GameLoopService (authoritative runtime stats)
        var ps = loop.Player;
        if (HPText)   HPText.text   = $"HP {eng.Player.Hp}/{eng.Player.MaxHp}";
        if (StatsText) StatsText.text = $"ATK {ps.Atk}  •  DEF {ps.Def}";
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
