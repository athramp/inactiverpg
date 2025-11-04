// Assets/Scripts/Gameplay/Skills/SkillProfile.cs
using UnityEngine;

public enum SkillKind { Charge, Roll, Blink }

[CreateAssetMenu(menuName = "InactiveRPG/Skill Profile")]
public class SkillProfile : ScriptableObject
{
    public string displayName;
    public SkillKind kind;

    [Header("Timing")]
    public float castTime = 0.2f;       // time before it fires/moves
    public float cooldown = 6f;         // seconds
    public float moveDuration = 0.25f;  // how long the motion lasts (0 for blink)
    public bool grantsIFrames = false;
    public float iFrameDuration = 0.2f; // overlapping with cast/move is fine

    [Header("Movement")]
    public float moveDistance = 3f;     // +forward for Charge, -back for Roll, Blink uses this as back distance

    [Header("Combat Add-ons")]
    public float bonusDamageMultiplier = 1.0f; // e.g., Charge 1.5x
    public float minRangeToUse = 0f;           // use only if target farther than this (e.g., Charge)
    public float maxRangeToUse = 999f;         // or only if within this (e.g., Roll if too close)

    [Header("VFX/SFX (optional)")]
    public GameObject vfxOnCast;
    public GameObject vfxOnTravel;
    public GameObject vfxOnLand;
    // SkillProfile.cs  (inside class)
    public enum DamageDelivery { Instant, Projectile } // VFX later
    [Header("Damage (pure hit)")]
    public bool isPureDamage = false;
    [Range(0f, 10f)] public float damageMultiplier = 1f;  // scales base damage
    public int flatBonusDamage = 0;                       // adds on top
    public DamageDelivery delivery = DamageDelivery.Instant;

    // Optional: choose a projectile prefab/speed later (for Snipe/Conflagrate if wanted)
    public float overrideProjectileSpeed = 0f; // 0 = use AttackProfile or instant
}
