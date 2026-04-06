using System.Collections.Generic;
using DungeonSystem.Core;

namespace DungeonSystem.Graph
{
    /// <summary>
    /// Abstract node in the dungeon graph. Has no spatial position yet.
    /// </summary>
    public class GraphNode
    {
        public int Id { get; }
        public RoomType Type { get; set; }
        public int Depth { get; set; }              // Distance from start along main path
        public bool IsMainPath { get; set; }
        public bool IsDeadEnd { get; set; }
        public int BranchId { get; set; } = -1;     // Which branch this belongs to (-1 = main)

        // Connections
        public List<GraphEdge> Edges { get; } = new List<GraphEdge>();

        // Assigned after layout phase
        public int TemplateWidth { get; set; } = 1;
        public int TemplateHeight { get; set; } = 1;

        /// <summary>
        /// The specific template chosen during AssignTemplateSizes.
        /// RoomInstantiator MUST use this instead of picking a new random one.
        /// </summary>
        public Data.RoomTemplate AssignedTemplate { get; set; }

        public GraphNode(int id, RoomType type)
        {
            Id = id;
            Type = type;
        }

        public List<GraphNode> GetNeighbors()
        {
            var neighbors = new List<GraphNode>();
            foreach (var edge in Edges)
                neighbors.Add(edge.GetOther(this));
            return neighbors;
        }

        public bool IsConnectedTo(GraphNode other)
        {
            foreach (var edge in Edges)
                if (edge.GetOther(this) == other) return true;
            return false;
        }
    }

    /// <summary>
    /// Connection between two graph nodes. May become a corridor or direct adjacency.
    /// </summary>
    public class GraphEdge
    {
        public GraphNode A { get; }
        public GraphNode B { get; }
        public bool IsShortcut { get; set; }        // Cycle/loop edge
        public bool IsSecret { get; set; }          // Hidden connection
        public bool IsLocked { get; set; }          // Requires key
        public string LockId { get; set; }          // Key identifier

        public GraphEdge(GraphNode a, GraphNode b)
        {
            A = a;
            B = b;
        }

        public GraphNode GetOther(GraphNode node) => node == A ? B : A;
    }

    /// <summary>
    /// Complete abstract dungeon graph for one floor.
    /// </summary>
    public class DungeonGraph
    {
        public List<GraphNode> Nodes { get; } = new List<GraphNode>();
        public List<GraphEdge> Edges { get; } = new List<GraphEdge>();

        public GraphNode StartNode { get; set; }
        public GraphNode BossNode { get; set; }
        public GraphNode StairUpNode { get; set; }
        public GraphNode StairDownNode { get; set; }

        private int _nextId = 0;

        public GraphNode CreateNode(RoomType type)
        {
            var node = new GraphNode(_nextId++, type);
            Nodes.Add(node);
            return node;
        }

        public GraphEdge Connect(GraphNode a, GraphNode b, bool isShortcut = false)
        {
            if (a.IsConnectedTo(b)) return null;

            var edge = new GraphEdge(a, b) { IsShortcut = isShortcut };
            Edges.Add(edge);
            a.Edges.Add(edge);
            b.Edges.Add(edge);
            return edge;
        }

        /// <summary>
        /// Get all nodes on the main path in order.
        /// </summary>
        public List<GraphNode> GetMainPath()
        {
            var path = new List<GraphNode>();
            foreach (var n in Nodes)
                if (n.IsMainPath) path.Add(n);
            path.Sort((a, b) => a.Depth.CompareTo(b.Depth));
            return path;
        }

        /// <summary>
        /// Get all dead-end nodes.
        /// </summary>
        public List<GraphNode> GetDeadEnds()
        {
            var result = new List<GraphNode>();
            foreach (var n in Nodes)
                if (n.IsDeadEnd) result.Add(n);
            return result;
        }
    }
}