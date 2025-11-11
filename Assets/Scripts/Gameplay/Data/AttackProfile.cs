using UnityEngine;

public enum AttackKind { Melee, Hitscan, Projectile, AoEInstant, AoEPersistent }

[CreateAssetMenu(menuName = "RPG/Combat/Attack Profile")]
public class AttackProfile : ScriptableObject
{
    [Header("Timing")]
    public float period = 1.2f;         // seconds between attack starts
    public float windup = 0.3f;         // time to impact start

    [Header("Ranges & Shape")]
    public AttackKind kind = AttackKind.Melee;
    public float reach = 1.5f;          // used by Melee/Hitscan
    public float projectileSpeed = 12f; // used by Projectile
    public float aoeRadius = 2.5f;      // used by AoE
    public int targetCap = 1;           // 0 = unlimited

    [Header("Damage")]
    public float variance = 0f;         // 0..1 (optional)
    public float critChance = 0f;       // 0..1
    public float critMult = 2f;         // 2x default
    public GameObject projectilePrefab; 

    // You can extend later with status effects, tags, etc.
}
