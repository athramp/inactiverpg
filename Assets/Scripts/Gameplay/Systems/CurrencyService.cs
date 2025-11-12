using System;
using System.Collections.Generic;
using UnityEngine;

public enum CurrencyType { Gold, LampCharge, Gem }

[Serializable]
public class CurrencyChangedEvent : UnityEngine.Events.UnityEvent<CurrencyType, long> { }

public class CurrencyService : MonoBehaviour
{
    [Serializable]
    private class Balance
    {
        public CurrencyType type;
        public long amount;
    }

    [SerializeField] private List<Balance> balances = new()
    {
        new Balance{ type = CurrencyType.Gold, amount = 0 },
        new Balance{ type = CurrencyType.LampCharge, amount = 0 },
        new Balance{ type = CurrencyType.Gem, amount = 0 },
    };

    [SerializeField] private CurrencyChangedEvent onCurrencyChanged;
    public event Action<CurrencyType, long> OnCurrencyChanged;

    private void Awake()
    {
        OnCurrencyChanged += (type, amt) => onCurrencyChanged?.Invoke(type, amt);
    }

    public long Get(CurrencyType type)
    {
        return GetBalance(type).amount;
    }

    public void Add(CurrencyType type, long delta)
    {
        var balance = GetBalance(type);
        balance.amount = System.Math.Max(0L, balance.amount + delta);
        OnCurrencyChanged?.Invoke(type, balance.amount);
    }

    public bool TrySpend(CurrencyType type, long cost)
    {
        var balance = GetBalance(type);
        if (balance.amount < cost) return false;
        balance.amount -= cost;
        OnCurrencyChanged?.Invoke(type, balance.amount);
        return true;
    }

    private Balance GetBalance(CurrencyType type)
    {
        var bal = balances.Find(b => b.type == type);
        if (bal == null)
        {
            bal = new Balance { type = type, amount = 0 };
            balances.Add(bal);
        }
        return bal;
    }
}
