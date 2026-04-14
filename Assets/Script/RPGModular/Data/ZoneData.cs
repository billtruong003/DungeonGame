using System;
using UnityEngine;
using BillInspector;

namespace RPGModular
{
    [CreateAssetMenu(menuName = "Game/Zone Data")]
    [BillTitle("Zone", "Map area definition")]
    public class ZoneData : ScriptableObject
    {
        [BillBoxGroup("Identity")]
        public string zoneID;
        [BillBoxGroup("Identity"), BillLabelText("Name Key (Loc)")]
        public string nameKey;
        [BillBoxGroup("Identity"), BillEnumToggleButtons]
        public ZoneType type;

        [BillBoxGroup("Scene")]
        public string sceneName;
        public string bgmKey;

        [BillBoxGroup("Level")]
        [BillSlider(1, 100)] public int recommendedLevel;
        [BillSlider(0, 100)] public int minLevel;

        [BillBoxGroup("Spawn Points")]
        [BillTableList]
        public SpawnPoint[] spawnPoints;

        [BillBoxGroup("Connections")]
        [BillTableList]
        public ZoneConnection[] connections;
    }

    [Serializable]
    public class SpawnPoint
    {
        public string spawnID;
        public Vector3 position;
        public float yRotation;
    }

    [Serializable]
    public class ZoneConnection
    {
        public ZoneData targetZone;
        public string targetSpawnID;
        public int requiredLevel;
        public QuestData requiredQuest;
    }
}
