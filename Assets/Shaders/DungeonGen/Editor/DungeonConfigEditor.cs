#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Linq;
using DungeonSystem.Data;
using DungeonSystem.Core;

namespace DungeonSystem.Editor
{
    [CustomEditor(typeof(DungeonConfig))]
    public class DungeonConfigEditor : UnityEditor.Editor
    {
        bool _showRecipeStatus = true;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var config = (DungeonConfig)target;

            EditorGUILayout.Space(12);

            // ── Recipe Management Section ──
            _showRecipeStatus = EditorGUILayout.Foldout(_showRecipeStatus,
                "Recipe Status", true, EditorStyles.foldoutHeader);

            if (_showRecipeStatus)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Show coverage
                int totalTypes = 0;
                int coveredTypes = 0;
                foreach (RoomType type in System.Enum.GetValues(typeof(RoomType)))
                {
                    if (type == RoomType.Corridor || type == RoomType.Junction) continue;
                    totalTypes++;
                    if (config.GetRecipe(type) != null) coveredTypes++;
                }

                Color barColor = coveredTypes == totalTypes
                    ? new Color(0.3f, 0.8f, 0.3f)
                    : coveredTypes > 0
                        ? new Color(0.8f, 0.7f, 0.2f)
                        : new Color(0.8f, 0.3f, 0.3f);

                GUI.color = barColor;
                EditorGUILayout.LabelField(
                    $"Coverage: {coveredTypes}/{totalTypes} room types have recipes",
                    EditorStyles.boldLabel);
                GUI.color = Color.white;

                // Show which types are missing
                if (coveredTypes < totalTypes)
                {
                    string missing = "";
                    foreach (RoomType type in System.Enum.GetValues(typeof(RoomType)))
                    {
                        if (type == RoomType.Corridor || type == RoomType.Junction) continue;
                        if (config.GetRecipe(type) == null)
                            missing += (missing.Length > 0 ? ", " : "") + type;
                    }
                    EditorGUILayout.HelpBox($"Missing: {missing}", MessageType.Info);
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();

                GUI.backgroundColor = new Color(0.3f, 0.6f, 0.9f);
                if (GUILayout.Button("Auto-Generate & Assign ALL Recipes", GUILayout.Height(28)))
                {
                    int count = RecipeAutoGenerator.GenerateAndAssign(config);
                    EditorUtility.DisplayDialog("Done",
                        $"Added {count} new recipes to this config.\n" +
                        $"Total recipes: {config.roomRecipes.Count}",
                        "OK");
                }

                GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
                if (GUILayout.Button("Open Recipe Builder", GUILayout.Height(28)))
                {
                    RecipeBuilderWindow.ShowWindow();
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();

                // Quick links to each recipe
                if (config.roomRecipes != null && config.roomRecipes.Count > 0)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Quick Edit:", EditorStyles.miniLabel);
                    EditorGUILayout.BeginHorizontal();
                    int col = 0;
                    foreach (var recipe in config.roomRecipes)
                    {
                        if (recipe == null) continue;
                        Color c = GetTypeColor(recipe.roomType);
                        GUI.backgroundColor = c;
                        if (GUILayout.Button(recipe.roomType.ToString(),
                            EditorStyles.miniButton, GUILayout.Width(70)))
                        {
                            Selection.activeObject = recipe;
                        }
                        col++;
                        if (col >= 5)
                        {
                            col = 0;
                            EditorGUILayout.EndHorizontal();
                            EditorGUILayout.BeginHorizontal();
                        }
                    }
                    GUI.backgroundColor = Color.white;
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
            }
        }

        static Color GetTypeColor(RoomType type) => type switch
        {
            RoomType.Start => new Color(0.4f, 0.8f, 0.4f),
            RoomType.Boss => new Color(0.9f, 0.3f, 0.3f),
            RoomType.MiniBoss => new Color(0.9f, 0.5f, 0.3f),
            RoomType.Combat => new Color(0.6f, 0.6f, 0.6f),
            RoomType.Loot => new Color(0.9f, 0.8f, 0.3f),
            RoomType.Puzzle => new Color(0.3f, 0.6f, 0.9f),
            RoomType.Shop => new Color(0.4f, 0.8f, 0.4f),
            RoomType.SafeRoom => new Color(0.4f, 0.7f, 0.9f),
            RoomType.SecretRoom => new Color(0.7f, 0.3f, 0.7f),
            RoomType.Trap => new Color(0.9f, 0.6f, 0.3f),
            _ => new Color(0.7f, 0.7f, 0.7f)
        };
    }
}
#endif
