using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct ClassDef
{
    public string ClassId;
    public float BaseHP;
    public float BaseATK;
    public float BaseDEF;
    public float HpGrowth;
    public float AtkGrowth;
    public float DefGrowth;
}

[CreateAssetMenu(fileName = "ClassCatalog", menuName = "InactiveRPG/Data/ClassCatalog")]
public class ClassCatalog : ScriptableObject
{
    public List<ClassDef> classes;

    public ClassDef Get(string id) => classes.Find(c => c.ClassId == id);
}
