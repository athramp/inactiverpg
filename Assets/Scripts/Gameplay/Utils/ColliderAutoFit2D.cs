// Assets/Scripts/Gameplay/Utils/ColliderAutoFit2D.cs
using UnityEngine;

[ExecuteAlways]
public class ColliderAutoFit2D : MonoBehaviour
{
    public SpriteRenderer target;          // drag Soldier's SpriteRenderer
    [Header("Which colliders live on THIS object?")]
    public CircleCollider2D circle;
    public BoxCollider2D box;

    [Header("Multipliers relative to sprite bounds")]
    public float circleRadiusMul = 0.6f;   // 0.6 * half of min(width,height)
    public Vector2 boxSizeMul = new Vector2(1f, 1f);

    void Reset()
    {
        target = GetComponentInParent<SpriteRenderer>();
        circle = GetComponent<CircleCollider2D>();
        box = GetComponent<BoxCollider2D>();
    }

    void OnEnable()  { Fit(); }
    void OnValidate(){ Fit(); }

    public void Fit()
    {
        if (!target) return;
        var b = target.bounds;                         // world-space bounds
        float minHalf = Mathf.Min(b.extents.x, b.extents.y);

        if (circle)
            circle.radius = minHalf * circleRadiusMul; // world units

        if (box)
            box.size = new Vector2(b.size.x * boxSizeMul.x,
                                   b.size.y * boxSizeMul.y);
    }
}
