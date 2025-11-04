using UnityEngine;

public class PlayerSpaceCoordinator : MonoBehaviour
{
    [Header("Refs")]
    public Transform playerVisual;     // visible player (not under worldRoot)
    public WorldShifter worldShifter;

    [Header("Screen window (computed at runtime if autoBounds=true)")]
    public bool autoBounds = true;
    public float leftBoundX = -6.5f;   // used if autoBounds = false
    public float rightBoundX = +3.0f;  // used if autoBounds = false
    public float screenMargin = 0.75f; // world units kept away from the screen edge

    [Header("Logical gameplay X (1D)")]
    public float logicalX = 0f;
    public float LogicalX => logicalX;

    Camera _cam;
    float _halfView;

    void Awake()
    {
        _cam = Camera.main;
        if (autoBounds) RecomputeBounds();
    }

    void OnValidate()
    {
        if (!Application.isPlaying && autoBounds)
        {
            _cam = Camera.main;
            RecomputeBounds();
        }
    }
public void SyncVisualToLogical()
{
    // Take the current logicalX and make sure the playerRoot reflects it visually
    if (playerVisual == null) return;

    Vector3 pos = playerVisual.localPosition;
    pos.x = logicalX; // or transform.position.x = logicalX if it’s in world space
    playerVisual.localPosition = pos;

    Debug.Log($"[PlayerSpaceCoordinator] Synced visual to logicalX={logicalX:F2}");
}
    public float GetLogicalX()
    {
        return logicalX;    // or whatever your internal variable name is
    }
    public void  SetLogicalX(float x) => logicalX = x;
    public void RecomputeBounds()
    {
        if (_cam == null) return;
        // compute screen edges in world units
        float halfHeight = _cam.orthographicSize;
        float halfWidth  = halfHeight * _cam.aspect;
        _halfView = halfWidth;
        // playerVisual moves in world space; use camera center as 0 for bounds
        // since backgrounds are siblings, this is fine
        float camX = _cam.transform.position.x;
        leftBoundX  = camX - halfWidth  + screenMargin;
        rightBoundX = camX + halfWidth  - screenMargin;
    }

    /// Call at encounter start to position the player near the left side if desired
    public void AnchorPlayerVisuallyToLeft()
{
    if (autoBounds) RecomputeBounds();
    logicalX = leftBoundX;  // no world shift, no playerVisual writes
}

    /// Main entry point for movement from inputs or skills (dx is desired displacement in world units)
    public void RequestPlayerDisplacement(float dx)
{
    if (!worldShifter) return;

    // 1) Update gameplay truth (engine will read this in CO)
    logicalX += dx;
    // If you keep a logical arena, clamp here:
    // logicalX = Mathf.Clamp(logicalX, arenaMinX, arenaMaxX);

    // 2) Compute where the sprite WOULD move on screen, using its current position
    //    NOTE: we will NOT write to playerVisual; CO places it from engine later.
    float currentVisX = playerVisual ? playerVisual.position.x : 0f;
    float desiredVisX = currentVisX + dx;

    // 3) Dead-zone: if desired is inside the window, do nothing (no world shift)
    if (desiredVisX >= leftBoundX && desiredVisX <= rightBoundX)
        return;

    // 4) Overflow: compute how much of dx would push us past the window
    float clampedVisX = Mathf.Clamp(desiredVisX, leftBoundX, rightBoundX);
    float actualLocal = clampedVisX - currentVisX;        // portion that stays in-window
    float overflow    = dx - actualLocal;                 // portion that exits window

    // 5) Shift the world by the overflow (small epsilon guard to avoid jitter)
    if (Mathf.Abs(overflow) > 1e-4f)
        worldShifter.ShiftWorld(-overflow);

    // IMPORTANT: do NOT touch playerVisual here.
    // CO will call visuals.SetPlayerX(engine.Player.PosX) later this frame.
}


    // (Optional) helper for continuous input movement
    public void MoveByInput(float axis, float speed)
    {
        if (Mathf.Abs(axis) <= 0f) return;
        RequestPlayerDisplacement(axis * speed * Time.deltaTime);
        
    }
}
