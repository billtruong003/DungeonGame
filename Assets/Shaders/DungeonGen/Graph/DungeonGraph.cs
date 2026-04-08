using System.Collections.Generic;
using DungeonSystem.Core;

namespace DungeonSystem.Graph
{
    public class GraphNode
    {
        public int Id { get; }
        public RoomType Type { get; set; }
        public int Depth { get; set; }
        public bool IsMainPath { get; set; }
        public bool IsDeadEnd { get; set; }
        public int BranchId { get; set; } = -1;

        public List<GraphEdge> Edges { get; } = new List<GraphEdge>();

        public int TemplateWidth { get; set; } = 1;
        public int TemplateHeight { get; set; } = 1;

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

    public class GraphEdge
    {
        public GraphNode A { get; }
        public GraphNode B { get; }
        public bool IsShortcut { get; set; }
        public bool IsSecret { get; set; }
        public bool IsLocked { get; set; }
        public string LockId { get; set; }

        public GraphEdge(GraphNode a, GraphNode b)
        {
            A = a;
            B = b;
        }

        public GraphNode GetOther(GraphNode node) => node == A ? B : A;
    }

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

        public List<GraphNode> GetMainPath()
        {
            var path = new List<GraphNode>();
            foreach (var n in Nodes)
                if (n.IsMainPath) path.Add(n);
            path.Sort((a, b) => a.Depth.CompareTo(b.Depth));
            return path;
        }

        public List<GraphNode> GetDeadEnds()
        {
            var result = new List<GraphNode>();
            foreach (var n in Nodes)
                if (n.IsDeadEnd) result.Add(n);
            return result;
        }
    }
}
