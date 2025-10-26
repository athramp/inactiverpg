using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct XpRow
{
    public int Level;
    public int XpToNext;
}

[CreateAssetMenu(fileName = "XpTable", menuName = "InactiveRPG/Data/XpTable")]
public class XpTable : ScriptableObject
{
    public List<XpRow> rows;

    public int GetXpToNext(int level)
    {
        var row = rows.Find(r => r.Level == level);
        return row.XpToNext;
    }
}
