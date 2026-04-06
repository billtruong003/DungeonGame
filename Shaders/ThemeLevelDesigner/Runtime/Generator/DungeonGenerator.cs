using System.Collections.Generic;
using UnityEngine;

namespace ThemeLevelDesigner
{
    /// <summary>
    /// Generates a dungeon layout from a DungeonConfigSO.
    /// Graph-first approach: build topology, then place rooms spatially.
    /// </summary>
    public static class DungeonGenerator
    {
        public static GeneratedDungeon Generate(DungeonConfigSO config)
        {
            var rng = config.seed == 0
                ? new System.Random()
                : new System.Random(config.seed);

            int roomCount = rng.Next(config.minRooms, config.maxRooms + 1);

            // Step 1: Build graph
            var graph = BuildGraph(roomCount, config, rng);

            // Step 2: Assign rooms from pool
            AssignRooms(graph, config, rng);

            // Step 3: Spatial placement
            var placed = PlaceRooms(graph, config, rng);

            // Step 4: Difficulty assignment
            AssignDifficulty(placed, config);

            return new GeneratedDungeon
            {
                config = config,
                rooms = placed,
                seed = config.seed == 0 ? rng.GetHashCode() : config.seed
            };
        }

        static List<GraphNode> BuildGraph(int count, DungeonConfigSO config, System.Random rng)
        {
            var nodes = new List<GraphNode>();

            // Critical path
            int pathLen = Mathf.Max(config.criticalPathMinLength, 3);
            pathLen = Mathf.Min(pathLen, count);

            for (int i = 0; i < pathLen; i++)
            {
                var node = new GraphNode { index = i, isCriticalPath = true };
                if (i == 0 && config.requireStartRoom)
                    node.requiredType = RoomType.Start;
                else if (i == pathLen - 1 && config.requireBossRoom)
                    node.requiredType = RoomType.Boss;

                if (i > 0)
                {
                    node.connections.Add(i - 1);
                    nodes[i - 1].connections.Add(i);
                }
                nodes.Add(node);
            }

            // Branch rooms
            for (int i = pathLen; i < count; i++)
            {
                var node = new GraphNode { index = i };
                int parent = rng.Next(0, nodes.Count);
                node.connections.Add(parent);
                nodes[parent].connections.Add(i);
                nodes.Add(node);
            }

            return nodes;
        }

        static void AssignRooms(List<GraphNode> graph, DungeonConfigSO config, System.Random rng)
        {
            foreach (var node in graph)
            {
                RoomSO best = null;
                var candidates = new List<RoomSO>();

                foreach (var room in config.roomPool)
                {
                    if (node.requiredType.HasValue && room.roomType == node.requiredType.Value)
                    {
                        candidates.Add(room);
                    }
                    else if (!node.requiredType.HasValue && room.roomType != RoomType.Start && room.roomType != RoomType.Boss)
                    {
                        candidates.Add(room);
                    }
                }

                if (candidates.Count > 0)
                    best = candidates[rng.Next(candidates.Count)];
                else if (config.roomPool.Count > 0)
                    best = config.roomPool[rng.Next(config.roomPool.Count)];

                node.assignedRoom = best;
            }
        }

