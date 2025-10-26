using UnityEngine;

public static class DamageCalculator
{
    public static int ComputeDamage(int atk, int def)
    {
        int dmg = Mathf.Max(1, atk - def);
        return dmg;
    }
}
