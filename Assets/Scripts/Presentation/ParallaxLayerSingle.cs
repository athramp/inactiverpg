using UnityEngine;

[ExecuteAlways]
public class ParallaxLayerSingle : MonoBehaviour
{
    [Tooltip("0 = sky (sticks to camera). 1 = moves with world. Higher = faster than world.")]
    [Range(0f, 2f)]
    public float parallaxFactor = 0.5f;
    private float _scrolled;
    [Tooltip("Assign your ONE sprite (e.g., ground image). The script will clone it to loop.")]
    public SpriteRenderer sprite;

    [Tooltip("If true and only one sprite is assigned, a second tile will be auto-created at runtime.")]
    public bool autoDuplicateIfMissing = true;

    [Tooltip("Manual width override in world units (pixels / PPU). Leave 0 to auto-measure.")]
    public float overrideWidth = 0f;
    [Tooltip("Sorting order for the sprite renderers in this layer.")]
    public int sortingOrder = 0;
    
    // internal
    private SpriteRenderer _left;
    private SpriteRenderer _right;
    private float _segmentWidth;
    private Transform _cam;

    private void OnEnable()
    {
        _cam = Camera.main ? Camera.main.transform : null;
        SetupTiles();
        RecalcWidth();
        AlignRightOfLeft();
    }

    private void OnValidate()
    {
        SetupTiles();
        RecalcWidth();
        AlignRightOfLeft();
    }


    void Awake()
{
    var renderers = GetComponentsInChildren<SpriteRenderer>(true);
    foreach (var sr in renderers)
    {
        sr.sortingLayerName = "Background";
        sr.sortingOrder = sortingOrder; // make this a public int in your script
    }
}
    private void SetupTiles()
    {
        // If user already wired two children, honor them
        if (transform.childCount >= 2)
        {
            _left = transform.GetChild(0).GetComponent<SpriteRenderer>();
            _right = transform.GetChild(1).GetComponent<SpriteRenderer>();
        }
        else
        {
            // Build left from assigned sprite
            if (_left == null)
            {
                _left = EnsureChildRenderer("Left", sprite);
            }

            // Build or reuse right
            if (_right == null && autoDuplicateIfMissing)
            {
                _right = EnsureChildRenderer("Right", sprite);
            }
        }
    }

    private SpriteRenderer EnsureChildRenderer(string name, SpriteRenderer source)
    {
        var child = transform.Find(name);
        SpriteRenderer sr;
        if (child == null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            sr = go.AddComponent<SpriteRenderer>();
        }
        else
        {
            sr = child.GetComponent<SpriteRenderer>() ?? child.gameObject.AddComponent<SpriteRenderer>();
        }

        if (source != null)
        {
            sr.sprite = source.sprite;
            sr.flipX = source.flipX;
            sr.color = source.color;
            sr.sortingLayerID = source.sortingLayerID;
            sr.sortingOrder = source.sortingOrder;
            sr.drawMode = source.drawMode;
            sr.sharedMaterial = source.sharedMaterial;
        }
        return sr;
    }

    private void RecalcWidth()
    {
        if (_left == null) return;

        // choose widest child’s bounds as tile width
        float lw = _left.bounds.size.x;
        float rw = _right ? _right.bounds.size.x : lw;
        _segmentWidth = overrideWidth > 0f ? overrideWidth : Mathf.Max(lw, rw);

        if (_segmentWidth <= 0.0001f)
            _segmentWidth = 10f; // fallback to something sane
    }

    private void AlignRightOfLeft()
    {
        if (_left == null || _right == null) return;

        var lp = _left.transform.localPosition;
        _right.transform.localPosition = new Vector3(lp.x + _segmentWidth, lp.y, lp.z);
    }

    /// <summary>Call this from WorldShifter when the world slides horizontally.</summary>
    public void Shift(float worldDeltaX)
{
    float move = worldDeltaX * parallaxFactor;
    transform.position += new Vector3(move, 0f, 0f);

    if (_left == null || _right == null || _segmentWidth <= 0f) return;

    // make sure _left is really the leftmost
    EnsureOrder();

    // camera horizontal bounds in world units
    if (_cam == null && Camera.main) _cam = Camera.main.transform;
    float camX = _cam ? _cam.position.x : 0f;

    // assume orthographic camera
    float halfView = Camera.main ? Camera.main.orthographicSize * Camera.main.aspect : 5f * 0.5625f;
    float camLeft  = camX - halfView;
    float camRight = camX + halfView;

    // small buffer so we swap just as the tile leaves the view
    const float buffer = 0.01f;

    // if the right edge of the left tile is left of the camera left edge → move it to the right side
    float leftRightEdge = _left.transform.position.x + _segmentWidth * 0.5f;
    if (leftRightEdge < camLeft - buffer)
    {
        MoveLeftTileToRight();
    }

    // if the left edge of the right tile is right of the camera right edge → move it to the left side
    float rightLeftEdge = _right.transform.position.x - _segmentWidth * 0.5f;
    if (rightLeftEdge > camRight + buffer)
    {
        MoveRightTileToLeft();
    }
}

private void EnsureOrder()
{
    if (_left == null || _right == null) return;
    if (_left.transform.position.x > _right.transform.position.x)
        SwapTiles();
}

private void MoveLeftTileToRight()
{
    var rp = _right.transform.position;
    _left.transform.position = new Vector3(rp.x + _segmentWidth, rp.y, rp.z);
    SwapTiles();
}

private void MoveRightTileToLeft()
{
    var lp = _left.transform.position;
    _right.transform.position = new Vector3(lp.x - _segmentWidth, lp.y, lp.z);
    SwapTiles();
}

    private void SwapTiles()
    {
        var tmp = _left;
        _left = _right;
        _right = tmp;
    }
}
