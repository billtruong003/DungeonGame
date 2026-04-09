#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace VRCore.Editor
{
    public class VRCoreSetupWizard : EditorWindow
    {
        private VRCorePackageValidator.PackageCheckResult _packageResult;
        private bool _packagesChecked;
        private bool _checkingPackages;
        private Vector2 _scroll;

        [MenuItem("VRCore/Setup Wizard", priority = 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<VRCoreSetupWizard>("VRCore Setup Wizard");
            window.minSize = new Vector2(450, 600);
            window.Show();
        }

        [MenuItem("VRCore/Create Player Rig", priority = 20)]
        public static void MenuCreateRig() => VRCoreSceneBuilder.BuildPlayerRig();

        [MenuItem("VRCore/Create Test Grabbables", priority = 21)]
        public static void MenuCreateGrabbables() => VRCoreSceneBuilder.CreateTestGrabbables();

        [MenuItem("VRCore/Setup Layers + Collision Matrix", priority = 40)]
        public static void MenuSetupLayers()
        {
            VRCoreLayerSetup.CreateAllLayers();
            VRCoreLayerSetup.ConfigureCollisionMatrix();
        }

        [MenuItem("VRCore/Detect Ragdoll Animator 2", priority = 41)]
        public static void MenuDetectRagdoll()
        {
            bool found = VRCorePackageValidator.DetectRagdollAnimator(out string asmName);
            VRCorePackageValidator.ApplyRagdollIntegration(found, asmName);
            Debug.Log(found
                ? $"[VRCore] Ragdoll Animator 2 detected in assembly '{asmName}'."
                : "[VRCore] Ragdoll Animator 2 not found.");
        }

        private void OnEnable()
        {
            RefreshPackageCheck();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawHeader();
            EditorGUILayout.Space(10);
            DrawStepPackages();
            EditorGUILayout.Space(6);
            DrawStepProjectSettings();
            EditorGUILayout.Space(6);
            DrawStepLayers();
            EditorGUILayout.Space(6);
            DrawStepRagdollDetection();
            EditorGUILayout.Space(6);
            DrawStepSceneSetup();
            EditorGUILayout.Space(20);
            DrawRunAll();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("VRCore Setup Wizard", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Configures project for VR interaction development.", EditorStyles.wordWrappedMiniLabel);
            DrawSeparator();
        }

        private void DrawStepPackages()
        {
            EditorGUILayout.LabelField("Step 1: Package Validation", EditorStyles.boldLabel);

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
                        VRCorePackageValidator.InstallPackage(status.requirement.id);
                        RefreshPackageCheck();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            if (_packageResult.allRequiredInstalled)
                EditorGUILayout.HelpBox("All required packages installed.", MessageType.Info);
            else
                EditorGUILayout.HelpBox("Some required packages are missing.", MessageType.Warning);
        }

        private void DrawStepProjectSettings()
        {
            EditorGUILayout.LabelField("Step 2: Project Settings", EditorStyles.boldLabel);

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

        private void DrawStepLayers()
        {
            EditorGUILayout.LabelField("Step 3: Physics Layers", EditorStyles.boldLabel);

            bool allExist = VRCoreLayerSetup.AllLayersExist();
            DrawStatusLine("All VRCore layers exist", allExist);

            if (!allExist)
            {
                if (GUILayout.Button("Create Layers + Configure Collision Matrix"))
                {
                    VRCoreLayerSetup.CreateAllLayers();
                    VRCoreLayerSetup.ConfigureCollisionMatrix();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Layers configured correctly.", MessageType.Info);
            }
        }

        private void DrawStepRagdollDetection()
        {
            EditorGUILayout.LabelField("Step 4: Ragdoll Animator 2", EditorStyles.boldLabel);

            bool detected = _packagesChecked
                ? _packageResult.ragdollDetected
                : VRCorePackageValidator.DetectRagdollAnimator(out _);

            DrawStatusLine("Ragdoll Animator 2", detected);

            if (detected)
                EditorGUILayout.HelpBox("Ragdoll Animator 2 detected. Combat features enabled.", MessageType.Info);
            else
                EditorGUILayout.HelpBox("Not found. Fallback ragdoll will be used.", MessageType.Warning);

            if (GUILayout.Button("Refresh Detection"))
            {
                bool found = VRCorePackageValidator.DetectRagdollAnimator(out string asmName);
                VRCorePackageValidator.ApplyRagdollIntegration(found, asmName);
            }
        }

        private void DrawStepSceneSetup()
        {
            EditorGUILayout.LabelField("Step 5: Scene Setup", EditorStyles.boldLabel);

            bool hasRig = Object.FindFirstObjectByType<VRCoreBootstrap>() != null;
            DrawStatusLine("Player Rig in Scene", hasRig);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(hasRig ? "Rebuild Player Rig" : "Create Player Rig"))
            {
                if (hasRig)
                {
                    var existing = Object.FindFirstObjectByType<VRCoreBootstrap>();
                    if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);
                }
                VRCoreSceneBuilder.BuildPlayerRig();
                VRCoreSceneBuilder.CreateFloor();
            }

            if (GUILayout.Button("Add Test Grabbables"))
                VRCoreSceneBuilder.CreateTestGrabbables();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawRunAll()
        {
            DrawSeparator();

            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
            if (GUILayout.Button("Run All Steps", GUILayout.Height(36)))
            {
                ApplyProjectSettings();
                VRCoreLayerSetup.CreateAllLayers();
                VRCoreLayerSetup.ConfigureCollisionMatrix();

                bool ragdoll = VRCorePackageValidator.DetectRagdollAnimator(out string asmName);
                VRCorePackageValidator.ApplyRagdollIntegration(ragdoll, asmName);

                var existing = Object.FindFirstObjectByType<VRCoreBootstrap>();
                if (existing != null)
                    Undo.DestroyObjectImmediate(existing.gameObject);

                VRCoreSceneBuilder.BuildPlayerRig();
                VRCoreSceneBuilder.CreateFloor();
                VRCoreSceneBuilder.CreateTestGrabbables();

                VRCoreAssetCreator.CreateAllDefaults();

                RefreshPackageCheck();
                Debug.Log("[VRCore] All setup steps completed.");
            }
            GUI.backgroundColor = Color.white;
        }

        private void RefreshPackageCheck()
        {
            _checkingPackages = true;
            _packagesChecked = false;

            VRCorePackageValidator.CheckPackagesAsync(result =>
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
            Debug.Log("[VRCore] Input handling already set to Both.");
#else
            Debug.LogWarning("[VRCore] Go to Project Settings > Player > Active Input Handling and select 'Both'. Unity will restart.");
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