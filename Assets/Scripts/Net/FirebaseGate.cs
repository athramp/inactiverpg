// FirebaseGate.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using Firebase;
using Firebase.Extensions;
using UnityEngine;
using TMPro;

public class FirebaseGate : MonoBehaviour
{
    // -------- Static API --------
    public static FirebaseGate Instance { get; private set; }
    public static bool IsReady { get; private set; }
    public static Exception InitException { get; private set; }
    public static event Action OnReady;

    /// Wait until ready OR throw if init failed.
    public static async Task WaitUntilReady()
    {
        if (IsReady) return;

        // If no gate exists (e.g., scene forgot to include it), create one now.
        EnsureBootstrapGate();

        // Wait until we’re ready or failed.
        while (!IsReady && InitException == null)
            await Task.Yield();

        if (InitException != null) throw InitException;
    }

    // Creates a gate at runtime if user forgot to place it in scene.
    static void EnsureBootstrapGate()
    {
        if (Instance != null) return;
        var go = new GameObject("~FirebaseGate~(Auto)");
        Instance = go.AddComponent<FirebaseGate>();
        DontDestroyOnLoad(go);
        Instance.autoBootstrap = true;
    }

    // -------- Config / Inspector --------
    [Header("Gate")]
    [Tooltip("Optional: If gate is auto-created via code, this will be true.")]
    [SerializeField] private bool autoBootstrap = false;

    [Tooltip("Extra logs in builds")]
    [SerializeField] private bool verboseLogs = true;

    [Tooltip("Give up after this many seconds (only affects HUD text).")]
    [SerializeField] private float initTimeoutSeconds = 20f;

    [Header("HUD (optional)")]
    [Tooltip("Attach a TextMeshProUGUI to show Firebase status. If null, a tiny label will be created in play mode.")]
    [SerializeField] private TextMeshProUGUI statusLabel;

    private CancellationTokenSource _cts;
    private bool _started;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        _cts?.Cancel();
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (_started) return;
        _started = true;
        _cts = new CancellationTokenSource();
        _ = InitializeAsync(_cts.Token);
    }

#if !UNITY_EDITOR
    // In case the scene doesn’t have the gate at all, force-create before first scene loads.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void PreSceneBootstrap()
    {
        EnsureBootstrapGate();
    }
#endif

    async Task InitializeAsync(CancellationToken ct)
    {
        try
        {
            SetHud("Firebase: initializing…");
            if (verboseLogs) Debug.Log("[FirebaseGate] Checking dependencies…");

            var status = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (ct.IsCancellationRequested) return;

            if (status != DependencyStatus.Available)
            {
                var msg = $"[FirebaseGate] Dependencies not available: {status}";
                Debug.LogError(msg);
                InitException = new Exception(msg);
                SetHud($"Firebase: {status}");
                return;
            }

            // Make sure DefaultInstance is realized on main thread
            await Task.Yield();
            if (FirebaseApp.DefaultInstance == null)
            {
                var msg = "[FirebaseGate] FirebaseApp.DefaultInstance is null after init";
                Debug.LogError(msg);
                InitException = new Exception(msg);
                SetHud("Firebase: DefaultInstance null");
                return;
            }

            // Touch commonly used singletons early so they’re warm (no-op if not used)
            try { var _ = Firebase.Auth.FirebaseAuth.DefaultInstance; } catch { /* ignore */ }
            try { var __ = Firebase.Firestore.FirebaseFirestore.DefaultInstance; } catch { /* ignore */ }
            try { var ___ = Firebase.Functions.FirebaseFunctions.DefaultInstance; } catch { /* ignore */ }

            IsReady = true;
            if (verboseLogs) Debug.Log("[FirebaseGate] READY.");
            SetHud("Firebase: READY");
            OnReady?.Invoke();
        }
        catch (Exception ex)
        {
            InitException = ex;
            Debug.LogException(ex);
            SetHud($"Firebase: ERROR\n{ex.GetType().Name}");
        }

        // Simple timeout notifier for visibility (doesn’t cancel init; only adjusts HUD).
        float t = 0f;
        while (!IsReady && InitException == null && t < initTimeoutSeconds)
        {
            if (ct.IsCancellationRequested) return;
            await Task.Yield();
            t += Time.unscaledDeltaTime;
        }
        if (!IsReady && InitException == null)
        {
            SetHud("Firebase: TIMEOUT (see logs)");
            if (verboseLogs) Debug.LogWarning("[FirebaseGate] Timeout waiting for dependencies.");
        }
    }

    // -------- Minimal HUD --------
    void SetHud(string text)
    {
        // if (!Application.isPlaying) return;

        // if (statusLabel == null)
        // {
        //     // Create a tiny overlay label if none provided.
        //     var hudGo = new GameObject("FirebaseHUD");
        //     var canvas = new GameObject("FirebaseHUDCanvas");
        //     var c = canvas.AddComponent<Canvas>();
        //     c.renderMode = RenderMode.ScreenSpaceOverlay;
        //     DontDestroyOnLoad(canvas);
        //     hudGo.transform.SetParent(canvas.transform);

        //     var rect = hudGo.AddComponent<RectTransform>();
        //     rect.anchorMin = new Vector2(0, 1);
        //     rect.anchorMax = new Vector2(0, 1);
        //     rect.pivot = new Vector2(0, 1);
        //     rect.anchoredPosition = new Vector2(8, -8);
        //     rect.sizeDelta = new Vector2(420, 64);

        //     statusLabel = hudGo.AddComponent<TextMeshProUGUI>();
        //     statusLabel.fontSize = 18;
        //     statusLabel.alignment = TextAlignmentOptions.Left;
        //     statusLabel.enableWordWrapping = false;
        //     statusLabel.color = new Color(1, 1, 1, 0.92f);
        //     var bg = hudGo.AddComponent<FirebaseHudBG>();
        //     bg.SetColor(new Color(0, 0, 0, 0.5f));
        // }

        // statusLabel.text = text;
    }

    // Tiny background for the HUD label
    private class FirebaseHudBG : MonoBehaviour
    {
        Color col = new Color(0, 0, 0, 0.5f);
        public void SetColor(Color c) => col = c;
        void OnGUI()
        {
            var pos = GetComponent<RectTransform>().transform as RectTransform;
            if (pos == null) return;
            var rect = new Rect(8, 8, 420, 64);
#if UNITY_EDITOR
            // GUI coords differ in editor playmode overlays; just draw top-left block.
#endif
            var old = GUI.color;
            GUI.color = col;
            GUI.Box(rect, GUIContent.none);
            GUI.color = old;
        }
    }
}
