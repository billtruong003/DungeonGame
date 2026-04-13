#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using BillVRCore.Hand;
using BillVRCore.Interaction;

namespace BillVRCore.Editor
{
    public class HandPoseWizard : EditorWindow
    {
        private VRHand targetHand;
        private FingerRig fingerRig;
        private Grabbable targetGrabbable;
        private WizardMode mode = WizardMode.SetupOpenClosed;

        private float masterBend;
        private float maxBendAngle = 90f;
        private float relaxPreview = 0.14f;
        private FingerSession[] sessions;
        private Vector2 scrollPos;
        private bool initialized;

        // Natural curl distribution per joint:
        // Knuckle(MCP) ~90°, Middle(PIP) ~100°, Distal(DIP) ~65°
        // Normalized: 0.9, 1.0, 0.65
        private static readonly float[] JointBendWeight = { 0.9f, 1.0f, 0.65f };

        // Pose save status
        private bool _openPoseSaved;
        private bool _closedPoseSaved;

        // Pose preview
        private HandPoseAsset previewAsset;
        private float previewCurl = 0.5f;

        private enum WizardMode { SetupOpenClosed, CreateGrabbablePose }

        [MenuItem("BillVR/Hand Pose Wizard", priority = 30)]
        public static void Open()
        {
            var window = GetWindow<HandPoseWizard>("Hand Pose Wizard");
            window.minSize = new Vector2(420, 600);
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            DrawHeader();
            DrawHandSelection();

            if (targetHand == null || fingerRig == null || !initialized)
            {
                EditorGUILayout.HelpBox("Assign a VRHand from the scene to begin.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            DrawModeToggle();
            DrawMasterControls();
            DrawFingerControls();
            DrawRelaxSection();
            DrawOperations();

            if (mode == WizardMode.CreateGrabbablePose)
                DrawGrabbableSection();

            EditorGUILayout.EndScrollView();

            if (GUI.changed && !Application.isPlaying)
                SceneView.RepaintAll();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(4);
            var style = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            EditorGUILayout.LabelField("BillVR Hand Pose Wizard", style);
            EditorGUILayout.Space(4);
            DrawSeparator();
        }

        private void DrawHandSelection()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Target Hand", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            targetHand = (VRHand)EditorGUILayout.ObjectField("Hand", targetHand, typeof(VRHand), true);
            if (EditorGUI.EndChangeCheck())
                OnHandChanged();

            if (targetHand != null)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Side", targetHand.Side.ToString());
                EditorGUILayout.LabelField("FingerRig", fingerRig != null ? fingerRig.name : "Not Found");
                EditorGUI.indentLevel--;
            }

            DrawSeparator();
        }

        private void DrawModeToggle()
        {
            EditorGUILayout.Space(2);
            mode = (WizardMode)GUILayout.Toolbar((int)mode, new[] { "Setup Open/Closed", "Grabbable Pose" });
            EditorGUILayout.Space(4);
        }

