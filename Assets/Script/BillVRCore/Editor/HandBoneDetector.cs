#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BillVRCore.Hand;

namespace BillVRCore.Editor
{
    public static class HandBoneDetector
    {
        private static readonly string[] ThumbKeywords = { "thumb", "thmb", "thb", "finger0", "metacarpal1" };
        private static readonly string[] IndexKeywords = { "index", "idx", "finger1", "point" };
        private static readonly string[] MiddleKeywords = { "middle", "mid", "finger2" };
        private static readonly string[] RingKeywords = { "ring", "finger3" };
        private static readonly string[] PinkyKeywords = { "pinky", "little", "finger4", "mini" };

        public struct DetectedFinger
        {
            public FingerType type;
            public Transform[] joints;
            public Transform tip;
            public float confidence;
        }

        public static DetectedFinger[] DetectFingers(Transform handRoot)
        {
            var leaves = FindLeafBones(handRoot);
            var chains = new List<List<Transform>>();

            foreach (var leaf in leaves)
            {
                var chain = TraceChainToRoot(leaf, handRoot);
                if (chain.Count >= 3 && chain.Count <= 6)
                    chains.Add(chain);
            }

            if (chains.Count < 5)
            {
                var extendedLeaves = FindNearLeafBones(handRoot, 2);
                foreach (var leaf in extendedLeaves)
                {
                    var chain = TraceChainToRoot(leaf, handRoot);
                    if (chain.Count >= 3 && chain.Count <= 6 && !ChainOverlaps(chains, chain))
                        chains.Add(chain);
                }
            }

            return AssignFingerTypes(chains, handRoot);
        }

        private static DetectedFinger[] AssignFingerTypes(List<List<Transform>> chains, Transform root)
        {
            var results = new List<DetectedFinger>();
            var assigned = new HashSet<int>();

            for (int i = 0; i < chains.Count; i++)
            {
                FingerType? matched = MatchByName(chains[i]);
                if (matched.HasValue && !results.Any(r => r.type == matched.Value))
                {
                    results.Add(CreateFinger(matched.Value, chains[i]));
                    assigned.Add(i);
                }
            }

            var unassigned = chains.Where((_, i) => !assigned.Contains(i)).ToList();
            if (unassigned.Count > 0 && !results.Any(r => r.type == FingerType.Thumb))
            {
                var thumb = FindThumbByAngle(unassigned, root);
                if (thumb != null)
                {
                    results.Add(CreateFinger(FingerType.Thumb, thumb));
                    unassigned.Remove(thumb);
                }
            }

            var remainingTypes = new[] { FingerType.Index, FingerType.Middle, FingerType.Ring, FingerType.Pinky }
                .Where(t => !results.Any(r => r.type == t)).ToList();

            var sortedByPosition = SortChainsByPosition(unassigned, root);
            for (int i = 0; i < Mathf.Min(sortedByPosition.Count, remainingTypes.Count); i++)
                results.Add(CreateFinger(remainingTypes[i], sortedByPosition[i]));

            return results.OrderBy(f => (int)f.type).ToArray();
        }

        private static FingerType? MatchByName(List<Transform> chain)
        {
            string combined = string.Join(" ", chain.Select(t => t.name.ToLowerInvariant()));

            if (ThumbKeywords.Any(k => combined.Contains(k))) return FingerType.Thumb;
            if (IndexKeywords.Any(k => combined.Contains(k))) return FingerType.Index;
            if (MiddleKeywords.Any(k => combined.Contains(k))) return FingerType.Middle;
            if (RingKeywords.Any(k => combined.Contains(k))) return FingerType.Ring;
            if (PinkyKeywords.Any(k => combined.Contains(k))) return FingerType.Pinky;

            return null;
        }

        private static List<Transform> FindThumbByAngle(List<List<Transform>> chains, Transform root)
        {
            List<Transform> bestCandidate = null;
            float bestAngle = 0f;

            Vector3 palmForward = root.forward;

            foreach (var chain in chains)
            {
                if (chain.Count < 3) continue;

                Vector3 fingerDir = (chain[^1].position - chain[0].position).normalized;
                float angle = Vector3.Angle(palmForward, fingerDir);

                if (angle > bestAngle)
                {
                    bestAngle = angle;
                    bestCandidate = chain;
                }
            }

            return bestAngle > 20f ? bestCandidate : null;
        }

        private static List<List<Transform>> SortChainsByPosition(List<List<Transform>> chains, Transform root)
        {
            return chains.OrderBy(c =>
            {
                Vector3 local = root.InverseTransformPoint(c[0].position);
                return local.x;
            }).ToList();
        }

        private static DetectedFinger CreateFinger(FingerType type, List<Transform> chain)
        {
            return new DetectedFinger
            {
                type = type,
                joints = chain.ToArray(),
                tip = chain[^1],
                confidence = chain.All(t => MatchByName(chain).HasValue) ? 1f : 0.6f
            };
        }

        private static List<Transform> FindLeafBones(Transform root)
        {
            var leaves = new List<Transform>();
            FindLeavesRecursive(root, leaves);
            return leaves;
        }

        private static void FindLeavesRecursive(Transform node, List<Transform> leaves)
        {
            if (node.childCount == 0)
            {
                leaves.Add(node);
                return;
            }

            for (int i = 0; i < node.childCount; i++)
                FindLeavesRecursive(node.GetChild(i), leaves);
        }

        private static List<Transform> FindNearLeafBones(Transform root, int maxChildDepth)
        {
            var result = new List<Transform>();
            FindNearLeavesRecursive(root, result, maxChildDepth);
            return result;
        }

        private static void FindNearLeavesRecursive(Transform node, List<Transform> result, int maxDepth)
        {
            if (maxDepth <= 0 || node.childCount <= 1)
            {
                result.Add(node);
                return;
            }

            for (int i = 0; i < node.childCount; i++)
                FindNearLeavesRecursive(node.GetChild(i), result, maxDepth - 1);
        }

        private static List<Transform> TraceChainToRoot(Transform leaf, Transform root)
        {
            var chain = new List<Transform>();
            Transform current = leaf;

            while (current != null && current != root)
            {
                chain.Add(current);
                current = current.parent;
            }

            chain.Reverse();

            // Strip the first bone if it's a shared parent (e.g. hand/wrist bone)
            // that has more than 1 child — a real finger joint typically has only
            // 1 child (the next joint), while a hand root fans out to multiple fingers.
            while (chain.Count > 3 && chain[0].childCount > 1)
                chain.RemoveAt(0);

            return chain;
        }

        private static bool ChainOverlaps(List<List<Transform>> existing, List<Transform> candidate)
        {
            foreach (var chain in existing)
            {
                if (chain.Intersect(candidate).Any())
                    return true;
            }
            return false;
        }
    }
}
#endif
