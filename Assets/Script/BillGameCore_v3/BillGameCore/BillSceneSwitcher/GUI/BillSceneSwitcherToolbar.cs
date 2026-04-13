#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UIElements;

namespace BillGameCore.BillSceneSwitcher
{
    /// <summary>
    /// Injects a Scene Switcher button into Unity's main toolbar, next to the Play button.
    /// Uses VisualElement injection for Unity 2021+.
    /// </summary>
    [InitializeOnLoad]
    static class BillSceneSwitcherToolbar
    {
        static readonly Type s_toolbarType = typeof(Editor).Assembly.GetType("UnityEditor.Toolbar");
        static ScriptableObject s_currentToolbar;
        static VisualElement s_buttonContainer;
        static bool s_injected;

        static BillSceneSwitcherToolbar()
        {
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
        }

        static void OnUpdate()
        {
            if (s_injected && s_currentToolbar != null) return;

            if (!BillSceneSwitcherPrefs.Enabled || !BillSceneSwitcherPrefs.ShowInToolbar)
            {
                RemoveButton();
                return;
            }

            var toolbars = Resources.FindObjectsOfTypeAll(s_toolbarType);
            if (toolbars == null || toolbars.Length == 0) return;

            s_currentToolbar = (ScriptableObject)toolbars[0];

            var root = GetRootVisualElement(s_currentToolbar);
            if (root == null) return;

            InjectButton(root);
        }

        static VisualElement GetRootVisualElement(ScriptableObject toolbar)
        {
            var prop = toolbar.GetType().GetProperty("rootVisualElement",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return prop?.GetValue(toolbar) as VisualElement;
        }

        static void InjectButton(VisualElement root)
        {
            // Place button right after the Play/Pause/Step zone
            var playZone = root.Q("ToolbarZonePlayMode");
            if (playZone == null) return;

            if (s_buttonContainer != null)
            {
                s_buttonContainer.RemoveFromHierarchy();
                s_buttonContainer = null;
            }

            s_buttonContainer = new VisualElement();
            s_buttonContainer.name = "BillSceneSwitcher";
            s_buttonContainer.style.flexDirection = FlexDirection.Row;
            s_buttonContainer.style.alignItems = Align.Center;
            s_buttonContainer.style.marginLeft = 6;

            var imgui = new IMGUIContainer(DrawToolbarGUI);
            imgui.style.width = 140;
            imgui.style.minWidth = 80;
            imgui.style.height = 22;

            s_buttonContainer.Add(imgui);

            // Insert after play zone
            var parent = playZone.parent;
            if (parent != null)
            {
                int idx = parent.IndexOf(playZone);
                parent.Insert(idx + 1, s_buttonContainer);
            }

            s_injected = true;
        }

        static void RemoveButton()
        {
            if (s_buttonContainer != null)
            {
                s_buttonContainer.RemoveFromHierarchy();
                s_buttonContainer = null;
            }
            s_injected = false;
            s_currentToolbar = null;
        }

        // ───────────────────────────────────────────
        // IMGUI drawing for the toolbar button
        // ───────────────────────────────────────────

        static GUIStyle s_buttonStyle;

        static void DrawToolbarGUI()
        {
            if (!BillSceneSwitcherPrefs.Enabled) return;

            s_buttonStyle ??= new GUIStyle("ToolbarDropDownLeft")
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(22, 8, 0, 0),
                fixedHeight = 22
            };

            var sceneName = GetActiveSceneName();
            var icon = EditorGUIUtility.IconContent("SceneAsset Icon");
            var content = new GUIContent(" " + TruncateName(sceneName, 14), icon.image, "Scene Switcher - Click to switch scenes");

            var rect = GUILayoutUtility.GetRect(content, s_buttonStyle, GUILayout.MinWidth(80), GUILayout.MaxWidth(160));

            if (GUI.Button(rect, content, s_buttonStyle))
            {
                var screenRect = GUIUtility.GUIToScreenRect(rect);
                BillSceneSwitcherDropdown.Show(screenRect);
            }
        }

        static string GetActiveSceneName()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(scene.name)) return scene.name;
            if (!string.IsNullOrEmpty(scene.path))
                return System.IO.Path.GetFileNameWithoutExtension(scene.path);
            return "Untitled";
        }

        static string TruncateName(string name, int maxLen)
        {
            return name.Length <= maxLen ? name : name.Substring(0, maxLen - 2) + "..";
        }

        // ───────────────────────────────────────────
        // Domain reload cleanup
        // ───────────────────────────────────────────

        [InitializeOnLoadMethod]
        static void DomainReloadCleanup()
        {
            s_currentToolbar = null;
            s_buttonContainer = null;
            s_buttonStyle = null;
            s_injected = false;
        }
    }
}
#endif