        private void DrawMasterControls()
        {
            EditorGUILayout.LabelField("Master Bend", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            masterBend = EditorGUILayout.Slider("Curl", masterBend, 0f, 1f);
            maxBendAngle = EditorGUILayout.Slider("Max Angle", maxBendAngle, 30f, 120f);
            if (EditorGUI.EndChangeCheck())
                ApplyMasterBend(masterBend);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Open (0)", GUILayout.Height(28)))
            {
                masterBend = 0f;
                ApplyMasterBend(0f);
            }
            if (GUILayout.Button("Half (0.5)", GUILayout.Height(28)))
            {
                masterBend = 0.5f;
                ApplyMasterBend(0.5f);
            }
            if (GUILayout.Button("Closed (1)", GUILayout.Height(28)))
            {
                masterBend = 1f;
                ApplyMasterBend(1f);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);
            GUI.backgroundColor = new Color(1f, 0.85f, 0.6f);
            if (GUILayout.Button("Flip All Bend Axes (if fingers bend wrong way)"))
            {
                if (sessions != null)
                {
                    foreach (var s in sessions)
                        s.bendAxis = -s.bendAxis;
                    ApplyMasterBend(masterBend);
                }
            }
            GUI.backgroundColor = Color.white;

            DrawSeparator();
        }

        private void DrawFingerControls()
        {
            if (sessions == null) return;

            EditorGUILayout.LabelField("Per-Finger Controls", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            foreach (var session in sessions)
            {
                if (session.chain == null) continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                var labelStyle = new GUIStyle(EditorStyles.boldLabel);
                labelStyle.normal.textColor = GetFingerColor(session.chain.type);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(session.chain.type.ToString(), labelStyle);
                var miniStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.gray } };
                EditorGUILayout.LabelField($"axis: ({session.bendAxis.x:F2}, {session.bendAxis.y:F2}, {session.bendAxis.z:F2})", miniStyle, GUILayout.Width(180));
                EditorGUILayout.EndHorizontal();

                EditorGUI.BeginChangeCheck();

                session.bend = EditorGUILayout.Slider("Bend", session.bend, 0f, 1f);

                bool showOffsets = EditorGUILayout.Foldout(session.showOffsets, "Joint Offsets");
                session.showOffsets = showOffsets;

                if (showOffsets && session.chain.joints != null)
                {
                    EditorGUI.indentLevel++;
                    for (int j = 0; j < session.chain.joints.Length && j < 3; j++)
                    {
                        string jointName = j == 0 ? "Knuckle" : (j == 1 ? "Middle" : "Distal");
                        session.jointOffsets[j] = EditorGUILayout.Slider(jointName, session.jointOffsets[j], -90f, 90f);
                    }
                    EditorGUI.indentLevel--;
                }

                if (EditorGUI.EndChangeCheck())
                    ApplyFingerSession(session);

                EditorGUILayout.EndVertical();
            }

            DrawSeparator();
        }

        private void DrawRelaxSection()
        {
            EditorGUILayout.LabelField("Relax Pose (Grip Offset)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("This offset is applied when the hand is idle. Higher = more curled at rest.", MessageType.None);

            EditorGUI.BeginChangeCheck();
            relaxPreview = EditorGUILayout.Slider("Grip Offset", relaxPreview, 0f, 0.5f);
            if (EditorGUI.EndChangeCheck())
                ApplyMasterBend(relaxPreview);

            if (GUILayout.Button("Save as Grip Offset", GUILayout.Height(26)))
            {
                var animator = targetHand.Animator;
                if (animator != null)
                {
                    Undo.RecordObject(animator, "Set Grip Offset");
                    animator.SetGripOffset(relaxPreview);
                    EditorUtility.SetDirty(animator);
                }
            }

            DrawSeparator();
        }

        private void DrawOperations()
        {
            EditorGUILayout.LabelField("Save Poses", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            if (mode == WizardMode.SetupOpenClosed)
            {
                // Status indicators
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                string openIcon = _openPoseSaved ? "\u2705" : "\u274C";
                string closedIcon = _closedPoseSaved ? "\u2705" : "\u274C";
                EditorGUILayout.LabelField($"{openIcon} Open Pose", GUILayout.Width(120));
                EditorGUILayout.LabelField($"{closedIcon} Closed Pose", GUILayout.Width(120));
                string status = (_openPoseSaved && _closedPoseSaved) ? "Ready!" : "Save both poses";
                var statusStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = (_openPoseSaved && _closedPoseSaved) ? new Color(0.2f, 0.8f, 0.3f) : new Color(1f, 0.6f, 0.2f) }
                };
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(status, statusStyle, GUILayout.Width(100));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.HelpBox(
                    "Step 1: Set Curl=0 (fingers relaxed/open) \u2192 Save OPEN\n" +
                    "Step 2: Set Curl=1 (fingers curled/fist) \u2192 Save CLOSED\n" +
                    "Saves what you SEE in the Scene view.",
                    MessageType.Info);

                EditorGUILayout.Space(2);

                EditorGUILayout.BeginHorizontal();

                GUI.backgroundColor = new Color(0.4f, 0.8f, 1f);
                if (GUILayout.Button("Save OPEN Pose", GUILayout.Height(36)))
                    SaveOpenPose();

                GUI.backgroundColor = new Color(1f, 0.5f, 0.2f);
                if (GUILayout.Button("Save CLOSED Pose", GUILayout.Height(36)))
                    SaveClosedPose();

                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);

                GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                if (GUILayout.Button("Reset to Rest", GUILayout.Height(28)))
                    ResetToRest();
                GUI.backgroundColor = Color.white;
            }

