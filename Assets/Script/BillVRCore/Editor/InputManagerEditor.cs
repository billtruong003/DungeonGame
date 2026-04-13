#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using BillVRCore.Input;

namespace BillVRCore.Editor
{
    [CustomEditor(typeof(InputManager))]
    public class InputManagerEditor : UnityEditor.Editor
    {
        private bool _showLiveInput = true;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (!Application.isPlaying) return;

            var manager = (InputManager)target;
            if (manager.Input == null) return;

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Runtime State", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Current Mode", manager.CurrentMode.ToString());

            EditorGUILayout.Space(4);
            DrawModeButtons(manager);

            _showLiveInput = EditorGUILayout.Foldout(_showLiveInput, "Live Input Values");
            if (_showLiveInput)
                DrawLiveInput(manager);

            Repaint();
        }

        private void DrawModeButtons(InputManager manager)
        {
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = manager.CurrentMode == InputMode.LegacyController
                ? Color.green : Color.white;
            if (GUILayout.Button("Legacy"))
                manager.SwitchMode(InputMode.LegacyController);

            GUI.backgroundColor = manager.CurrentMode == InputMode.Desktop
                ? Color.green : Color.white;
            if (GUILayout.Button("Desktop"))
                manager.SwitchMode(InputMode.Desktop);

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLiveInput(InputManager manager)
        {
            EditorGUI.indentLevel++;
            var input = manager.Input;

            EditorGUILayout.LabelField("Left Hand", EditorStyles.boldLabel);
            DrawHandInput(input, manager, HandSide.Left);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Right Hand", EditorStyles.boldLabel);
            DrawHandInput(input, manager, HandSide.Right);

            EditorGUI.indentLevel--;
        }

        private void DrawHandInput(IVRInput input, InputManager manager, HandSide side)
        {
            EditorGUI.indentLevel++;

            bool connected = input.IsConnected(side);
            EditorGUILayout.LabelField("Connected", connected ? "Yes" : "No");

            if (connected)
            {
                DrawProgressBar("Grip", input.GripStrength(side));
                DrawProgressBar("Trigger", input.TriggerStrength(side));

                Vector2 axis = input.JoystickAxis(side);
                EditorGUILayout.LabelField("Joystick", $"({axis.x:F2}, {axis.y:F2})");

                EditorGUILayout.LabelField("Grab", input.GrabHeld(side) ? "HELD" : "released");
                EditorGUILayout.LabelField("Trigger", input.TriggerHeld(side) ? "HELD" : "released");

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Fingers:");
                EditorGUI.indentLevel++;
                for (int i = 0; i < 5; i++)
                {
                    var finger = (FingerType)i;
                    DrawProgressBar(finger.ToString(), manager.GetFingerCurl(side, finger));
                }
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
        }

        private void DrawProgressBar(string label, float value)
        {
            Rect rect = GUILayoutUtility.GetRect(18, 18, "TextField");
            EditorGUI.ProgressBar(rect, value, $"{label}: {value:F2}");
        }
    }
}
#endif