        static List<PlacedRoom> PlaceRooms(List<GraphNode> graph, DungeonConfigSO config, System.Random rng)
        {
            var placed = new List<PlacedRoom>();
            var occupied = new HashSet<Vector2Int>();

            if (graph.Count == 0) return placed;

            // Place first room at origin
            var first = graph[0];
            var firstBounds = first.assignedRoom != null ? first.assignedRoom.GetBounds() : new Vector2Int(3, 3);
            var firstPlaced = new PlacedRoom
            {
                node = first,
                room = first.assignedRoom,
                worldGridPos = Vector2Int.zero,
                bounds = firstBounds
            };
            placed.Add(firstPlaced);
            MarkOccupied(occupied, firstPlaced);

            // BFS placement
            var queue = new Queue<int>();
            var visited = new HashSet<int> { 0 };
            queue.Enqueue(0);

            var directions = new Vector2Int[]
            {
                Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
            };

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                var currentPlaced = placed.Find(p => p.node.index == current);

                foreach (int neighbor in graph[current].connections)
                {
                    if (visited.Contains(neighbor)) continue;
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);

                    var neighborNode = graph[neighbor];
                    var bounds = neighborNode.assignedRoom != null
                        ? neighborNode.assignedRoom.GetBounds()
                        : new Vector2Int(3, 3);

                    // Try directions with gap
                    bool didPlace = false;
                    var shuffled = Shuffle(directions, rng);
                    foreach (var dir in shuffled)
                    {
                        var pos = currentPlaced.worldGridPos + dir * (Mathf.Max(currentPlaced.bounds.x, currentPlaced.bounds.y) + 2);
                        if (IsAreaFree(occupied, pos, bounds))
                        {
                            var p = new PlacedRoom
                            {
                                node = neighborNode,
                                room = neighborNode.assignedRoom,
                                worldGridPos = pos,
                                bounds = bounds
                            };
                            placed.Add(p);
                            MarkOccupied(occupied, p);
                            didPlace = true;
                            break;
                        }
                    }

                    if (!didPlace)
                    {
                        // Fallback: spiral outward
                        for (int radius = 3; radius < 30; radius++)
                        {
                            for (int dx = -radius; dx <= radius; dx++)
                            {
                                for (int dy = -radius; dy <= radius; dy++)
                                {
                                    if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius) continue;
                                    var pos = currentPlaced.worldGridPos + new Vector2Int(dx, dy);
                                    if (IsAreaFree(occupied, pos, bounds))
                                    {
                                        var p = new PlacedRoom
                                        {
                                            node = neighborNode,
                                            room = neighborNode.assignedRoom,
                                            worldGridPos = pos,
                                            bounds = bounds
                                        };
                                        placed.Add(p);
                                        MarkOccupied(occupied, p);
                                        didPlace = true;
                                        break;
                                    }
                                }
                                if (didPlace) break;
                            }
                            if (didPlace) break;
                        }
                    }
                }
            }

            return placed;
        }

        static void AssignDifficulty(List<PlacedRoom> rooms, DungeonConfigSO config)
        {
            // Find start room
            PlacedRoom start = rooms.Find(r => r.node.requiredType == RoomType.Start);
            if (start == null && rooms.Count > 0) start = rooms[0];

            foreach (var room in rooms)
            {
                float t = rooms.Count > 1
                    ? (float)rooms.IndexOf(room) / (rooms.Count - 1)
                    : 0f;
                room.difficulty = config.difficultyCurve.Evaluate(t);
            }
        }

        static void MarkOccupied(HashSet<Vector2Int> occupied, PlacedRoom room)
        {
            for (int x = 0; x < room.bounds.x; x++)
                for (int y = 0; y < room.bounds.y; y++)
                    occupied.Add(room.worldGridPos + new Vector2Int(x, y));
        }

        static bool IsAreaFree(HashSet<Vector2Int> occupied, Vector2Int pos, Vector2Int size)
        {
            // Check with 1-cell border
            for (int x = -1; x <= size.x; x++)
                for (int y = -1; y <= size.y; y++)
                    if (occupied.Contains(pos + new Vector2Int(x, y)))
                        return false;
            return true;
        }

        static T[] Shuffle<T>(T[] array, System.Random rng)
        {
            var copy = (T[])array.Clone();
            for (int i = copy.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (copy[i], copy[j]) = (copy[j], copy[i]);
            }
            return copy;
        }
    }

    public class GraphNode
    {
        public int index;
        public bool isCriticalPath;
        public RoomType? requiredType;
        public List<int> connections = new();
        public RoomSO assignedRoom;
    }

    [System.Serializable]
    public class PlacedRoom
    {
        public GraphNode node;
        public RoomSO room;
        public Vector2Int worldGridPos;
        public Vector2Int bounds;
        public float difficulty;
    }

    [System.Serializable]
    public class GeneratedDungeon
    {
        public DungeonConfigSO config;
        public List<PlacedRoom> rooms;
        public int seed;
    }
}