            DrawSeparator();

            // Preview saved poses section
            DrawPosePreview();
        }

        private void DrawPosePreview()
        {
            EditorGUILayout.LabelField("Preview Poses", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            // Quick preview buttons for open/closed
            if (_openPoseSaved || _closedPoseSaved)
            {
                EditorGUILayout.BeginHorizontal();
                if (_openPoseSaved && GUILayout.Button("Preview OPEN", GUILayout.Height(26)))
                    PreviewSavedPose(false);
                if (_closedPoseSaved && GUILayout.Button("Preview CLOSED", GUILayout.Height(26)))
                    PreviewSavedPose(true);
                if (GUILayout.Button("Back to Wizard", GUILayout.Height(26)))
                    ApplyMasterBend(masterBend);
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox("Save Open and Closed poses first to preview them here.", MessageType.Info);
            }

            EditorGUILayout.Space(4);

            // Preview from HandPoseAsset
            EditorGUI.BeginChangeCheck();
            previewAsset = (HandPoseAsset)EditorGUILayout.ObjectField("Preview Asset", previewAsset, typeof(HandPoseAsset), false);
            if (EditorGUI.EndChangeCheck() && previewAsset != null)
                PreviewFromAsset(previewAsset);

            if (previewAsset != null)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Apply to Hand", GUILayout.Height(26)))
                    PreviewFromAsset(previewAsset);

                // Show curl values from asset
                EditorGUILayout.BeginVertical();
                var miniLabel = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.gray } };
                EditorGUILayout.LabelField(
                    $"T:{previewAsset.thumbCurl:F1} I:{previewAsset.indexCurl:F1} M:{previewAsset.middleCurl:F1} R:{previewAsset.ringCurl:F1} P:{previewAsset.pinkyCurl:F1}",
                    miniLabel);
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            }

            // List all pose assets in project for quick access
            EditorGUILayout.Space(4);
            if (GUILayout.Button("Load Preset Pose..."))
                ShowPoseAssetPicker();

            DrawSeparator();
        }

        private void PreviewSavedPose(bool closed)
        {
            if (fingerRig == null) return;

            foreach (var chain in fingerRig.Fingers)
            {
                if (chain.joints == null) continue;
                var rotations = closed ? chain.closedLocalRotations : chain.openLocalRotations;
                if (rotations == null) continue;

                for (int j = 0; j < chain.joints.Length && j < rotations.Length; j++)
                    if (chain.joints[j] != null)
                        chain.joints[j].localRotation = rotations[j];
            }

            SceneView.RepaintAll();
        }

        private void PreviewFromAsset(HandPoseAsset asset)
        {
            if (fingerRig == null || asset == null) return;

            fingerRig.SetFromPoseData(asset);

            // Apply immediately without smooth interpolation
            foreach (var chain in fingerRig.Fingers)
            {
                if (chain.joints == null) continue;
                int idx = (int)chain.type;
                if (idx < 0 || idx >= 5) continue;

                float curl = asset.GetCurl(chain.type);
                if (chain.openLocalRotations == null || chain.closedLocalRotations == null) continue;

                for (int i = 0; i < chain.joints.Length; i++)
                {
                    if (chain.joints[i] == null) continue;
                    if (i >= chain.openLocalRotations.Length || i >= chain.closedLocalRotations.Length) continue;

                    chain.joints[i].localRotation = Quaternion.Slerp(
                        chain.openLocalRotations[i],
                        chain.closedLocalRotations[i],
                        curl);
                }
            }

            SceneView.RepaintAll();
        }

        private void ShowPoseAssetPicker()
        {
            string[] guids = AssetDatabase.FindAssets("t:HandPoseAsset");

            if (guids.Length == 0)
            {
                EditorUtility.DisplayDialog("No Pose Assets", "No HandPoseAsset found in project.\nUse BillVR > Create Default Configs to generate presets.", "OK");
                return;
            }

            var menu = new GenericMenu();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                menu.AddItem(new GUIContent(name), false, () =>
                {
                    previewAsset = AssetDatabase.LoadAssetAtPath<HandPoseAsset>(path);
                    if (previewAsset != null)
                        PreviewFromAsset(previewAsset);
                    Repaint();
                });
            }
            menu.ShowAsContext();
        }

        private void DrawGrabbableSection()
        {
            EditorGUILayout.LabelField("Grabbable Target", EditorStyles.boldLabel);
            targetGrabbable = (Grabbable)EditorGUILayout.ObjectField("Grabbable", targetGrabbable, typeof(Grabbable), true);

            if (targetGrabbable == null)
            {
                EditorGUILayout.HelpBox("Assign a Grabbable to save a custom grip pose for it.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
            if (GUILayout.Button("Save Pose to HandPoseAsset", GUILayout.Height(36)))
                SaveToAsset();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(2);
            if (GUILayout.Button("Save Curl Values to SnapGrabbable", GUILayout.Height(28)))
                SaveToSnapGrabbable();
        }

        private void OnHandChanged()
        {
            initialized = false;
            sessions = null;
            fingerRig = null;

            if (targetHand == null) return;

            var animator = targetHand.Animator;
            fingerRig = animator != null ? animator.GetFingerRig() : targetHand.GetComponentInChildren<FingerRig>();

            if (fingerRig == null) return;

            if (!Application.isPlaying)
                fingerRig.Initialize();

            BuildSessions();
            relaxPreview = animator != null ? animator.GripOffset : 0.14f;

            // Detect if poses were already saved
            _openPoseSaved = false;
            _closedPoseSaved = false;
            foreach (var chain in fingerRig.Fingers)
            {
                if (chain.openLocalRotations != null && chain.openLocalRotations.Length > 0)
                    _openPoseSaved = true;
                if (chain.closedLocalRotations != null && chain.closedLocalRotations.Length > 0)
                    _closedPoseSaved = true;
                break;
            }

            initialized = true;
        }

        private void BuildSessions()
        {
            var chains = fingerRig.Fingers;
            sessions = new FingerSession[chains.Length];

            for (int i = 0; i < chains.Length; i++)
            {
                sessions[i] = new FingerSession
                {
                    chain = chains[i],
                    bend = 0f,
                    jointOffsets = new float[3],
                    showOffsets = false,
                    bendAxis = DetectBendAxis(chains[i])
                };

                if (chains[i].joints != null)
                {
                    sessions[i].restRotations = new Quaternion[chains[i].joints.Length];
                    for (int j = 0; j < chains[i].joints.Length; j++)
                        if (chains[i].joints[j] != null)
                            sessions[i].restRotations[j] = chains[i].joints[j].localRotation;
                }
            }
        }

        private Vector3 DetectBendAxis(FingerRig.FingerChain chain)
        {
            if (chain.joints == null || chain.joints.Length < 2)
                return Vector3.right;

            Transform j0 = chain.joints[0];
            Transform j1 = chain.joints[1];
            if (j0 == null || j1 == null)
                return Vector3.right;

            // Palm direction: points INTO the palm (the direction fingers curl toward)
            Vector3 palmInward = GetPalmInwardDirection();

            Vector3 fingerDir = (j1.position - j0.position).normalized;

            // Bend axis = perpendicular to both finger direction and palm inward.
            // Positive rotation around this axis should curl the finger toward the palm.
            Vector3 bendWorld = Vector3.Cross(palmInward, fingerDir).normalized;

            if (bendWorld.sqrMagnitude < 0.001f)
            {
                // Finger is parallel to palm direction (shouldn't happen, but fallback)
                Vector3 handRight = targetHand != null ? targetHand.transform.right : Vector3.right;
                bendWorld = Vector3.Cross(handRight, fingerDir).normalized;
            }

            // Verify direction: a positive rotation should move the finger tip toward palm.
            // Test by simulating a small rotation and checking if the tip gets closer to palm.
            Vector3 tipPos = chain.joints[^1] != null ? chain.joints[^1].position : j1.position;
            Vector3 palmPos = targetHand != null && targetHand.PalmTransform != null
                ? targetHand.PalmTransform.position
                : (targetHand != null ? targetHand.transform.position : j0.position);

            Vector3 rotatedTip = Quaternion.AngleAxis(5f, bendWorld) * (tipPos - j0.position) + j0.position;
            float origDist = Vector3.Distance(tipPos, palmPos);
            float rotatedDist = Vector3.Distance(rotatedTip, palmPos);

            // If rotating +5 degrees moves the tip AWAY from palm, flip the axis
            if (rotatedDist > origDist)
                bendWorld = -bendWorld;

            return j0.InverseTransformDirection(bendWorld).normalized;
        }

        private Vector3 GetPalmInwardDirection()
        {
            if (targetHand != null && targetHand.PalmTransform != null)
            {
                // Palm transform after Euler(90,0,0): .forward points INTO the palm.
                return targetHand.PalmTransform.forward;
            }

            if (targetHand != null)
                return -targetHand.transform.up;

            return Vector3.down;
        }

        private void ApplyMasterBend(float value)
        {
            if (sessions == null) return;

            Undo.RecordObjects(GetAllJointTransforms(), "Master Bend");

            foreach (var session in sessions)
            {
                session.bend = value;
                ApplyFingerSession(session);
            }
        }

        private void ApplyFingerSession(FingerSession session)
        {
            if (session.chain?.joints == null || session.restRotations == null) return;

            for (int j = 0; j < session.chain.joints.Length && j < 3; j++)
            {
                if (session.chain.joints[j] == null || j >= session.restRotations.Length) continue;

                // Each joint gets a different share of the total bend
                float weight = j < JointBendWeight.Length ? JointBendWeight[j] : 0.5f;
                float angle = Mathf.Lerp(0f, maxBendAngle * weight, session.bend);
                float totalAngle = angle + session.jointOffsets[j];

                session.chain.joints[j].localRotation = session.restRotations[j] * Quaternion.AngleAxis(totalAngle, session.bendAxis);
            }
        }

        private void SaveOpenPose()
        {
            if (fingerRig == null) return;

            // Save the CURRENT bone state as open pose — whatever the user sees right now.
            // User should position fingers to OPEN (relaxed) state before clicking this.
            Undo.RecordObject(fingerRig, "Save Open Pose");
            fingerRig.SaveOpenPose();
            EditorUtility.SetDirty(fingerRig);

            _openPoseSaved = true;
            Debug.Log("<color=cyan>[BillVR] Saved OPEN Pose (current bone state)</color>");
        }

        private void SaveClosedPose()
        {
            if (fingerRig == null) return;

            // Save the CURRENT bone state as closed pose — whatever the user sees right now.
            // User should position fingers to CLOSED (fist) state before clicking this.
            Undo.RecordObject(fingerRig, "Save Closed Pose");
            fingerRig.SaveClosedPose();
            EditorUtility.SetDirty(fingerRig);

            _closedPoseSaved = true;
            Debug.Log("<color=orange>[BillVR] Saved CLOSED Pose (current bone state)</color>");
        }

        private void ResetToRest()
        {
            if (sessions == null) return;

            Undo.RecordObjects(GetAllJointTransforms(), "Reset Pose");

            foreach (var session in sessions)
            {
                session.bend = 0f;
                session.jointOffsets = new float[3];

                if (session.chain?.joints != null && session.restRotations != null)
                    for (int j = 0; j < session.chain.joints.Length; j++)
                        if (session.chain.joints[j] != null && j < session.restRotations.Length)
                            session.chain.joints[j].localRotation = session.restRotations[j];
            }

            masterBend = 0f;
        }

        private void SaveToAsset()
        {
            if (targetHand == null || targetGrabbable == null) return;

            string path = EditorUtility.SaveFilePanelInProject(
                "Save Hand Pose Asset", "NewHandPose", "asset", "Choose save location");

            if (string.IsNullOrEmpty(path)) return;

            var asset = CreateInstance<HandPoseAsset>();
            asset.SavePose(targetHand, targetGrabbable.transform);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;

            Debug.Log($"<color=green>[BillVR] Saved HandPoseAsset: {path}</color>");
        }

        private void SaveToSnapGrabbable()
        {
            if (targetGrabbable == null || sessions == null) return;

            var snap = targetGrabbable as SnapGrabbable;
            if (snap == null)
            {
                Debug.LogWarning("[BillVR] Target is not a SnapGrabbable");
                return;
            }

            Undo.RecordObject(snap, "Save Curl to SnapGrabbable");
            EditorUtility.SetDirty(snap);

            Debug.Log($"<color=green>[BillVR] Saved curl values to SnapGrabbable: {snap.name}</color>");
        }

        private Transform[] GetAllJointTransforms()
        {
            var list = new List<Transform>();
            if (fingerRig == null) return list.ToArray();

            foreach (var chain in fingerRig.Fingers)
                if (chain.joints != null)
                    foreach (var j in chain.joints)
                        if (j != null) list.Add(j);

            return list.ToArray();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (sessions == null || fingerRig == null) return;

            foreach (var session in sessions)
            {
                if (session.chain?.joints == null) continue;

                Color col = GetFingerColor(session.chain.type);

                foreach (var joint in session.chain.joints)
                {
                    if (joint == null) continue;

                    Handles.color = col;
                    Vector3 worldAxis = joint.TransformDirection(session.bendAxis);
                    Handles.DrawLine(joint.position, joint.position + worldAxis * 0.015f, 2f);

                    Handles.color = new Color(col.r, col.g, col.b, 0.3f);
                    Handles.SphereHandleCap(0, joint.position, Quaternion.identity, 0.005f, EventType.Repaint);
                }

                if (session.chain.tip != null)
                {
                    Handles.color = Color.white;
                    Handles.SphereHandleCap(0, session.chain.tip.position, Quaternion.identity, session.chain.tipRadius * 2f, EventType.Repaint);
                }
            }
        }

        private Color GetFingerColor(FingerType type)
        {
            return type switch
            {
                FingerType.Thumb => new Color(1f, 0.4f, 0.4f),
                FingerType.Index => new Color(0.4f, 0.8f, 1f),
                FingerType.Middle => new Color(0.4f, 1f, 0.5f),
                FingerType.Ring => new Color(1f, 0.8f, 0.3f),
                FingerType.Pinky => new Color(0.8f, 0.4f, 1f),
                _ => Color.white
            };
        }

        private void DrawSeparator()
        {
            EditorGUILayout.Space(2);
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.3f));
            EditorGUILayout.Space(2);
        }

        private class FingerSession
        {
            public FingerRig.FingerChain chain;
            public float bend;
            public float[] jointOffsets;
            public Quaternion[] restRotations;
            public Vector3 bendAxis;
            public bool showOffsets;
        }
    }
}
#endif
