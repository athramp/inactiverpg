// Assets/Scripts/Gameplay/Skills/SkillProfile.cs
using UnityEngine;

public enum SkillKind { Displacement, Damage, CrowdControl, Support }

public enum DamageType { Physical, Fire, Ice, Lightning }
public enum TargetingType { Self, Enemy, Area }

public enum CCType { Stun, Knockback, Pull, Slow }
public enum SupportType { Heal, Shield, BuffAtk, Haste }

[CreateAssetMenu(menuName = "InactiveRPG/Skill Profile")]
public class SkillProfile : ScriptableObject
{
    [Header("Basics")]
    public string displayName = "New Skill";
    public SkillKind kind = SkillKind.Damage;
    public float castTime = 0.15f;
    public float cooldown = 6f;
    public bool grantsIFrames = false;
    public float iFrameDuration = 0.2f;

    [Header("Use Conditions (1D)")]
    public bool respectRange = true;
    public float minRangeToUse = 0f;
    public float maxRangeToUse = 999f;

    [Header("Targeting")]
    public TargetingType targeting = TargetingType.Enemy;
    public float areaRadius = 1.25f; // for AOE if you add multi-enemy later

    // ---------- Displacement ----------
    [Header("Displacement")]
    public float moveDistance = 3f;        // +forward toward target, -back away
    public float moveDuration = 0.20f;     // 0 for blink

    // ---------- Damage ----------
    [Header("Damage")]
    public bool useProjectile = false;
    public float projectileSpeed = 6f;     // visual speed
    public DamageType damageType = DamageType.Physical;
    public float damageMultiplier = 1.0f;  // 1.5 = +50%
    public int flatBonusDamage = 0;

    // ---------- Crowd Control ----------
    [Header("Crowd Control")]
    public CCType ccType = CCType.Stun;
    public float ccDuration = 1.0f;        // stun/slow duration
    public float ccMagnitude = 2.0f;       // knockback/pull distance or slow factor

    // ---------- Support ----------
    [Header("Support")]
    public SupportType supportType = SupportType.Heal;
    public int supportAmount = 20;         // heal/shield amount
    public float supportDuration = 5f;     // buff durations

    // ---------- VFX (optional) ----------
    [Header("VFX/SFX (optional)")]
    public GameObject vfxOnCast;
    public GameObject vfxOnTravel; // projectile or trail
    public GameObject vfxOnLand;
}
