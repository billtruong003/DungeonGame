using UnityEngine;
using BillInspector;
using RPGModular;
using System.Diagnostics;

public class TestScript : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
#if UNITY_EDITOR
    [BillButton("TestHP")]
    public void DebugHP()
    {
        UnityEngine.Debug.Log(Game.Health.CurrentStamina);
        UnityEngine.Debug.Log(Game.Stats.GetStat(StatType.VIT));

    }
#endif
    // Update is called once per frame
    void Update()
    {

    }
}
