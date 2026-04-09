#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using VRCore.Hand;
using VRCore.Input;
using VRCore.Locomotion;
using VRCore.Tracking;

namespace VRCore.Editor
{
    public class VRCoreSceneValidator : EditorWindow
    {
        private Vector2 _scroll;

        [MenuItem("VRCore/Validate Scene", priority = 43)]
        public static void ShowWindow()
        {
            var window = GetWindow<VRCoreSceneValidator>("Scene Validator");
            window.minSize = new Vector2(400, 400);
            window.Show();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("VRCore Scene Validation", EditorStyles.boldLabel);
            EditorGUILayout.Space(8);

            int issues = 0;
            issues += CheckComponent<VRCoreBootstrap>("VRCoreBootstrap");
            issues += CheckComponent<InputManager>("InputManager");
            issues += CheckHands();
            issues += CheckTracking();
            issues += CheckPlayerBody();
            issues += CheckLayers();
            issues += CheckPhysicsSettings();

            EditorGUILayout.Space(12);
            if (issues == 0)
                EditorGUILayout.HelpBox("Scene is properly configured.", MessageType.Info);
            else
                EditorGUILayout.HelpBox($"{issues} issue(s) found. Fix them or run Setup Wizard.", MessageType.Warning);

            if (issues > 0 && GUILayout.Button("Open Setup Wizard"))
                VRCoreSetupWizard.ShowWindow();

            EditorGUILayout.EndScrollView();
        }

        private int CheckComponent<T>(string name) where T : Object
        {
            var obj = FindFirstObjectByType<T>();
            bool found = obj != null;
            DrawCheck(name, found);
            return found ? 0 : 1;
        }

        private int CheckHands()
        {
            var hands = FindObjectsByType<VRHand>(FindObjectsSortMode.None);
            int issues = 0;

            bool hasLeft = false, hasRight = false;
            foreach (var hand in hands)
            {
                if (hand.Side == HandSide.Left) hasLeft = true;
                if (hand.Side == HandSide.Right) hasRight = true;

                if (hand.FollowTarget == null)
                {
                    DrawIssue($"{hand.Side} hand missing FollowTarget");
                    issues++;
                }

                if (hand.PalmTransform == null)
                {
                    DrawIssue($"{hand.Side} hand missing PalmTransform");
                    issues++;
                }

                if (hand.GetComponent<GrabHandler>() == null)
                {
                    DrawIssue($"{hand.Side} hand missing GrabHandler");
                    issues++;
                }
            }

            DrawCheck("Left Hand", hasLeft);
            DrawCheck("Right Hand", hasRight);
            if (!hasLeft) issues++;
            if (!hasRight) issues++;

            return issues;
        }

        private int CheckTracking()
        {
            int issues = 0;
            var headDriver = FindFirstObjectByType<TrackedHeadDriver>();
            var controllerDrivers = FindObjectsByType<TrackedControllerDriver>(FindObjectsSortMode.None);

            DrawCheck("TrackedHeadDriver", headDriver != null);
            DrawCheck("TrackedControllerDrivers", controllerDrivers.Length >= 2);

            if (headDriver == null) issues++;
            if (controllerDrivers.Length < 2) issues++;
            return issues;
        }

        private int CheckPlayerBody()
        {
            var body = FindFirstObjectByType<VRPlayerBody>();
            DrawCheck("VRPlayerBody", body != null);

            if (body != null)
            {
                var lsm = FindFirstObjectByType<LocomotionStateMachine>();
                DrawCheck("LocomotionStateMachine", lsm != null);
                return lsm == null ? 1 : 0;
            }

            return 1;
        }

        private int CheckLayers()
        {
            int issues = 0;
            foreach (var (name, index) in VRCoreLayerSetup.RequiredLayers)
            {
                string existing = LayerMask.LayerToName(index);
                bool ok = existing == name;
                if (!ok) issues++;
            }
            DrawCheck("Physics Layers", issues == 0);
            return issues > 0 ? 1 : 0;
        }

        private int CheckPhysicsSettings()
        {
            bool linear = PlayerSettings.colorSpace == ColorSpace.Linear;
            DrawCheck("Color Space = Linear", linear);
            return linear ? 0 : 1;
        }

        private void DrawCheck(string label, bool ok)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(ok ? "\u2705" : "\u274C", GUILayout.Width(24));
            EditorGUILayout.LabelField(label);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawIssue(string message)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("  \u26A0", GUILayout.Width(30));
            EditorGUILayout.LabelField(message, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif
