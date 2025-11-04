// Assets/Scripts/Gameplay/Systems/WorldShifter.cs
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
public class WorldShifter : MonoBehaviour
{
    [Header("Scene Roots")]
    [Tooltip("Parent for everything that should shift with the world: enemies, projectiles, ground props, etc.")]

[SerializeField] private Transform worldRoot;
[SerializeField] private List<ParallaxLayerSingle> parallax = new();

[SerializeField] float debugScrollSpeed = 2.0f;   // world units per second
[SerializeField] float debugFineFactor  = 0.2f;   // hold Shift for finer control

    [Header("Rigidbody Batching (optional)")]
    [Tooltip("If true, we will MovePosition() RB2Ds when shifting to avoid physics jitter.")]
    public bool shiftRigidbodiesSafely = true;

    private readonly List<Rigidbody2D> _rb2dCache = new();

// TEMP: auto-find so it "just works" even if Inspector wasn’t filled
private void Awake()
{
    if (parallax == null || parallax.Count == 0)
    {
        parallax = new List<ParallaxLayerSingle>(FindObjectsOfType<ParallaxLayerSingle>(true));
        Debug.Log($"[WorldShifter] Auto-found parallax layers: {parallax.Count}");
    }
}
    void Update()    { }
    public void ShiftWorld(float dx)
{
    // 1) Ignore microscopic movement
    if (Mathf.Abs(dx) < 1e-4f) return;

    if (!worldRoot)
    {
        Debug.LogWarning("[WorldShifter] worldRoot not assigned.");
        return;
    }

    // 2) Move gameplay world (KEEP your current convention: +dx moves world +x)
    worldRoot.position += new Vector3(dx, 0f, 0f);

    // 3) Drive parallax safely
    if (parallax == null || parallax.Count == 0) return;

    // Optional: comment next line if the log is noisy
    // Debug.Log($"[WorldShifter] ShiftWorld {dx}, parallaxCount={parallax.Count}");

    for (int i = 0; i < parallax.Count; i++)
    {
        var p = parallax[i];
        if (!p) continue;
        p.Shift(dx); // keep your existing method; no behavior change here
    }
}



    public void RebuildRigidbodyCache()
    {
        _rb2dCache.Clear();
        _rb2dCache.AddRange(worldRoot.GetComponentsInChildren<Rigidbody2D>(includeInactive: false));
    }
}
