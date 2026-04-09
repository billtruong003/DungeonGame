#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace VRCore.Editor
{
    public static class VRCoreLayerSetup
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
            (8, 9),
            (8, 11),
            (9, 12),
        };

        public static readonly (int a, int b)[] AllowPairs = new[]
        {
            (11, 12),
            (9, 11),
            (10, 11),
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
                    Debug.Log($"[VRCore] Created layer {index}: {name}");
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

            Debug.Log("[VRCore] Collision matrix configured.");
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
