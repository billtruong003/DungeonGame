#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace BillVRCore.Editor
{
    public class BillVRSetupWizard : EditorWindow
    {
        private BillVRPackageValidator.PackageCheckResult _packageResult;
        private bool _packagesChecked;
        private bool _checkingPackages;
        private Vector2 _scroll;
        private int _validationIssues = -1;

        [MenuItem("BillVR/Setup Wizard", priority = 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<BillVRSetupWizard>("BillVR Setup Wizard");
            window.minSize = new Vector2(480, 700);
            window.Show();
        }

        [MenuItem("BillVR/Create Player Rig", priority = 20)]
        public static void MenuCreateRig() => BillVRSceneBuilder.BuildPlayerRig();

        [MenuItem("BillVR/Create Test Grabbables", priority = 21)]
        public static void MenuCreateGrabbables() => BillVRSceneBuilder.CreateTestGrabbables();

        [MenuItem("BillVR/Add Desktop Simulator", priority = 22)]
        public static void MenuAddDesktopSimulator()
        {
            var existing = Object.FindFirstObjectByType<DebugTools.DesktopVRSimulator>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                Debug.Log("[BillVR] Desktop Simulator already exists in scene.");
                return;
            }

            // Prefer attaching to player root
            var bootstrap = Object.FindFirstObjectByType<BillVRBootstrap>();
            if (bootstrap != null)
            {
                Undo.AddComponent<DebugTools.DesktopVRSimulator>(bootstrap.gameObject);
                Selection.activeGameObject = bootstrap.gameObject;

                // Also switch default input mode to Desktop
                var so = new SerializedObject(bootstrap);
                var modeProp = so.FindProperty("defaultInputMode");
                if (modeProp != null)
                {
                    modeProp.enumValueIndex = (int)InputMode.Desktop;
                    so.ApplyModifiedProperties();
                }
            }
            else
            {
                var go = new GameObject("[BillVR] DesktopSimulator");
                Undo.RegisterCreatedObjectUndo(go, "Add Desktop Simulator");
                go.AddComponent<DebugTools.DesktopVRSimulator>();
                Selection.activeGameObject = go;
            }

            Debug.Log("[BillVR] Desktop Simulator added. Press Play to test with keyboard/mouse.");
        }

        [MenuItem("BillVR/Spawn Grab Test Kit", priority = 23)]
        public static void MenuSpawnGrabTestKit()
        {
            var existing = Object.FindFirstObjectByType<DebugTools.VRGrabTestKit>();
            if (existing != null)
            {
                existing.SpawnTestObjects();
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            var go = new GameObject("[BillVR] GrabTestKit");
            Undo.RegisterCreatedObjectUndo(go, "Spawn Grab Test Kit");
            go.AddComponent<DebugTools.VRGrabTestKit>();
            Selection.activeGameObject = go;
            Debug.Log("[BillVR] Grab Test Kit added. Enter Play mode to spawn test objects.");
        }

        [MenuItem("BillVR/Setup Layers + Collision Matrix", priority = 40)]
        public static void MenuSetupLayers()
        {
            BillVRLayerSetup.CreateAllLayers();
            BillVRLayerSetup.ConfigureCollisionMatrix();
        }

        [MenuItem("BillVR/Detect Ragdoll Animator 2", priority = 41)]
        public static void MenuDetectRagdoll()
        {
            bool found = BillVRPackageValidator.DetectRagdollAnimator(out string asmName);
            BillVRPackageValidator.ApplyRagdollIntegration(found, asmName);
            Debug.Log(found
                ? $"[BillVR] Ragdoll Animator 2 detected in assembly '{asmName}'."
                : "[BillVR] Ragdoll Animator 2 not found.");
        }

        private void OnEnable()
        {
            RefreshPackageCheck();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawHeader();
            EditorGUILayout.Space(6);
            DrawOverallStatus();
            EditorGUILayout.Space(10);

            DrawStep1_Packages();
            EditorGUILayout.Space(6);
            DrawStep2_ProjectSettings();
            EditorGUILayout.Space(6);
            DrawStep3_Layers();
            EditorGUILayout.Space(6);
            DrawStep4_Ragdoll();
            EditorGUILayout.Space(6);
            DrawStep5_SceneSetup();
            EditorGUILayout.Space(6);
            DrawStep6_DefaultAssets();
            EditorGUILayout.Space(6);
            DrawStep7_Performance();
            EditorGUILayout.Space(6);
            DrawStep8_Validation();
            EditorGUILayout.Space(20);
            DrawRunAll();

            EditorGUILayout.EndScrollView();
        }

        // ─────────────────────────────────────────────
        //  Header & Summary
        // ─────────────────────────────────────────────

        private void DrawHeader()
        {
            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("BillVR Setup Wizard", titleStyle);
            EditorGUILayout.Space(2);

            var subtitleStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { wordWrap = true };
            EditorGUILayout.LabelField(
                "VR interaction framework for Unity 6 + OpenXR. Follow the steps below or click Run All.",
                subtitleStyle);

            DrawSeparator();
        }

        private void DrawOverallStatus()
        {
            bool packagesOk = _packagesChecked && _packageResult.allRequiredInstalled;
            bool settingsOk = IsBothInputMode() && PlayerSettings.colorSpace == ColorSpace.Linear;
            bool layersOk = BillVRLayerSetup.AllLayersExist();
            bool rigOk = Object.FindFirstObjectByType<BillVRBootstrap>() != null;

            int ready = 0;
            int total = 4;
            if (packagesOk) ready++;
            if (settingsOk) ready++;
            if (layersOk) ready++;
            if (rigOk) ready++;

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.FlexibleSpace();

            string status = ready == total
                ? "Ready to build!"
                : $"{ready}/{total} core checks passed";

            var statusStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ready == total ? new Color(0.2f, 0.8f, 0.3f) : new Color(1f, 0.7f, 0.2f) }
            };
            EditorGUILayout.LabelField(status, statusStyle);

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        // ─────────────────────────────────────────────
        //  Step 1: Packages
        // ─────────────────────────────────────────────

        private void DrawStep1_Packages()
        {
            EditorGUILayout.LabelField("Step 1 — Package Validation", EditorStyles.boldLabel);

            if (_checkingPackages)
            {
                EditorGUILayout.HelpBox("Checking packages...", MessageType.Info);
                return;
            }

            if (!_packagesChecked)
            {
                if (GUILayout.Button("Check Packages"))
                    RefreshPackageCheck();
                return;
            }

            foreach (var status in _packageResult.statuses)
            {
                string icon = status.installed ? "\u2705" : (status.requirement.optional ? "\u26A0" : "\u274C");
                string version = status.installed ? $" ({status.version})" : "";
                string label = $"{icon} {status.requirement.displayName}{version}";

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(label);

                if (!status.installed && !status.requirement.optional)
                {
                    if (GUILayout.Button("Install", GUILayout.Width(60)))
                    {
                        BillVRPackageValidator.InstallPackage(status.requirement.id);
                        RefreshPackageCheck();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            if (_packageResult.allRequiredInstalled)
                EditorGUILayout.HelpBox("All required packages installed.", MessageType.Info);
            else
                EditorGUILayout.HelpBox("Some required packages are missing. Install them before continuing.", MessageType.Warning);
        }

        // ─────────────────────────────────────────────
        //  Step 2: Project Settings
        // ─────────────────────────────────────────────

        private void DrawStep2_ProjectSettings()
        {
            EditorGUILayout.LabelField("Step 2 — Project Settings", EditorStyles.boldLabel);

            bool bothInput = IsBothInputMode();
            bool linearColor = PlayerSettings.colorSpace == ColorSpace.Linear;

            DrawStatusLine("Active Input Handling = Both", bothInput);
            DrawStatusLine("Color Space = Linear", linearColor);

            if (!bothInput || !linearColor)
            {
                if (GUILayout.Button("Fix Project Settings"))
                    ApplyProjectSettings();
            }
            else
            {
                EditorGUILayout.HelpBox("Project settings are correct.", MessageType.Info);
            }
        }

        // ─────────────────────────────────────────────
        //  Step 3: Physics Layers
        // ─────────────────────────────────────────────

        private void DrawStep3_Layers()
        {
            EditorGUILayout.LabelField("Step 3 — Physics Layers", EditorStyles.boldLabel);

            bool allExist = BillVRLayerSetup.AllLayersExist();
            DrawStatusLine("All BillVR layers configured", allExist);

            if (!allExist)
            {
                EditorGUILayout.HelpBox(
                    "Required layers: PlayerBody(8), RagdollDummy(9), Grabbable(10), " +
                    "HandPhysics(11), InventorySlot(12), GroundCheck(13), BodyIK(14)",
                    MessageType.None);

                if (GUILayout.Button("Create Layers + Configure Collision Matrix"))
                {
                    BillVRLayerSetup.CreateAllLayers();
                    BillVRLayerSetup.ConfigureCollisionMatrix();
                    Debug.Log("[BillVR] Layers and collision matrix configured.");
                }
            }
            else
            {
                EditorGUILayout.HelpBox("7 physics layers + collision matrix configured.", MessageType.Info);
            }
        }

        // ─────────────────────────────────────────────
        //  Step 4: Ragdoll Animator 2
        // ─────────────────────────────────────────────

        private void DrawStep4_Ragdoll()
        {
            EditorGUILayout.LabelField("Step 4 — Ragdoll Animator 2 (Optional)", EditorStyles.boldLabel);

            bool detected = _packagesChecked
                ? _packageResult.ragdollDetected
                : BillVRPackageValidator.DetectRagdollAnimator(out _);

            DrawStatusLine("Ragdoll Animator 2", detected);

            if (detected)
                EditorGUILayout.HelpBox("RA2 detected. Full combat physics enabled.", MessageType.Info);
            else
                EditorGUILayout.HelpBox("Not found. Basic fallback ragdoll will be used. Install RA2 from Asset Store for advanced combat physics.", MessageType.Warning);

            if (GUILayout.Button("Refresh Detection"))
            {
                bool found = BillVRPackageValidator.DetectRagdollAnimator(out string asmName);
                BillVRPackageValidator.ApplyRagdollIntegration(found, asmName);
                if (_packagesChecked) _packageResult.ragdollDetected = found;
                Repaint();
            }
        }

        // ─────────────────────────────────────────────
        //  Step 5: Scene Setup
        // ─────────────────────────────────────────────

        private void DrawStep5_SceneSetup()
        {
            EditorGUILayout.LabelField("Step 5 — Player Rig", EditorStyles.boldLabel);

            bool hasRig = Object.FindFirstObjectByType<BillVRBootstrap>() != null;
            DrawStatusLine("Player Rig in Scene", hasRig);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(hasRig ? "Rebuild Player Rig" : "Create Player Rig"))
            {
                if (hasRig)
                {
                    var existing = Object.FindFirstObjectByType<BillVRBootstrap>();
                    if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);
                }
                BillVRSceneBuilder.BuildPlayerRig();
                BillVRSceneBuilder.CreateFloor();
            }

            if (GUILayout.Button("Add Test Grabbables"))
                BillVRSceneBuilder.CreateTestGrabbables();

            if (GUILayout.Button("Add Diverse Objects"))
                BillVRSceneBuilder.CreateDiverseTestObjects();

            EditorGUILayout.EndHorizontal();

            if (hasRig)
                EditorGUILayout.HelpBox("Player rig exists. Includes: VRHands, Locomotion, Tracking, Debug Overlay.", MessageType.Info);
        }

        // ─────────────────────────────────────────────
        //  Step 6: Default Assets
        // ─────────────────────────────────────────────

        private void DrawStep6_DefaultAssets()
        {
            EditorGUILayout.LabelField("Step 6 — Default Config Assets", EditorStyles.boldLabel);

            bool hasConfigs = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/VRCore/Data/DefaultFingerMapping.asset") != null;
            bool hasPoses = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/VRCore/Data/Poses/OpenHand.asset") != null;
            bool hasItems = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/VRCore/Data/Items/Weapon_Pistol.asset") != null;

            DrawStatusLine("Finger Mapping Config", hasConfigs);
            DrawStatusLine("Hand Poses (8 presets)", hasPoses);
            DrawStatusLine("Item Data (5 presets)", hasItems);

            if (!hasConfigs || !hasPoses || !hasItems)
            {
                if (GUILayout.Button("Create Default Configs"))
                {
                    BillVRAssetCreator.CreateAllDefaults();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("All default config assets exist in Assets/VRCore/Data/.", MessageType.Info);
            }
        }

        // ─────────────────────────────────────────────
        //  Step 7: Performance Settings
        // ─────────────────────────────────────────────

        private void DrawStep7_Performance()
        {
            EditorGUILayout.LabelField("Step 7 — VR Performance Settings", EditorStyles.boldLabel);

            bool correctTimestep = Mathf.Approximately(Time.fixedDeltaTime, 0.01111f);
            bool correctVSync = QualitySettings.vSyncCount == 0;

            DrawStatusLine("Physics timestep = 90Hz (0.01111s)", correctTimestep);
            DrawStatusLine("VSync = Off (runtime managed)", correctVSync);

            if (!correctTimestep || !correctVSync)
            {
                if (GUILayout.Button("Apply VR Performance Settings"))
                {
                    BillVRSceneBuilder.ApplyVRPerformanceSettings();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Performance settings optimized for VR.", MessageType.Info);
            }
        }

        // ─────────────────────────────────────────────
        //  Step 8: Validate Scene
        // ─────────────────────────────────────────────

        private void DrawStep8_Validation()
        {
            EditorGUILayout.LabelField("Step 8 — Scene Validation", EditorStyles.boldLabel);

            if (_validationIssues >= 0)
            {
                if (_validationIssues == 0)
                    EditorGUILayout.HelpBox("Scene validation passed. Ready to Play!", MessageType.Info);
                else
                    EditorGUILayout.HelpBox($"{_validationIssues} issue(s) found. Open Validator for details.", MessageType.Warning);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Run Validation"))
            {
                _validationIssues = CountValidationIssues();
                Repaint();
            }
            if (GUILayout.Button("Open Validator Window"))
            {
                BillVRSceneValidator.ShowWindow();
            }
            EditorGUILayout.EndHorizontal();
        }

        // ─────────────────────────────────────────────
        //  Run All
        // ─────────────────────────────────────────────

        private void DrawRunAll()
        {
            DrawSeparator();

            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
            if (GUILayout.Button("Run All Steps", GUILayout.Height(40)))
                RunAllSteps();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(4);

            GUI.backgroundColor = new Color(0.4f, 0.6f, 0.9f);
            if (GUILayout.Button("Run All (Skip Scene Build)", GUILayout.Height(28)))
                RunAllSteps(skipScene: true);
            GUI.backgroundColor = Color.white;
        }

        private void RunAllSteps(bool skipScene = false)
        {
            // Step 2: Project settings
            ApplyProjectSettings();

            // Step 3: Layers
            BillVRLayerSetup.CreateAllLayers();
            BillVRLayerSetup.ConfigureCollisionMatrix();

            // Step 4: Ragdoll detection
            bool ragdoll = BillVRPackageValidator.DetectRagdollAnimator(out string asmName);
            BillVRPackageValidator.ApplyRagdollIntegration(ragdoll, asmName);

            if (!skipScene)
            {
                // Step 5: Scene setup
                var existing = Object.FindFirstObjectByType<BillVRBootstrap>();
                if (existing != null)
                    Undo.DestroyObjectImmediate(existing.gameObject);

                BillVRSceneBuilder.BuildPlayerRig();
                BillVRSceneBuilder.CreateFloor();
                BillVRSceneBuilder.CreateTestGrabbables();
            }

            // Step 6: Default assets
            BillVRAssetCreator.CreateAllDefaults();

            // Step 7: Performance
            BillVRSceneBuilder.ApplyVRPerformanceSettings();

            // Step 8: Validate
            _validationIssues = CountValidationIssues();

            // Refresh package check
            RefreshPackageCheck();

            Debug.Log("[BillVR] All setup steps completed.");
        }

        // ─────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────

        private void RefreshPackageCheck()
        {
            _checkingPackages = true;
            _packagesChecked = false;

            BillVRPackageValidator.CheckPackagesAsync(result =>
            {
                _packageResult = result;
                _packagesChecked = true;
                _checkingPackages = false;
                Repaint();
            });
        }

        private void ApplyProjectSettings()
        {
            PlayerSettings.colorSpace = ColorSpace.Linear;

#if ENABLE_INPUT_SYSTEM && ENABLE_LEGACY_INPUT_MANAGER
            Debug.Log("[BillVR] Input handling already set to Both.");
#else
            Debug.LogWarning("[BillVR] Go to Project Settings > Player > Active Input Handling and select 'Both'. Unity will restart.");
#endif
        }

        private bool IsBothInputMode()
        {
#if ENABLE_INPUT_SYSTEM && ENABLE_LEGACY_INPUT_MANAGER
            return true;
#else
            return false;
#endif
        }

        private int CountValidationIssues()
        {
            int issues = 0;

            if (Object.FindFirstObjectByType<BillVRBootstrap>() == null) issues++;
            if (Object.FindFirstObjectByType<Input.InputManager>() == null) issues++;

            var hands = Object.FindObjectsByType<Hand.VRHand>(FindObjectsSortMode.None);
            bool hasLeft = false, hasRight = false;
            foreach (var hand in hands)
            {
                if (hand.Side == HandSide.Left) hasLeft = true;
                if (hand.Side == HandSide.Right) hasRight = true;
                if (hand.FollowTarget == null) issues++;
                if (hand.PalmTransform == null) issues++;
                if (hand.GetComponent<Hand.GrabHandler>() == null) issues++;
            }
            if (!hasLeft) issues++;
            if (!hasRight) issues++;

            if (Object.FindFirstObjectByType<Tracking.TrackedHeadDriver>() == null) issues++;
            if (Object.FindObjectsByType<Tracking.TrackedControllerDriver>(FindObjectsSortMode.None).Length < 2) issues++;
            if (Object.FindFirstObjectByType<Locomotion.VRPlayerBody>() == null) issues++;
            if (Object.FindFirstObjectByType<Locomotion.LocomotionStateMachine>() == null) issues++;
            if (!BillVRLayerSetup.AllLayersExist()) issues++;
            if (PlayerSettings.colorSpace != ColorSpace.Linear) issues++;

            return issues;
        }

        private void DrawStatusLine(string label, bool ok)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(ok ? "\u2705" : "\u274C", GUILayout.Width(24));
            EditorGUILayout.LabelField(label);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSeparator()
        {
            EditorGUILayout.Space(4);
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.3f));
            EditorGUILayout.Space(4);
        }
    }
}
#endif
