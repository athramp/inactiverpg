// Assets/Scripts/Gameplay/Monsters/MonsterDef.cs
using UnityEngine;

public enum MonsterTier { Normal, Elite, Boss }

[CreateAssetMenu(menuName="RPG/Monster Def")]
public class MonsterDef : ScriptableObject
{
    [Header("Identity")]
    public string monsterId = "skeleton";
    public string displayName = "Skeleton";
    public MonsterTier tier = MonsterTier.Normal;

    [Header("Stats")]
    public int hp = 100;
    public int atk = 8;
    public int def = 2;
    [Range(0, 1)] public float critChance = 0.02f;
    public float critMult = 1.5f;
    public int xpReward = 12;

    [Header("Spawn/Look")]
    public float baseScale = 20f;  // your project scales sprites up ~20×
    public Color tint = Color.white;

    [Header("Weights (for random pick)")]
    [Range(0, 1)] public float weight = 1f; // relative chance in its pool
}
