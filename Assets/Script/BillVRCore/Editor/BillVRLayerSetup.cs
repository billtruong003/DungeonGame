#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace BillVRCore.Editor
{
    public static class BillVRLayerSetup
    {
        public static readonly (string name, int index)[] RequiredLayers = new[]
        {
            ("PlayerBody", 8),
            ("RagdollDummy", 9),
            ("Grabbable", 10),
            ("HandPhysics", 11),
            ("InventorySlot", 12),
            ("GroundCheck", 13),
            ("BodyIK", 14)
        };

        public static readonly (int a, int b)[] IgnorePairs = new[]
        {
            // PlayerBody — hands/inventory shouldn't push player, utility layers are raycast-only
            (8, 9),   // PlayerBody ↔ RagdollDummy
            (8, 11),  // PlayerBody ↔ HandPhysics
            (8, 12),  // PlayerBody ↔ InventorySlot
            (8, 13),  // PlayerBody ↔ GroundCheck
            (8, 14),  // PlayerBody ↔ BodyIK
            // RagdollDummy — no interaction with utility layers
            (9, 12),  // RagdollDummy ↔ InventorySlot
            (9, 13),  // RagdollDummy ↔ GroundCheck
            (9, 14),  // RagdollDummy ↔ BodyIK
            // GroundCheck & BodyIK are raycast-only, ignore all physics collisions
            (10, 13), // Grabbable ↔ GroundCheck
            (10, 14), // Grabbable ↔ BodyIK
            (11, 13), // HandPhysics ↔ GroundCheck
            (11, 14), // HandPhysics ↔ BodyIK
            (12, 13), // InventorySlot ↔ GroundCheck
            (12, 14), // InventorySlot ↔ BodyIK
            (13, 14), // GroundCheck ↔ BodyIK
        };

        public static readonly (int a, int b)[] AllowPairs = new[]
        {
            (8, 10),  // PlayerBody ↔ Grabbable (body bumps objects)
            (9, 10),  // RagdollDummy ↔ Grabbable (ragdoll hits objects)
            (9, 11),  // RagdollDummy ↔ HandPhysics (hands interact with ragdolls)
            (10, 11), // Grabbable ↔ HandPhysics (core grab interaction)
            (10, 12), // Grabbable ↔ InventorySlot (items snap to slots)
            (11, 12), // HandPhysics ↔ InventorySlot (hands reach into slots)
        };

        public static bool AllLayersExist()
        {
            foreach (var (name, index) in RequiredLayers)
            {
                string existing = LayerMask.LayerToName(index);
                if (string.IsNullOrEmpty(existing) || existing != name)
                    return false;
            }
            return true;
        }

        public static void CreateAllLayers()
        {
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/TagManager.asset"));

            SerializedProperty layers = tagManager.FindProperty("layers");

            foreach (var (name, index) in RequiredLayers)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(index);
                if (layer.stringValue != name)
                {
                    layer.stringValue = name;
                    Debug.Log($"[BillVR] Created layer {index}: {name}");
                }
            }

            tagManager.ApplyModifiedProperties();
        }

        public static void ConfigureCollisionMatrix()
        {
            for (int i = 0; i < 32; i++)
            {
                for (int j = i; j < 32; j++)
                {
                    bool isIgnored = false;
                    foreach (var (a, b) in IgnorePairs)
                    {
                        if ((i == a && j == b) || (i == b && j == a))
                        {
                            isIgnored = true;
                            break;
                        }
                    }

                    bool isRequired = IsRequiredLayer(i) && IsRequiredLayer(j);
                    if (!isRequired) continue;

                    Physics.IgnoreLayerCollision(i, j, isIgnored);
                }
            }

            Debug.Log("[BillVR] Collision matrix configured.");
        }

        private static bool IsRequiredLayer(int index)
        {
            foreach (var (_, layerIndex) in RequiredLayers)
            {
                if (layerIndex == index) return true;
            }
            return false;
        }

        public static string GetLayerReport()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Layer Status:");

            foreach (var (name, index) in RequiredLayers)
            {
                string existing = LayerMask.LayerToName(index);
                bool ok = existing == name;
                string status = ok ? "OK" : (string.IsNullOrEmpty(existing) ? "MISSING" : $"CONFLICT ({existing})");
                sb.AppendLine($"  [{index}] {name}: {status}");
            }

            return sb.ToString();
        }
    }
}
#endif
