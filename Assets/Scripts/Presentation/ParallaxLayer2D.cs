using UnityEngine;

[ExecuteAlways]
public class ParallaxLayer2D : MonoBehaviour
{
    [Tooltip("0 = fixed to camera (sky). 1 = moves with world. <1 = slower parallax.")]
    [Range(0f, 2f)]
    public float parallaxFactor = 0.5f;

    [Tooltip("The two child sprites that tile seamlessly. Left then Right.")]
    public SpriteRenderer leftSprite;
    public SpriteRenderer rightSprite;

    [Tooltip("Manual width override (world units). Leave 0 to auto-measure from sprite bounds.")]
    public float overrideWidth = 0f;

    private float _segmentWidth;
    private Transform _cam;

    private void OnEnable()
    {
        _cam = Camera.main ? Camera.main.transform : null;
        RecalcWidth();
        AlignRightOfLeft();
    }

    private void OnValidate()
    {
        RecalcWidth();
        AlignRightOfLeft();
    }

    // measure sprite width in world units
    private void RecalcWidth()
    {
        float lw = leftSprite ? leftSprite.bounds.size.x : 0f;
        float rw = rightSprite ? rightSprite.bounds.size.x : 0f;
        _segmentWidth = overrideWidth > 0f ? overrideWidth : Mathf.Max(lw, rw);
    }

    // place right sprite next to left sprite
    private void AlignRightOfLeft()
    {
        if (leftSprite && rightSprite)
        {
            var lp = leftSprite.transform.localPosition;
            rightSprite.transform.localPosition = new Vector3(lp.x + _segmentWidth, lp.y, lp.z);
        }
    }

    /// <summary>
    /// Called by WorldShifter whenever the world moves horizontally.
    /// </summary>
    public void Shift(float worldDeltaX)
    {
        transform.position += new Vector3(worldDeltaX * parallaxFactor, 0f, 0f);
        TryLoop();
    }

    // keep the two tiles looping seamlessly
    private void TryLoop()
    {
        if (!leftSprite || !rightSprite) return;

        if (_cam == null && Camera.main) _cam = Camera.main.transform;
        float camX = _cam ? _cam.position.x : 0f;

        var leftRightEdge = leftSprite.transform.position.x + _segmentWidth * 0.5f;
        var rightLeftEdge = rightSprite.transform.position.x - _segmentWidth * 0.5f;

        // move left sprite to right side if it scrolled too far left
        if (leftRightEdge < camX - _segmentWidth)
        {
            leftSprite.transform.position += new Vector3(_segmentWidth * 2f, 0f, 0f);
            Swap(ref leftSprite, ref rightSprite);
        }
        // move right sprite to left side if scrolled too far right
        else if (rightLeftEdge > camX + _segmentWidth * 2f)
        {
            rightSprite.transform.position -= new Vector3(_segmentWidth * 2f, 0f, 0f);
            Swap(ref leftSprite, ref rightSprite);
        }
    }

    private void Swap(ref SpriteRenderer a, ref SpriteRenderer b)
    {
        var t = a;
        a = b;
        b = t;
    }
}
