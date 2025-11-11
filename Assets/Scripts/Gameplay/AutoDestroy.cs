using System.Linq;
using UnityEngine;

public static class AutoDestroy
{
    public static void Attach(GameObject go, float fallbackSeconds = 0f)
    {
        if (!go) return;
        var comp = go.AddComponent<AutoDestroyRunner>();
        comp.fallbackSeconds = fallbackSeconds;
    }

    private class AutoDestroyRunner : MonoBehaviour
    {
        public float fallbackSeconds = 1.3f;

        private ParticleSystem[] _ps;
        private bool _useFallbackTimer;
        private float _killAt = -1f;

        void Awake()
        {
            _ps = GetComponentsInChildren<ParticleSystem>(true);

            // If no PS or any PS loops → use fallback.
            if (_ps == null || _ps.Length == 0 || _ps.Any(p => p && p.main.loop))
                _useFallbackTimer = true;

            // If we have an Animator, prefer its current state's length.
            var anim = GetComponentInChildren<Animator>(true);
            if (anim && anim.runtimeAnimatorController)
            {
                // Try to get the current state's length. If it's 0, we still keep fallback.
                var info = anim.GetCurrentAnimatorStateInfo(0);
                if (info.length > 0.01f)
                {
                    _useFallbackTimer = true;
                    fallbackSeconds = Mathf.Max(fallbackSeconds, info.length);
                }
            }

            if (_useFallbackTimer)
            {
                _killAt = Time.time + fallbackSeconds;
            }
        }

        void Update()
        {
            // Fallback path
            if (_useFallbackTimer)
            {
                if (Time.time >= _killAt) Kill();
                return;
            }

            // Particle path: destroy when all non-looping PS are dead
            bool anyAlive = false;
            for (int i = 0; i < _ps.Length; i++)
            {
                var p = _ps[i];
                if (p && !p.main.loop && p.IsAlive(true)) { anyAlive = true; break; }
            }
            if (!anyAlive) Kill();
        }

        void Kill()
        {
            if (this) Destroy(gameObject);
        }
    }
}
