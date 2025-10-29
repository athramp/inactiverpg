using UnityEngine;

public static class DamageCalculator
{
    public static int ComputeDamage(int atk, int def)
    {
        Debug.Log($"[DamageCalculator] Computing damage: ATK={atk} DEF={def}");
        int dmg = Mathf.Max(1, atk - def);
        Debug.Log($"[DamageCalculator] Computed damage: {dmg}");
        return dmg;
    }
}
