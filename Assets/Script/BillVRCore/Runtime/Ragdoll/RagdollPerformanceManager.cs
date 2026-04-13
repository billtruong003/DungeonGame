#if VRCORE_HAS_RAGDOLL
using FIMSpace.FProceduralAnimation;
#endif
using System.Collections.Generic;
using UnityEngine;

namespace BillVRCore.Ragdoll
{
    public class RagdollPerformanceManager : MonoBehaviour
    {
        [Header("LOD Distances")]
        [SerializeField] private float highDetailDistance = 5f;
        [SerializeField] private float mediumDetailDistance = 15f;
        [SerializeField] private float lowDetailDistance = 30f;
        [SerializeField] private float disableDistance = 50f;

        [Header("Solver Iterations")]
        [SerializeField] private int highIterations = 12;
        [SerializeField] private int mediumIterations = 6;
        [SerializeField] private int lowIterations = 3;

        [Header("Settings")]
        [SerializeField] private float updateInterval = 0.5f;
        [SerializeField] private int maxActiveRagdolls = 10;

        public static RagdollPerformanceManager Instance { get; private set; }

        private readonly List<RagdollLODEntry> _entries = new();
        private Transform _cameraTransform;
        private float _lastUpdateTime;

        private struct RagdollLODEntry
        {
            public RagdollBridge bridge;
            public Rigidbody[] rigidbodies;
            public float distance;
            public RagdollLOD currentLOD;
        }

        private enum RagdollLOD { High, Medium, Low, Disabled }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            var cam = Camera.main;
            if (cam != null) _cameraTransform = cam.transform;
        }

        private void Update()
        {
            if (Time.time - _lastUpdateTime < updateInterval) return;
            _lastUpdateTime = Time.time;

            if (_cameraTransform == null) return;

            UpdateDistances();
            SortByDistance();
            ApplyLODs();
        }

        public void Register(RagdollBridge bridge)
        {
            if (bridge == null) return;

            foreach (var entry in _entries)
            {
                if (entry.bridge == bridge) return;
            }

            _entries.Add(new RagdollLODEntry
            {
                bridge = bridge,
                rigidbodies = bridge.GetComponentsInChildren<Rigidbody>(),
                distance = float.MaxValue,
                currentLOD = RagdollLOD.High
            });
        }

        public void Unregister(RagdollBridge bridge)
        {
            _entries.RemoveAll(e => e.bridge == bridge);
        }

        private void UpdateDistances()
        {
            Vector3 camPos = _cameraTransform.position;

            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry.bridge == null)
                {
                    _entries.RemoveAt(i--);
                    continue;
                }
                entry.distance = Vector3.Distance(camPos, entry.bridge.transform.position);
                _entries[i] = entry;
            }
        }

        private void SortByDistance()
        {
            _entries.Sort((a, b) => a.distance.CompareTo(b.distance));
        }

        private void ApplyLODs()
        {
            int activeCount = 0;

            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                RagdollLOD targetLOD = CalculateLOD(entry.distance, activeCount);

                if (targetLOD != entry.currentLOD)
                {
                    ApplyLODToEntry(ref entry, targetLOD);
                    _entries[i] = entry;
                }

                if (targetLOD != RagdollLOD.Disabled)
                    activeCount++;
            }
        }

        private RagdollLOD CalculateLOD(float distance, int currentActive)
        {
            if (currentActive >= maxActiveRagdolls) return RagdollLOD.Disabled;
            if (distance > disableDistance) return RagdollLOD.Disabled;
            if (distance > lowDetailDistance) return RagdollLOD.Low;
            if (distance > mediumDetailDistance) return RagdollLOD.Medium;
            if (distance > highDetailDistance) return RagdollLOD.Medium;
            return RagdollLOD.High;
        }

        private void ApplyLODToEntry(ref RagdollLODEntry entry, RagdollLOD lod)
        {
            entry.currentLOD = lod;

            int iterations = lod switch
            {
                RagdollLOD.High => highIterations,
                RagdollLOD.Medium => mediumIterations,
                RagdollLOD.Low => lowIterations,
                _ => 1
            };

            bool active = lod != RagdollLOD.Disabled;

            foreach (var rb in entry.rigidbodies)
            {
                if (rb == null) continue;
                rb.solverIterations = iterations;
                rb.detectCollisions = active;
            }

#if VRCORE_HAS_RAGDOLL
            var ragdollAnimator = entry.bridge.GetComponent<FIMSpace.FProceduralAnimation.RagdollAnimator2>();
            if (ragdollAnimator != null)
                ragdollAnimator.enabled = active;
#endif
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
