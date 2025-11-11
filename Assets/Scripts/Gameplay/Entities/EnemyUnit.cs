using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class EnemyUnit {
    public float chaseDelayTimer;
    public Transform view;     // mover Transform returned by BVC.SpawnEnemyView
    public MonsterDef def;
    public float posX;
    public int hp, maxHp, atk, defStat, shield;
    public float stunTimer;
    public Slider hpBar;       // captured from child of the MODEL
    public int enemyId;   // engine-side id
    public float lastX;
    public float animSpeed;
    public Animator animator;
    public bool deathStarted;
}