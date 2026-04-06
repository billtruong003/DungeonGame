#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PrefabGallery.Editor
{
    public static class FolderScanner
    {
        /// <summary>
        /// Scan a folder and add all prefabs into one category.
        /// </summary>
        public static int ScanFlat(
            GalleryCategory category,
            string folderPath,
            string previewFolder,
            bool recursive,
            bool skipDuplicates = true)
        {
            if (category == null || !AssetDatabase.IsValidFolder(folderPath)) return 0;

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
            var existingGuids = new HashSet<string>(
                category.entries.Where(e => !string.IsNullOrEmpty(e.guid)).Select(e => e.guid));

            int added = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);

                if (!recursive)
                {
                    string assetDir = Path.GetDirectoryName(assetPath).Replace("\\", "/");
                    if (assetDir != folderPath) continue;
                }

                if (skipDuplicates && existingGuids.Contains(guids[i])) continue;

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null) continue;

                EditorUtility.DisplayProgressBar("Scanning",
                    $"{prefab.name} ({i + 1}/{guids.Length})", (float)i / guids.Length);

                string safeName = PreviewGenerator.SanitizeName(prefab.name);
                string previewPath = $"{previewFolder}/{safeName}_preview.png";

                category.entries.Add(new PrefabEntry
                {
                    name = prefab.name,
                    prefab = prefab,
                    guid = guids[i],
                    assetPath = assetPath,
                    defaultScale = Vector3.one,
                    preview = PreviewGenerator.GeneratePreview(prefab, previewPath)
                });
                added++;
            }

            EditorUtility.ClearProgressBar();
            return added;
        }

        /// <summary>
        /// Smart scan: each direct subfolder becomes a category.
        /// Prefabs at root level go into a "General" category.
        /// </summary>
        public static int ScanSmart(
            GalleryTheme theme,
            string rootFolder,
            string previewFolder,
            bool skipDuplicates = true)
        {
            if (theme == null || !AssetDatabase.IsValidFolder(rootFolder)) return 0;

            int totalAdded = 0;

            // 1) Root-level prefabs → "General" category
            var rootGuids = AssetDatabase.FindAssets("t:Prefab", new[] { rootFolder });
            var rootPrefabs = new List<string>();
            foreach (var g in rootGuids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                string dir = Path.GetDirectoryName(p).Replace("\\", "/");
                if (dir == rootFolder)
                    rootPrefabs.Add(g);
            }

            if (rootPrefabs.Count > 0)
            {
                var generalCat = theme.GetOrCreateCategory("General");
                totalAdded += AddPrefabsByGuid(generalCat, rootPrefabs, previewFolder, skipDuplicates);
            }

            // 2) Each direct subfolder → named category
            string[] subfolders = AssetDatabase.GetSubFolders(rootFolder);
            foreach (string subfolder in subfolders)
            {
                string catName = Path.GetFileName(subfolder);
                // Capitalize first letter
                if (catName.Length > 0)
                    catName = char.ToUpper(catName[0]) + catName.Substring(1);

                var cat = theme.GetOrCreateCategory(catName);
                var subGuids = AssetDatabase.FindAssets("t:Prefab", new[] { subfolder });

                totalAdded += AddPrefabsByGuid(cat, subGuids.ToList(), previewFolder, skipDuplicates);
            }

            EditorUtility.ClearProgressBar();
            return totalAdded;
        }

        private static int AddPrefabsByGuid(
            GalleryCategory category,
            List<string> guids,
            string previewFolder,
            bool skipDuplicates)
        {
            var existing = new HashSet<string>(
                category.entries.Where(e => !string.IsNullOrEmpty(e.guid)).Select(e => e.guid));

            int added = 0;
            for (int i = 0; i < guids.Count; i++)
            {
                if (skipDuplicates && existing.Contains(guids[i])) continue;

                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null) continue;

                EditorUtility.DisplayProgressBar("Scanning",
                    $"{prefab.name} ({i + 1}/{guids.Count})", (float)i / guids.Count);

                string safeName = PreviewGenerator.SanitizeName(prefab.name);
                string previewPath = $"{previewFolder}/{safeName}_preview.png";

                category.entries.Add(new PrefabEntry
                {
                    name = prefab.name,
                    prefab = prefab,
                    guid = guids[i],
                    assetPath = assetPath,
                    defaultScale = Vector3.one,
                    preview = PreviewGenerator.GeneratePreview(prefab, previewPath)
                });
                added++;
            }

            return added;
        }

        /// <summary>
        /// Scan a selected folder from Project window.
        /// Returns the folder path or null.
        /// </summary>
        public static string GetSelectedFolderPath()
        {
            foreach (var obj in Selection.GetFiltered<Object>(SelectionMode.Assets))
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (AssetDatabase.IsValidFolder(path))
                    return path;

                // If a file is selected, use its parent directory
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && AssetDatabase.IsValidFolder(dir.Replace("\\", "/")))
                    return dir.Replace("\\", "/");
            }
            return null;
        }
    }
}
#endif
