using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EnemyDebugOverlay : MonoBehaviour
{
    public CombatOrchestrator orchestrator;
    public KeyCode toggleKey = KeyCode.F1;
    public bool visible = true;

    Vector2 _scroll;

    void Awake()
    {
        if (!orchestrator) orchestrator = CombatOrchestrator.Instance;
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey)) visible = !visible;
        if (!orchestrator) orchestrator = CombatOrchestrator.Instance;
    }

    void OnGUI()
    {
        if (!visible || orchestrator == null) return;

        const int w = 1000;
        const int h = 500;
        var rect = new Rect(10, 1300, w, h);
        GUI.color = new Color(0, 0, 0, 0.7f);
        GUI.Box(rect, GUIContent.none);
        GUI.color = Color.white;

        GUILayout.BeginArea(rect, GUIContent.none);
        GUILayout.Label("<b>Enemies Debug</b>", GetStyle(14, FontStyle.Bold));

        // Use the dictionary values and convert to a list for Count/index
        List<EnemyUnit> enemies = orchestrator.EnemyViews != null
            ? orchestrator.EnemyViews.Values.ToList()
            : null;

        if (enemies == null || enemies.Count == 0)
        {
            GUILayout.Label("No enemies.", GetStyle(12));
            GUILayout.EndArea();
            return;
        }

        var target = orchestrator.CurrentTarget;

        _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
        GUI.enabled = false; // sliders read-only
        for (int i = 0; i < enemies.Count; i++)
        {
            var u = enemies[i];
            if (u == null) continue;

            bool isDead   = u.hp <= 0;
            bool isTarget = (u == target);

            var name = u.def ? u.def.monsterId : $"Enemy#{u.enemyId}";
            string header = $"{i}: {name}  X={u.posX:0.00}" + (isTarget ? "  ← TARGET" : "");
            GUILayout.Label(header, GetStyle(30, isTarget ? FontStyle.Bold : FontStyle.Normal,
                                             isTarget ? Color.yellow : (isDead ? Color.gray : Color.white)));

            float max = Mathf.Max(1, u.maxHp);
            float val = Mathf.Clamp(u.hp, 0, u.maxHp);
            GUILayout.HorizontalSlider(val, 0, max); // visual bar
            GUILayout.Label($"HP: {val}/{max}", GetStyle(30, FontStyle.Normal, isDead ? Color.gray : Color.white));

            GUILayout.Space(4);
        }
        GUI.enabled = true;
        GUILayout.EndScrollView();

        GUILayout.EndArea();
    }

    static GUIStyle GetStyle(int size, FontStyle fs = FontStyle.Normal, Color? col = null)
    {
        var st = new GUIStyle(GUI.skin.label);
        st.richText = true; st.fontSize = size; st.fontStyle = fs;
        if (col.HasValue) st.normal.textColor = col.Value;
        return st;
    }
}
