#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace VRCore.Editor
{
    public static class VRCorePackageValidator
    {
        private const string RagdollDefine = "VRCORE_HAS_RAGDOLL";
        private const string RagdollNamespace = "FIMSpace.FProceduralAnimation";
        private const string RagdollTypeName = "RagdollAnimator2";
        private const string RuntimeAsmdefGuid = "VRCore.Runtime";

        public static readonly PackageRequirement[] RequiredPackages = new[]
        {
            new PackageRequirement("com.unity.xr.management", "XR Plugin Management"),
            new PackageRequirement("com.unity.xr.openxr", "OpenXR Plugin"),
            new PackageRequirement("com.unity.xr.interaction.toolkit", "XR Interaction Toolkit"),
            new PackageRequirement("com.unity.inputsystem", "Input System"),
            new PackageRequirement("com.unity.xr.hands", "XR Hands (optional)", true),
        };

        private static ListRequest _listRequest;
        private static Action<PackageCheckResult> _onComplete;

        public struct PackageRequirement
        {
            public string id;
            public string displayName;
            public bool optional;
            public PackageRequirement(string id, string displayName, bool optional = false)
            { this.id = id; this.displayName = displayName; this.optional = optional; }
        }

        public struct PackageStatus
        {
            public PackageRequirement requirement;
            public bool installed;
            public string version;
        }

        public struct PackageCheckResult
        {
            public List<PackageStatus> statuses;
            public bool allRequiredInstalled;
            public bool ragdollDetected;
            public string ragdollAssemblyName;
        }

        public static void CheckPackagesAsync(Action<PackageCheckResult> onComplete)
        {
            _onComplete = onComplete;
            _listRequest = Client.List(true);
            EditorApplication.update += OnListRequestUpdate;
        }

        private static void OnListRequestUpdate()
        {
            if (!_listRequest.IsCompleted) return;
            EditorApplication.update -= OnListRequestUpdate;

            var result = new PackageCheckResult
            {
                statuses = new List<PackageStatus>(),
                allRequiredInstalled = true
            };

            var installedIds = new HashSet<string>();
            var versionMap = new Dictionary<string, string>();

            if (_listRequest.Status == StatusCode.Success)
            {
                foreach (var pkg in _listRequest.Result)
                {
                    installedIds.Add(pkg.name);
                    versionMap[pkg.name] = pkg.version;
                }
            }

            foreach (var req in RequiredPackages)
            {
                bool installed = installedIds.Contains(req.id);
                string version = installed && versionMap.ContainsKey(req.id) ? versionMap[req.id] : "";
                result.statuses.Add(new PackageStatus
                {
                    requirement = req, installed = installed, version = version
                });
                if (!installed && !req.optional)
                    result.allRequiredInstalled = false;
            }

            result.ragdollDetected = DetectRagdollAnimator(out string ra2AssemblyName);
            result.ragdollAssemblyName = ra2AssemblyName;
            ApplyRagdollIntegration(result.ragdollDetected, ra2AssemblyName);

            _onComplete?.Invoke(result);
        }

        public static bool DetectRagdollAnimator(out string assemblyName)
        {
            assemblyName = null;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.Namespace == RagdollNamespace && type.Name == RagdollTypeName)
                        {
                            assemblyName = assembly.GetName().Name;
                            return true;
                        }
                    }
                }
                catch { }
            }
            return false;
        }

        public static void ApplyRagdollIntegration(bool hasRagdoll, string ra2AssemblyName)
        {
            UpdateScriptingDefine(hasRagdoll);

            if (hasRagdoll && !string.IsNullOrEmpty(ra2AssemblyName))
            {
                if (ra2AssemblyName == "Assembly-CSharp" || ra2AssemblyName == "Assembly-CSharp-firstpass")
                {
                    Debug.LogWarning(
                        "[VRCore] Ragdoll Animator 2 found but it compiles into Assembly-CSharp (no .asmdef). " +
                        "VRCore.Runtime.asmdef cannot reference Assembly-CSharp. " +
                        "To enable RA2 integration, create an .asmdef in the FImpossible Creations folder, " +
                        "or remove VRCORE_HAS_RAGDOLL from Scripting Defines to use fallback ragdoll.");
                    UpdateScriptingDefine(false);
                    return;
                }

                AddAssemblyReference(ra2AssemblyName);
            }
            else if (!hasRagdoll)
            {
                RemoveAllRA2References();
            }
        }

        private static void UpdateScriptingDefine(bool hasRagdoll)
        {
            var target = EditorUserBuildSettings.selectedBuildTargetGroup;
            if (target == BuildTargetGroup.Unknown) target = BuildTargetGroup.Standalone;

            string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(target);
            var defineList = defines.Split(';').Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
            bool hasDefine = defineList.Contains(RagdollDefine);

            if (hasRagdoll && !hasDefine)
            {
                defineList.Add(RagdollDefine);
                PlayerSettings.SetScriptingDefineSymbolsForGroup(target, string.Join(";", defineList));
                Debug.Log("[VRCore] Added VRCORE_HAS_RAGDOLL scripting define.");
            }
            else if (!hasRagdoll && hasDefine)
            {
                defineList.Remove(RagdollDefine);
                PlayerSettings.SetScriptingDefineSymbolsForGroup(target, string.Join(";", defineList));
                Debug.Log("[VRCore] Removed VRCORE_HAS_RAGDOLL scripting define.");
            }
        }

        private static string FindVRCoreRuntimeAsmdef()
        {
            string[] guids = AssetDatabase.FindAssets("VRCore.Runtime t:asmdef");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("VRCore.Runtime.asmdef"))
                    return path;
            }

            string[] allAsmdefs = AssetDatabase.FindAssets("t:asmdef");
            foreach (string guid in allAsmdefs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                try
                {
                    string json = File.ReadAllText(path);
                    if (json.Contains("\"name\": \"VRCore.Runtime\"") || json.Contains("\"name\":\"VRCore.Runtime\""))
                        return path;
                }
                catch { }
            }

            return null;
        }

        private static void AddAssemblyReference(string assemblyName)
        {
            string asmdefPath = FindVRCoreRuntimeAsmdef();
            if (asmdefPath == null)
            {
                Debug.LogWarning("[VRCore] Could not find VRCore.Runtime.asmdef to add RA2 reference.");
                return;
            }

            string json = File.ReadAllText(asmdefPath);
            if (json.Contains(assemblyName))
            {
                Debug.Log($"[VRCore] VRCore.Runtime.asmdef already references '{assemblyName}'.");
                return;
            }

            var asmdef = JsonUtility.FromJson<AsmdefData>(json);
            if (asmdef.references == null)
                asmdef.references = new string[0];

            var refs = new List<string>(asmdef.references);
            if (!refs.Contains(assemblyName))
            {
                refs.Add(assemblyName);
                asmdef.references = refs.ToArray();

                string newJson = JsonUtility.ToJson(asmdef, true);
                File.WriteAllText(asmdefPath, newJson);
                AssetDatabase.Refresh();

                Debug.Log($"[VRCore] Added '{assemblyName}' reference to VRCore.Runtime.asmdef for Ragdoll Animator 2 integration.");
            }
        }

        private static void RemoveAllRA2References()
        {
            string asmdefPath = FindVRCoreRuntimeAsmdef();
            if (asmdefPath == null) return;

            string json = File.ReadAllText(asmdefPath);
            if (!json.Contains("FImpossible") && !json.Contains("Ragdoll")) return;

            var asmdef = JsonUtility.FromJson<AsmdefData>(json);
            if (asmdef.references == null || asmdef.references.Length == 0) return;

            var refs = new List<string>(asmdef.references);
            int removed = refs.RemoveAll(r =>
                r.Contains("FImpossible") || r.Contains("Ragdoll") || r.Contains("FIMSpace"));

            if (removed > 0)
            {
                asmdef.references = refs.ToArray();
                string newJson = JsonUtility.ToJson(asmdef, true);
                File.WriteAllText(asmdefPath, newJson);
                AssetDatabase.Refresh();
                Debug.Log("[VRCore] Removed RA2 references from VRCore.Runtime.asmdef.");
            }
        }

        public static void InstallPackage(string packageId)
        {
            Client.Add(packageId);
            Debug.Log($"[VRCore] Installing {packageId}...");
        }

        [Serializable]
        private class AsmdefData
        {
            public string name;
            public string rootNamespace;
            public string[] references;
            public string[] includePlatforms;
            public string[] excludePlatforms;
            public bool allowUnsafeCode;
            public bool overrideReferences;
            public string[] precompiledReferences;
            public bool autoReferenced;
            public string[] defineConstraints;
            public VersionDefine[] versionDefines;
            public bool noEngineReferences;
        }

        [Serializable]
        private class VersionDefine
        {
            public string name;
            public string expression;
            public string define;
        }
    }
}
#endif
