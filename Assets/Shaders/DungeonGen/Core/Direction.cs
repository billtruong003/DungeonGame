namespace DungeonSystem.Core
{
    public enum Direction
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3,
        None = -1
    }

    public enum RoomType
    {
        Start,
        Combat,
        Loot,
        Puzzle,
        Boss,
        MiniBoss,
        StaircaseUp,
        StaircaseDown,
        Corridor,
        Junction,      // T or + shaped connector
        SecretRoom,
        SafeRoom,      // Save point / rest area
        Shop,
        Trap
    }

    public enum DoorState
    {
        Open,
        Walled,
        Locked,
        Hidden    // Secret door
    }

    public enum GenerationStrategy
    {
        BranchingTree,    // Main path + side branches (Zelda-like)
        Cyclic,           // Loops and shortcuts (Dark Souls-like)
        Linear,           // Mostly straight path
        Arena             // Open layout, many connections
    }
}
