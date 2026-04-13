#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using BillVRCore.Hand;

namespace BillVRCore.Editor
{
    public class HandPoseBaker : EditorWindow
    {
        private FingerRig _targetRig;
        private HandPoseAsset _currentPose;
        private string _poseName = "NewPose";
        private Vector2 _scroll;

        [MenuItem("BillVR/Hand Pose Baker", priority = 22)]
        public static void ShowWindow()
        {
            var window = GetWindow<HandPoseBaker>("Hand Pose Baker");
            window.minSize = new Vector2(350, 500);
            window.Show();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Hand Pose Baker", EditorStyles.boldLabel);
            EditorGUILayout.Space(8);

            _targetRig = (FingerRig)EditorGUILayout.ObjectField("Target Finger Rig", _targetRig,
                typeof(FingerRig), true);

            EditorGUILayout.Space(4);

            if (_targetRig == null)
            {
                EditorGUILayout.HelpBox("Assign a FingerRig to bake poses.", MessageType.Info);
                DrawAutoDetectSection();
                EditorGUILayout.EndScrollView();
                return;
            }

            DrawPoseSaveSection();
            EditorGUILayout.Space(8);
            DrawPoseLoadSection();
            EditorGUILayout.Space(8);
            DrawFingerPreview();
            EditorGUILayout.Space(8);
            DrawAutoDetectSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawPoseSaveSection()
        {
            EditorGUILayout.LabelField("Save Pose", EditorStyles.boldLabel);

            _poseName = EditorGUILayout.TextField("Pose Name", _poseName);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Save Open Pose"))
            {
                _targetRig.SaveOpenPose();
                Debug.Log("[BillVR] Open pose saved to FingerRig.");
            }

            if (GUILayout.Button("Save Closed Pose"))
            {
                _targetRig.SaveClosedPose();
                Debug.Log("[BillVR] Closed pose saved to FingerRig.");
            }

            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Bake Current Pose to Asset"))
                BakeCurrentPose();
        }

        private void DrawPoseLoadSection()
        {
            EditorGUILayout.LabelField("Load Pose", EditorStyles.boldLabel);

            _currentPose = (HandPoseAsset)EditorGUILayout.ObjectField("Pose Asset", _currentPose,
                typeof(HandPoseAsset), false);

            if (_currentPose != null && GUILayout.Button("Apply Pose to Rig"))
            {
                _targetRig.SetFromPoseData(_currentPose);
                _targetRig.UpdateFingers(1f);
                SceneView.RepaintAll();
            }
        }

        private void DrawFingerPreview()
        {
            EditorGUILayout.LabelField("Finger Preview (live)", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see live finger curl values.", MessageType.Info);
                return;
            }

            foreach (var finger in _targetRig.Fingers)
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.Slider(finger.type.ToString(), finger.currentCurl, 0f, 1f);
                EditorGUI.EndDisabledGroup();
            }
        }

        private void DrawAutoDetectSection()
        {
            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Auto-Detect Fingers", EditorStyles.boldLabel);

            if (Selection.activeGameObject == null)
            {
                EditorGUILayout.HelpBox("Select a hand root object in the hierarchy to auto-detect finger bones.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"Selected: {Selection.activeGameObject.name}");

            if (GUILayout.Button("Detect Finger Bones"))
            {
                var results = HandBoneDetector.DetectFingers(Selection.activeGameObject.transform);

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Detected {results.Length} fingers:");
                foreach (var finger in results)
                {
                    sb.AppendLine($"  {finger.type}: {finger.joints.Length} joints " +
                        $"({string.Join(" > ", System.Linq.Enumerable.Select(finger.joints, j => j.name))})");
                }
                Debug.Log(sb.ToString());
            }

            if (GUILayout.Button("Detect + Create FingerRig Component"))
            {
                CreateFingerRigFromDetection(Selection.activeGameObject.transform);
            }
        }

        private void BakeCurrentPose()
        {
            var pose = ScriptableObject.CreateInstance<HandPoseAsset>();

            foreach (var finger in _targetRig.Fingers)
                pose.SetCurl(finger.type, finger.currentCurl);

            string path = EditorUtility.SaveFilePanelInProject(
                "Save Hand Pose", _poseName, "asset", "Choose location for hand pose asset");

            if (string.IsNullOrEmpty(path)) return;

            AssetDatabase.CreateAsset(pose, path);
            AssetDatabase.SaveAssets();
            _currentPose = pose;
            Debug.Log($"[BillVR] Hand pose saved to {path}");
        }

        private void CreateFingerRigFromDetection(Transform handRoot)
        {
            var detected = HandBoneDetector.DetectFingers(handRoot);
            if (detected.Length == 0)
            {
                Debug.LogWarning("[BillVR] No fingers detected.");
                return;
            }

            var rig = handRoot.GetComponent<FingerRig>();
            if (rig == null)
                rig = Undo.AddComponent<FingerRig>(handRoot.gameObject);

            var fingers = new FingerRig.FingerChain[5];
            for (int i = 0; i < 5; i++)
            {
                fingers[i] = new FingerRig.FingerChain
                {
                    type = (FingerType)i,
                    joints = new Transform[0]
                };
            }

            foreach (var detected_finger in detected)
            {
                int idx = (int)detected_finger.type;
                if (idx < 0 || idx >= 5) continue;

                fingers[idx] = new FingerRig.FingerChain
                {
                    type = detected_finger.type,
                    joints = detected_finger.joints,
                    tip = detected_finger.tip,
                    tipRadius = 0.008f
                };
            }

            var field = typeof(FingerRig).GetField("fingers",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(rig, fingers);
                EditorUtility.SetDirty(rig);
            }

            _targetRig = rig;
            Debug.Log($"[BillVR] FingerRig created with {detected.Length} detected finger chains.");
        }
    }
}
#endif
