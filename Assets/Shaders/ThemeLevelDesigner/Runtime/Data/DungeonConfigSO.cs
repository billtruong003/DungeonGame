using System.Collections.Generic;
using UnityEngine;

namespace ThemeLevelDesigner
{
    [CreateAssetMenu(fileName = "NewDungeonConfig", menuName = "Level Design/Dungeon Config", order = 3)]
    public class DungeonConfigSO : ScriptableObject
    {
        [Header("Room Pool")]
        public List<RoomSO> roomPool = new();

        [Header("Generation Rules")]
        [Range(3, 50)] public int minRooms = 5;
        [Range(3, 50)] public int maxRooms = 12;
        [Range(1, 10)] public int criticalPathMinLength = 3;

        [Header("Required Rooms")]
        public bool requireStartRoom = true;
        public bool requireBossRoom = true;

        [Header("Difficulty")]
        public AnimationCurve difficultyCurve = AnimationCurve.Linear(0, 1, 1, 10);

        [Header("Seed")]
        [Tooltip("0 = random seed each time")]
        public int seed = 0;

        [Header("Spacing")]
        public float cellSize = 1f;
        public int corridorWidth = 1;
    }
}
