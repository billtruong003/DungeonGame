using System.Collections.Generic;
using UnityEngine;
using DungeonSystem.Data;
using DungeonSystem.Graph;
using DungeonSystem.Layout;

namespace DungeonSystem.Runtime
{
    public class DungeonGenerator : MonoBehaviour
    {
        [Header("Configuration")]
        public DungeonConfig config;

        [Header("Debug")]
        public bool logTimings = true;

        private readonly List<GameObject> _floorContainers = new List<GameObject>();
        private readonly List<FloorResult> _floorResults = new List<FloorResult>();

        public IReadOnlyList<FloorResult> FloorResults => _floorResults;

        public void GenerateDungeon()
        {
            ClearDungeon();

            if (config == null || config.roomDatabase == null)
            {
                Debug.LogError("[DungeonGenerator] Config or RoomDatabase is null.");
                return;
            }

            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            int seed = config.useFixedSeed ? config.fixedSeed : System.Environment.TickCount;
            var rng = new System.Random(seed);

            if (logTimings) Debug.Log($"[DungeonGenerator] Seed: {seed}");

            var graphGen = new GraphGenerator(config, rng);
            var layoutSolver = new LayoutSolver(config, rng);
            var instantiator = new RoomInstantiator(config, rng);

            for (int floor = 0; floor < config.totalFloors; floor++)
            {
                var floorStopwatch = new System.Diagnostics.Stopwatch();
                floorStopwatch.Start();

                bool isFirst = floor == 0;
                bool isLast = floor == config.totalFloors - 1;

                DungeonGraph graph = graphGen.Generate(floor, isFirst, isLast);
                AssignTemplateSizes(graph, floor, rng);
                FloorLayout layout = layoutSolver.Solve(graph, floor);

                var floorGO = new GameObject($"Floor_{floor}");
                floorGO.transform.SetParent(transform);
                floorGO.transform.localPosition = Vector3.zero;
                _floorContainers.Add(floorGO);

                var instances = instantiator.Instantiate(layout, floor, floorGO.transform);

                var result = new FloorResult
                {
                    FloorIndex = floor,
                    Graph = graph,
                    Layout = layout,
                    Instances = instances,
                    Container = floorGO
                };
                _floorResults.Add(result);

                floorStopwatch.Stop();
                if (logTimings)
                {
                    Debug.Log($"[DungeonGenerator] Floor {floor}: " +
                        $"{graph.Nodes.Count} rooms, {graph.Edges.Count} edges, " +
                        $"{layout.Corridors.Count} corridors, " +
                        $"{floorStopwatch.ElapsedMilliseconds}ms");
                }
            }

            stopwatch.Stop();
            if (logTimings)
                Debug.Log($"[DungeonGenerator] Total generation: {stopwatch.ElapsedMilliseconds}ms, {config.totalFloors} floors");
        }

        private void AssignTemplateSizes(DungeonGraph graph, int floorIndex, System.Random rng)
        {
            foreach (var node in graph.Nodes)
            {
                var template = config.roomDatabase.GetRandom(node.Type, floorIndex, rng);
                if (template != null)
                {
                    node.AssignedTemplate = template;
                    node.TemplateWidth = template.widthInCells;
                    node.TemplateHeight = template.heightInCells;
                }
                else
                {
                    node.TemplateWidth = 1;
                    node.TemplateHeight = 1;

                    if (node.Type == Core.RoomType.Boss)
                    {
                        node.TemplateWidth = 2;
                        node.TemplateHeight = 2;
                    }
                }
            }
        }

        public void ClearDungeon()
        {
            foreach (var container in _floorContainers)
            {
                if (container != null)
                {
                    if (Application.isPlaying) Destroy(container);
                    else DestroyImmediate(container);
                }
            }
            _floorContainers.Clear();
            _floorResults.Clear();

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }
    }

    public class FloorResult
    {
        public int FloorIndex;
        public DungeonGraph Graph;
        public FloorLayout Layout;
        public Dictionary<PlacedRoom, RoomInstance> Instances;
        public GameObject Container;
    }
}
