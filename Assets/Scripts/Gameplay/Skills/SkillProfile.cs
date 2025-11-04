using UnityEngine;

public enum SkillRole
{
    Displacement,   // movement-only
    Damage,         // pure damage
    CrowdControl    // (later) stuns, roots, interrupts
}

public enum MovementMode
{
    None,           // e.g., Conflagrate/Smash/Snipe if instant/hitscan
    DashToward,     // Charge
    DashAway,       // Roll
    BlinkAway       // Blink-like snap
}

[CreateAssetMenu(menuName = "InactiveRPG/Skill Profile")]
public class SkillProfile : ScriptableObject
{
    public string displayName = "New Skill";

    [Header("Classification")]
    public SkillRole role = SkillRole.Displacement;
    public MovementMode movement = MovementMode.None;

    [Header("Timing")]
    public float castTime = 0.2f;
    public float cooldown = 6f;
    public float moveDuration = 0.25f;  // 0 for blink-like snap
    public bool grantsIFrames = false;
    public float iFrameDuration = 0.2f;

    [Header("Displacement")]
    public float moveDistance = 3f;   // magnitude; direction comes from MovementMode

    [Header("Damage")]
    public bool isPureDamage = false; // set true for Conflagrate/Snipe/Smash
    public float damageMultiplier = 1.0f;
    public int flatBonusDamage = 0;
    public bool usesProjectile = false;
    public float projectileSpeedOverride = 0f;   // 0 = use AttackProfile speed (engine)

    [Header("Range gating")]
    public float minRangeToUse = 0f;
    public float maxRangeToUse = 999f;

    [Header("VFX/SFX (optional)")]
    public GameObject vfxOnCast;
    public GameObject vfxOnTravel;
    public GameObject vfxOnLand;
}
