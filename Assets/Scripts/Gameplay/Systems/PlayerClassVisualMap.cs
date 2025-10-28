using UnityEngine;

[CreateAssetMenu(menuName="RPG/PlayerClassVisualMap")]
public class PlayerClassVisualMap : ScriptableObject
{
    [System.Serializable] public struct Entry { public string classId; public AnimatorOverrideController aoc; }
    public Entry[] entries;
    public AnimatorOverrideController Get(string id) {
        foreach (var e in entries) if (e.classId == id) return e.aoc;
        return null;
    }
}
