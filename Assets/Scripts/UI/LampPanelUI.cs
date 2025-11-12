using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Gameplay.Loot;

public class LampPanelUI : MonoBehaviour
{
    [SerializeField] LampProgressionService lamp;
    [SerializeField] CurrencyService currency;
    [SerializeField] LampLootController lootController;
    [SerializeField] TMP_Text lampLevelText, chargesText, timerText;
    [SerializeField] Button rollButton, upgradeButton;

    void Awake()
    {
        if (!lamp) lamp = FindObjectOfType<LampProgressionService>();
        if (!currency) currency = FindObjectOfType<CurrencyService>();
        if (!lootController) lootController = FindObjectOfType<LampLootController>();

        rollButton.onClick.AddListener(TryRoll);
        upgradeButton.onClick.AddListener(TryUpgrade);
    }

    void OnEnable()
    {
        Refresh();
        lamp.OnLampChanged += Refresh;
        currency.OnCurrencyChanged += HandleCurrency;
    }
    void OnDisable()
    {
        lamp.OnLampChanged -= Refresh;
        currency.OnCurrencyChanged -= HandleCurrency;
    }

    void HandleCurrency(CurrencyType type, long amount)
    {
        if (type == CurrencyType.LampCharge || type == CurrencyType.Gold) Refresh();
    }

    void Refresh()
    {
        lampLevelText.text = $"Lamp Lv {lamp.LampLevel}";
        chargesText.text = $"Charges: {currency.Get(CurrencyType.LampCharge)}";
        timerText.text = lamp.UpgradeInProgress ? $"{lamp.UpgradeTimer:F0}s" : "Ready";
        rollButton.interactable = currency.Get(CurrencyType.LampCharge) > 0;
        upgradeButton.interactable = !lamp.UpgradeInProgress && lamp.NextDef != null && currency.Get(CurrencyType.Gold) >= lamp.NextDef.upgradeGoldCost;
    }

    void TryRoll()
    {
        if (lootController.RollAndStore()) Refresh();
    }

    void TryUpgrade()
    {
        if (lamp.TryStartUpgrade()) Refresh();
    }
}
