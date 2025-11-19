using UnityEngine;
using TMPro;

public class CurrencyHUD : MonoBehaviour
{
    [SerializeField] private CurrencyService currencyService;
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private TMP_Text lampText;
    [SerializeField] private TMP_Text gemText;

    void Awake()
    {
        if (!currencyService) currencyService = FindObjectOfType<CurrencyService>();
        RefreshAll();
        if (currencyService != null)
            currencyService.OnCurrencyChanged += HandleCurrencyChanged;
    }

    void OnDestroy()
    {
        if (currencyService != null)
            currencyService.OnCurrencyChanged -= HandleCurrencyChanged;
    }

    void HandleCurrencyChanged(CurrencyType type, long amount)
    {
        Refresh(type);
    }

    void RefreshAll()
    {
        Refresh(CurrencyType.Gold);
        Refresh(CurrencyType.LampCharge);
        Refresh(CurrencyType.Gem);
    }

    void Refresh(CurrencyType type)
    {
        if (currencyService == null) return;
        long value = currencyService.Get(type);
        switch (type)
        {
            case CurrencyType.Gold:
                if (goldText) goldText.text = value.ToString("N0");
                break;
            case CurrencyType.LampCharge:
                if (lampText) lampText.text = value.ToString("N0");
                break;
            case CurrencyType.Gem:
                if (gemText) gemText.text = value.ToString("N0");
                break;
        }
    }
}
