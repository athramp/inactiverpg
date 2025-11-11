using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName="Combat/Skill Catalog")]
public class SkillCatalog : ScriptableObject
{
    public List<SkillProfile> all; // assign in Inspector
}

public class SkillLoadout : MonoBehaviour
{
    public SkillCatalog catalog;           // assign the catalog
    [Range(1, 6)] public int slotCount = 4;

    // current selection (indices into catalog or direct refs)
    public SkillProfile[] slots;

    void Awake()
    {
        if (slots == null || slots.Length != slotCount)
            slots = new SkillProfile[slotCount];
    }

    public void SetSlot(int i, SkillProfile s)
    {
        if (i < 0 || i >= slots.Length) return;
        slots[i] = s;
    }

    public SkillProfile GetSlot(int i) => (i >=0 && i < slots.Length) ? slots[i] : null;
}
