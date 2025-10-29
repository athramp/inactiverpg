using UnityEngine;
using UnityEngine.UI;

public class HpBarLerp : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] Slider slider;            // assign in Inspector (your HP Slider)
    [SerializeField] Image fillImage;          // assign your Fill (child Image under Fill Area)

    [Header("Smoothing")]
    [Tooltip("How fast the bar catches up to the target (units/sec).")]
    public float lerpSpeed = 8f;
    [Tooltip("Use unscaled time (UI unaffected by slow-mo/pauses).")]
    public bool useUnscaledTime = true;
    [Tooltip("Skip smoothing when large changes happen (e.g., revive).")]
    public float snapIfDeltaOver = 0.35f; // 35% jump snaps

    [Header("Colors")]
    [Tooltip("Color at 0% HP")]
    public Color lowColor = new Color(0.9f, 0.1f, 0.1f);   // red
    [Tooltip("Color at 50% HP")]
    public Color midColor = new Color(0.95f, 0.75f, 0.2f); // yellow
    [Tooltip("Color at 100% HP")]
    public Color highColor = new Color(0.15f, 0.85f, 0.3f); // green

    float targetValue;   // absolute value in slider space (0..maxValue)

    void Reset()
    {
        slider = GetComponent<Slider>();
        if (!fillImage && slider && slider.fillRect)
            fillImage = slider.fillRect.GetComponentInChildren<Image>();
    }

    void Awake()
    {
        if (!slider) slider = GetComponent<Slider>();
        // Ensure smooth interpolation is visible
        slider.wholeNumbers = false;
    }

    void Update()
    {
        if (!slider) return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float current = slider.value;
        float max = Mathf.Max(1f, slider.maxValue);

        // Snap on huge jumps (e.g., death → respawn)
        if (Mathf.Abs(targetValue - current) / max > snapIfDeltaOver)
            current = targetValue;

        // Smoothly approach target
        float next = Mathf.MoveTowards(current, targetValue, lerpSpeed * max * dt);
        slider.value = next;

        // Tint by % (0..1)
        float pct = Mathf.InverseLerp(0f, max, next);
        if (fillImage)
        {
            // Simple 3-stop gradient (low → mid → high)
            Color c = (pct < 0.5f)
                ? Color.Lerp(lowColor, midColor, pct / 0.5f)
                : Color.Lerp(midColor, highColor, (pct - 0.5f) / 0.5f);
            fillImage.color = c;
        }
    }

    /// <summary>
    /// Call this whenever HP changes. Values are absolute (not normalized).
    /// </summary>
    public void SetHp(float currentHp, float maxHp)
    {
        if (!slider) return;

        slider.minValue = 0f;
        slider.maxValue = Mathf.Max(1f, maxHp);
        targetValue = Mathf.Clamp(currentHp, 0f, slider.maxValue);

        // Optional: if dead, force red and fast snap
        // if (currentHp <= 0f) slider.value = targetValue;
    }
}
