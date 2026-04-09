#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using VRCore.Hand;
using VRCore.Input;
using VRCore.Inventory;

namespace VRCore.Editor
{
    public static class VRCoreAssetCreator
    {
        private const string ConfigPath = "Assets/VRCore/Data";

        [MenuItem("VRCore/Create Default Configs", priority = 42)]
        public static void CreateAllDefaults()
        {
            EnsureDirectory();
            CreateDefaultFingerMapping();
            CreateDefaultHandPoses();
            CreateDefaultItemData();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[VRCore] Default config assets created in Assets/VRCore/Data/");
        }

        public static FingerMappingConfig CreateDefaultFingerMapping()
        {
            string path = $"{ConfigPath}/DefaultFingerMapping.asset";
            var existing = AssetDatabase.LoadAssetAtPath<FingerMappingConfig>(path);
            if (existing != null) return existing;

            var config = ScriptableObject.CreateInstance<FingerMappingConfig>();
            AssetDatabase.CreateAsset(config, path);
            return config;
        }

        public static void CreateDefaultHandPoses()
        {
            CreatePose("OpenHand", 0f, 0f, 0f, 0f, 0f);
            CreatePose("ClosedFist", 1f, 1f, 1f, 1f, 1f);
            CreatePose("Pointing", 0.8f, 0f, 1f, 1f, 1f);
            CreatePose("ThumbsUp", 0f, 1f, 1f, 1f, 1f);
            CreatePose("PistolGrip", 0.7f, 0.3f, 0.9f, 0.9f, 0.9f);
            CreatePose("RifleGrip", 0.6f, 0.5f, 0.85f, 0.85f, 0.85f);
            CreatePose("SwordGrip", 0.8f, 0.7f, 0.95f, 0.95f, 0.95f);
            CreatePose("Pinch", 0.9f, 0.9f, 0.1f, 0.1f, 0.1f);
        }

        public static void CreateDefaultItemData()
        {
            CreateItemData("Weapon_Pistol", ItemCategory.Weapon,
                new[] { SlotType.HipRight, SlotType.HipLeft });
            CreateItemData("Weapon_Rifle", ItemCategory.Weapon,
                new[] { SlotType.Back });
            CreateItemData("Weapon_Melee", ItemCategory.Weapon,
                new[] { SlotType.HipLeft, SlotType.Back });
            CreateItemData("Ammo_Magazine", ItemCategory.Ammo,
                new[] { SlotType.Belt, SlotType.Chest });
            CreateItemData("Item_Generic", ItemCategory.Generic,
                new[] { SlotType.Any });
        }

        private static void CreatePose(string name, float thumb, float index, float middle, float ring, float pinky)
        {
            string path = $"{ConfigPath}/Poses/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<HandPoseData>(path) != null) return;

            EnsureDirectory($"{ConfigPath}/Poses");

            var pose = ScriptableObject.CreateInstance<HandPoseData>();
            pose.SetAll(thumb, index, middle, ring, pinky);
            AssetDatabase.CreateAsset(pose, path);
        }

        private static void CreateItemData(string name, ItemCategory category, SlotType[] slots)
        {
            string path = $"{ConfigPath}/Items/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<ItemData>(path) != null) return;

            EnsureDirectory($"{ConfigPath}/Items");

            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemName = name.Replace("_", " ");
            item.category = category;
            item.compatibleSlots = slots;
            AssetDatabase.CreateAsset(item, path);
        }

        private static void EnsureDirectory(string path = null)
        {
            path ??= ConfigPath;
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
            }
        }
    }
}
#endif
