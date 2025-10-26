using UnityEngine;
using UnityEngine.UI;

public class TabBar : MonoBehaviour
{
    [Header("Tabs")]
    public Button tabCamp, tabSkill, tabAdventure, tabTown;

    [Header("Panels")]
    public GameObject panelCamp, panelSkill, panelAdventure, panelTown;

    [Header("Battle Overlay")]
    public GameObject battleCanvas;
    public bool showOnCamp = true;
    public bool showOnSkill = false;
    public bool showOnAdventure = false;
    public bool showOnTown = false;

    void Awake()
    {
        // Ensure overlay is off at app start (before Start runs).
        if (battleCanvas) battleCanvas.SetActive(false);
    }

    void Start()
    {
        if (tabCamp)       tabCamp.onClick.AddListener(ShowCamp);
        if (tabSkill)      tabSkill.onClick.AddListener(ShowSkill);
        if (tabAdventure)  tabAdventure.onClick.AddListener(ShowAdventure);
        if (tabTown)       tabTown.onClick.AddListener(ShowTown);

        // Optional: pick your default tab here (usually NOT Camp before login).
        // ShowAdventure();
    }

    public void ShowCamp()      => Show(panelCamp);
    public void ShowSkill()     => Show(panelSkill);
    public void ShowAdventure() => Show(panelAdventure);
    public void ShowTown()      => Show(panelTown);

    void Show(GameObject g)
    {
        if (panelCamp)       panelCamp.SetActive(false);
        if (panelSkill)      panelSkill.SetActive(false);
        if (panelAdventure)  panelAdventure.SetActive(false);
        if (panelTown)       panelTown.SetActive(false);

        if (g) g.SetActive(true);

        if (!battleCanvas) return;

        bool want =
            (g == panelCamp      && showOnCamp) ||
            (g == panelSkill     && showOnSkill) ||
            (g == panelAdventure && showOnAdventure) ||
            (g == panelTown      && showOnTown);

        battleCanvas.SetActive(want);
    }
}
