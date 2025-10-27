using UnityEngine;

public class SimpleHpBar2D : MonoBehaviour
{
    [SerializeField] private SpriteRenderer bg;
    [SerializeField] private SpriteRenderer fill;

    [Header("Visual (world units)")]
    [SerializeField] private float width = 0.6f;   // total bar width
    [SerializeField] private float height = 0.08f; // bar height
    [SerializeField] private Color bgColor = new Color(0, 0, 0, 0.5f);
    [SerializeField] private Color fillColor = new Color(0.2f, 1f, 0.2f, 1f);
    [SerializeField] private bool keepConstantSize = true;

    private int maxHp = 1;
    private int curHp = 1;

    // cached
    private float baseWidth;
    private float baseHeight;

    void Awake()
    {
        if (!bg || !fill)
        {
            Debug.LogWarning("[HPBar] Assign BG and Fill SpriteRenderers.");
            enabled = false;
            return;
        }

        bg.color = bgColor;
        fill.color = fillColor;

        baseWidth = Mathf.Max(0.01f, width);
        baseHeight = Mathf.Max(0.01f, height);

        // Set BG at center with full width/height
        bg.transform.localPosition = Vector3.zero;
        bg.transform.localScale = new Vector3(baseWidth, baseHeight, 1f);

        // Set Fill so its LEFT EDGE aligns with BG’s left edge
        fill.transform.localScale = new Vector3(baseWidth, baseHeight, 1f);
        fill.transform.localPosition = new Vector3(-baseWidth * 0.5f, 0f, 0f); // anchor left
        Apply();
    }

    void LateUpdate()
    {
        if (!keepConstantSize) return;

        // Keep HP bar visually constant even if the enemy is scaled
        var parent = transform.parent ? transform.parent : transform;
        var lossy = parent.lossyScale;
        float inv = 1f / Mathf.Max(0.0001f, lossy.x);
        transform.localScale = new Vector3(inv, inv, 1f);
    }

    public void SetMax(int max)
    {
        maxHp = Mathf.Max(1, max);
        Apply();
    }

    public void SetCurrent(int cur)
    {
        curHp = Mathf.Clamp(cur, 0, maxHp);
        Apply();
    }

    private void Apply()
    {
        if (!fill) return;
        float pct = curHp / (float)maxHp;

        // Resize Fill by scaling X from its left edge
        fill.transform.localScale = new Vector3(baseWidth * Mathf.Clamp01(pct), baseHeight, 1f);
        // keep left edge anchored where it is (localPosition already set in Awake)
    }
}
