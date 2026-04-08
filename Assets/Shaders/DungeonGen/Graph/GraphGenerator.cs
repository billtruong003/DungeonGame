using System;
using System.Collections.Generic;
using System.Linq;
using DungeonSystem.Core;
using DungeonSystem.Data;
using UnityEngine;

namespace DungeonSystem.Graph
{
    public class GraphGenerator
    {
        private readonly DungeonConfig _config;
        private readonly System.Random _rng;

        public GraphGenerator(DungeonConfig config, System.Random rng)
        {
            _config = config;
            _rng = rng;
        }

        public DungeonGraph Generate(int floorIndex, bool isFirstFloor, bool isLastFloor)
        {
            int totalRooms = _rng.Next(_config.minRoomsPerFloor, _config.maxRoomsPerFloor + 1);
            int mainPathLength = Mathf.Max(3, Mathf.RoundToInt(totalRooms * _config.mainPathRatio));
            int branchRooms = totalRooms - mainPathLength;

            var graph = new DungeonGraph();

            var mainPath = CreateMainPath(graph, mainPathLength, floorIndex, isFirstFloor, isLastFloor);
            CreateBranches(graph, mainPath, branchRooms, floorIndex);
            MarkDeadEnds(graph);

            if (_config.strategy == GenerationStrategy.Cyclic || _config.cycleProbability > 0f)
                AddCycles(graph);

            PlaceSpecialRooms(graph, floorIndex);

            return graph;
        }

        private List<GraphNode> CreateMainPath(DungeonGraph graph, int length, int floorIndex, bool isFirst, bool isLast)
        {
            var path = new List<GraphNode>();

            RoomType startType = isFirst ? RoomType.Start : RoomType.StaircaseDown;
            var startNode = graph.CreateNode(startType);
            startNode.IsMainPath = true;
            startNode.Depth = 0;
            graph.StartNode = startNode;
            if (!isFirst) graph.StairDownNode = startNode;
            path.Add(startNode);

            var distribution = _config.GetDistribution(floorIndex);
            for (int i = 1; i < length - 1; i++)
            {
                RoomType type = distribution.Sample(_rng);
                var node = graph.CreateNode(type);
                node.IsMainPath = true;
                node.Depth = i;
                graph.Connect(path[^1], node);
                path.Add(node);
            }

            if (_config.placeMiniBoss && length > 4)
            {
                int midIndex = length / 2;
                if (midIndex > 0 && midIndex < path.Count)
                    path[midIndex].Type = RoomType.MiniBoss;
            }

            RoomType endType = isLast ? RoomType.Boss : RoomType.StaircaseUp;
            var endNode = graph.CreateNode(endType);
            endNode.IsMainPath = true;
            endNode.Depth = length - 1;
            graph.Connect(path[^1], endNode);
            path.Add(endNode);

            if (isLast)
                graph.BossNode = endNode;
            else
                graph.StairUpNode = endNode;

            return path;
        }

        private void CreateBranches(DungeonGraph graph, List<GraphNode> mainPath, int branchBudget, int floorIndex)
        {
            if (branchBudget <= 0 || mainPath.Count < 3) return;

            var distribution = _config.GetDistribution(floorIndex);
            int branchId = 0;
            int roomsPlaced = 0;

            var branchPoints = new List<GraphNode>();
            for (int i = 1; i < mainPath.Count - 1; i++)
                branchPoints.Add(mainPath[i]);

            Shuffle(branchPoints);

            foreach (var branchRoot in branchPoints)
            {
                if (roomsPlaced >= branchBudget) break;
                if (branchRoot.Edges.Count >= 3) continue;

                int branchLength = _rng.Next(1, _config.maxBranchDepth + 1);
                branchLength = Mathf.Min(branchLength, branchBudget - roomsPlaced);

                var prev = branchRoot;
                for (int j = 0; j < branchLength; j++)
                {
                    RoomType type = distribution.Sample(_rng);
                    var node = graph.CreateNode(type);
                    node.IsMainPath = false;
                    node.BranchId = branchId;
                    node.Depth = branchRoot.Depth + j + 1;
                    graph.Connect(prev, node);
                    prev = node;
                    roomsPlaced++;
                }
                branchId++;
            }
        }

        private void AddCycles(DungeonGraph graph)
        {
            var deadEnds = graph.Nodes.Where(n => n.Edges.Count == 1 && !n.IsMainPath).ToList();

            foreach (var de in deadEnds)
            {
                if (_rng.NextDouble() > _config.cycleProbability) continue;

                GraphNode best = null;
                int bestDist = int.MaxValue;

                foreach (var candidate in graph.Nodes)
                {
                    if (candidate == de) continue;
                    if (de.IsConnectedTo(candidate)) continue;
                    if (candidate.Edges.Count >= 4) continue;

                    int dist = Mathf.Abs(de.Depth - candidate.Depth);
                    if (dist > 0 && dist < bestDist && dist <= 3)
                    {
                        bestDist = dist;
                        best = candidate;
                    }
                }

                if (best != null)
                {
                    graph.Connect(de, best, isShortcut: true);
                    de.IsDeadEnd = false;
                }
            }
        }

        private void MarkDeadEnds(DungeonGraph graph)
        {
            foreach (var node in graph.Nodes)
            {
                if (node.Edges.Count == 1 && node != graph.StartNode && node != graph.BossNode
                    && node != graph.StairUpNode && node != graph.StairDownNode)
                {
                    node.IsDeadEnd = true;
                }
            }
        }

        private void PlaceSpecialRooms(DungeonGraph graph, int floorIndex)
        {
            var deadEnds = graph.GetDeadEnds();

            foreach (var de in deadEnds)
            {
                if (_rng.NextDouble() < 0.3)
                {
                    de.Type = RoomType.SecretRoom;
                    if (de.Edges.Count > 0)
                        de.Edges[0].IsSecret = true;
                }
            }

            if (_config.guaranteeShop)
            {
                var candidate = FindNodeForSpecialRoom(graph, RoomType.Shop);
                if (candidate != null) candidate.Type = RoomType.Shop;
            }

            if (_config.guaranteeSafeRoom)
            {
                var candidate = FindNodeForSpecialRoom(graph, RoomType.SafeRoom);
                if (candidate != null) candidate.Type = RoomType.SafeRoom;
            }
        }

        private GraphNode FindNodeForSpecialRoom(DungeonGraph graph, RoomType avoidDuplicate)
        {
            var candidates = graph.Nodes
                .Where(n => !n.IsMainPath
                    && n.Type == RoomType.Combat
                    && n.Type != avoidDuplicate)
                .ToList();

            if (candidates.Count == 0)
            {
                candidates = graph.Nodes
                    .Where(n => n.Type == RoomType.Combat && n != graph.StartNode && n != graph.BossNode)
                    .ToList();
            }

            return candidates.Count > 0 ? candidates[_rng.Next(candidates.Count)] : null;
        }

        private void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
