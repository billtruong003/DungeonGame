using UnityEngine;

namespace DungeonSystem.Runtime
{
    public enum SpawnPointType
    {
        Enemy,
        Item,
        Chest,
        Trap,
        NPC,
        BossSpawn,
        PlayerSpawn,
        Light,
        Prop,
        PuzzleObject
    }

    public class SpawnPoint : MonoBehaviour
    {
        public SpawnPointType pointType;
        [Range(0, 10)] public int priority;
        public GameObject assignedPrefab;
    }
}
