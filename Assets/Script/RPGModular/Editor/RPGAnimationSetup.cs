#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

namespace RPGModular.Editor
{
    /// <summary>
    /// Animation Setup Wizard — Humanoid retargeting aware.
    ///
    /// Flow:
    /// 1. Gán Model FBX (nguồn Avatar) → tool extract Avatar
    /// 2. Kéo animation FBX clips vào slots → preview
    /// 3. Bấm XUẤT → tạo Animator Controller với Blend Tree
    ///    - Clips tham chiếu trực tiếp từ FBX (KHÔNG copy) → giữ Humanoid retargeting
    ///    - Hoặc chọn "Extract Clips" → duplicate ra .anim riêng (đổi tên chuẩn convention)
    /// 4. Auto gán Avatar lên Animator component
    ///
    /// Menu: RPG > Animation Setup Wizard
    /// </summary>
    public class RPGAnimationSetup : EditorWindow
    {
        [MenuItem("RPG/Animation Setup Wizard", false, 300)]
        public static void Open() => GetWindow<RPGAnimationSetup>("RPG Anim Setup").Show();

        // ═══════════════════════════════════════════════════════
        // Config
        // ═══════════════════════════════════════════════════════

        private GameObject modelFBX;
        private Avatar modelAvatar;
        private string outputFolder = "Assets/Animations/Player";
        private string controllerName = "PlayerAnimator";
        private bool extractClips = false; // false = reference trực tiếp từ FBX (khuyến nghị)

        // Clip slots — mỗi slot là (AnimationClip clip, string targetName, bool shouldLoop)
        private ClipSlot[] explorationSlots;
        private ClipSlot[] combatSharedSlots;
        private ClipSlot[] combatWeaponSlots;
        private string weaponPrefix = "Sword";

        // Preview
        private UnityEditor.Editor modelPreviewEditor;
        private AnimationClip previewClip;
        private Vector2 scroll;
        private int tab;
        private string[] tabs = { "1. Model + Avatar", "2. Exploration", "3. Combat Chung", "4. Vũ Khí", "5. Xuất" };

        // ═══════════════════════════════════════════════════════
        // Init
        // ═══════════════════════════════════════════════════════

        private void OnEnable()
        {
            InitSlots();
        }

        private void InitSlots()
        {
            explorationSlots = new ClipSlot[]
            {
                new("Explore_Idle",      "Idle / Breathing Idle", true),
                new("Explore_Walk",      "Walking",               true),
                new("Explore_Run",       "Running / Jogging",     true),
                new("Explore_Sprint",    "Fast Run / Sprinting",  true),
                new("Explore_Jump",      "Jump",                  false),
                new("Explore_DoubleJump","Flip / Jump variant",   false),
                new("Explore_Fall",      "Falling Idle",          true),
                new("Explore_Land_Soft", "Landing",               false),
                new("Explore_Land_Hard", "Hard Landing",          false),
                new("Explore_Dash",      "Dodge Forward",         false),
            };

            combatSharedSlots = new ClipSlot[]
            {
                new("Dodge_Fwd",    "Dodge Forward",         false),
                new("Dodge_Back",   "Dodge Backward",        false),
                new("Dodge_Left",   "Dodge Left",            false),
                new("Dodge_Right",  "Dodge Right",           false),
                new("Death",        "Dying / Death",         false),
                new("Skill_Charge", "Power Up / Charge",     true),
            };

            RebuildWeaponSlots();
        }

        private void RebuildWeaponSlots()
        {
            // Preserve existing clips if prefix changed
            var oldClips = combatWeaponSlots?.ToDictionary(s => s.baseName, s => s.clip);

            string p = weaponPrefix;
            combatWeaponSlots = new ClipSlot[]
            {
                new($"{p}_Idle",       "Sword Idle / Combat Idle",   true),
                new($"{p}_Walk_Fwd",   "Sword Walk / Walk Forward",  true),
                new($"{p}_Walk_Back",  "Walking Backward",           true),
                new($"{p}_Walk_Left",  "Left Strafe Walk",           true),
                new($"{p}_Walk_Right", "Right Strafe Walk",          true),
                new($"{p}_Atk1",       "Sword Slash (đòn 1)",       false),
                new($"{p}_Atk2",       "Slash 2 / Cross Slash",     false),
                new($"{p}_Atk3",       "Overhead Slash / Stab",     false),
                new($"{p}_Hit_Light",  "Hit Reaction / Flinch",     false),
                new($"{p}_Hit_Heavy",  "Big Hit / Knockback",       false),
                new($"{p}_Knockback",  "Stumble Backwards",         false),
                new($"{p}_Equip",      "Draw Sword / Unsheathe",    false),
                new($"{p}_Unequip",    "Sheathe Sword",             false),
            };
        }

        // ═══════════════════════════════════════════════════════
        // GUI
        // ═══════════════════════════════════════════════════════

        private void OnGUI()
        {
            EditorGUILayout.Space(5);
            tab = GUILayout.Toolbar(tab, tabs);
            EditorGUILayout.Space(5);

            scroll = EditorGUILayout.BeginScrollView(scroll);

            switch (tab)
            {
                case 0: DrawModelTab(); break;
                case 1: DrawClipTab("Exploration — 10 clips di chuyển khám phá", explorationSlots); break;
                case 2: DrawClipTab("Combat Chung — 6 clips dùng mọi vũ khí", combatSharedSlots); break;
                case 3: DrawWeaponTab(); break;
                case 4: DrawExportTab(); break;
            }

            EditorGUILayout.EndScrollView();
        }

        // ── Tab 1: Model + Avatar ──

        private void DrawModelTab()
        {
            EditorGUILayout.LabelField("Bước 1: Chọn Model FBX (nguồn Avatar)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Kéo file FBX của nhân vật vào đây.\n" +
                "• Model PHẢI có Rig → Animation Type = Humanoid\n" +
                "• Tool sẽ tự extract Avatar từ FBX\n" +
                "• Avatar này sẽ gán lên Animator khi xuất\n\n" +
                "Nếu dùng Mixamo: upload model → download \"Without Skin\" cho animation,\n" +
                "download \"With Skin\" cho model FBX.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            modelFBX = (GameObject)EditorGUILayout.ObjectField("Model FBX", modelFBX, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck() && modelFBX != null)
            {
                ExtractAvatar();
            }

            // Avatar status
            if (modelAvatar != null)
            {
                EditorGUILayout.Space(5);
                GUI.color = Color.green;
                EditorGUILayout.LabelField($"Avatar: {modelAvatar.name} — {(modelAvatar.isHuman ? "HUMANOID OK" : "KHÔNG PHẢI HUMANOID!")}", EditorStyles.boldLabel);
                GUI.color = Color.white;

                if (!modelAvatar.isHuman)
                {
                    EditorGUILayout.HelpBox(
                        "Avatar không phải Humanoid!\n" +
                        "Fix: Chọn FBX → Inspector → Rig tab → Animation Type = Humanoid → Apply",
                        MessageType.Error);
                }
            }
            else if (modelFBX != null)
            {
                GUI.color = Color.red;
                EditorGUILayout.LabelField("Không tìm thấy Avatar! Kiểm tra FBX Rig setting.", EditorStyles.boldLabel);
                GUI.color = Color.white;
            }

            // Manual avatar override
            EditorGUILayout.Space(5);
            modelAvatar = (Avatar)EditorGUILayout.ObjectField("Avatar (override)", modelAvatar, typeof(Avatar), false);

            // Model preview
            if (modelFBX != null)
            {
                EditorGUILayout.Space(10);
                if (modelPreviewEditor == null || modelPreviewEditor.target != modelFBX)
                {
                    if (modelPreviewEditor != null) DestroyImmediate(modelPreviewEditor);
                    modelPreviewEditor = UnityEditor.Editor.CreateEditor(modelFBX);
                }
                modelPreviewEditor.OnInteractivePreviewGUI(
                    GUILayoutUtility.GetRect(256, 256), EditorStyles.helpBox);
            }

            EditorGUILayout.Space(10);
            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
            controllerName = EditorGUILayout.TextField("Controller Name", controllerName);
        }

        private void ExtractAvatar()
        {
            string path = AssetDatabase.GetAssetPath(modelFBX);
            if (string.IsNullOrEmpty(path)) return;

            // Try ModelImporter
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer != null && importer.animationType != ModelImporterAnimationType.Human)
            {
                Debug.LogWarning($"[AnimSetup] {path} Animation Type = {importer.animationType}. Đổi sang Humanoid!");
            }

            // Extract avatar from FBX sub-assets
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var asset in subAssets)
            {
                if (asset is Avatar av && av.isHuman)
                {
                    modelAvatar = av;
                    Debug.Log($"[AnimSetup] Avatar found: {av.name} (Humanoid)");
                    return;
                }
            }

            // Fallback: try non-human avatar
            foreach (var asset in subAssets)
            {
                if (asset is Avatar av)
                {
                    modelAvatar = av;
                    Debug.LogWarning($"[AnimSetup] Avatar found: {av.name} — KHÔNG phải Humanoid!");
                    return;
                }
            }

            Debug.LogError($"[AnimSetup] Không tìm thấy Avatar trong {path}");
        }

        // ── Tab 2,3: Clip slots ──

        private void DrawClipTab(string title, ClipSlot[] slots)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Kéo animation clip vào slot. Clip có thể từ FBX sub-asset hoặc .anim standalone.\n" +
                "• Cột phải: keyword tìm trên Mixamo.com\n" +
                "• Nút ▶ Preview animation trên model",
                MessageType.Info);

            EditorGUILayout.Space(5);

            foreach (var slot in slots)
                DrawSlotField(slot);

            DrawPreviewSection();
        }

        // ── Tab 4: Weapon ──

        private void DrawWeaponTab()
        {
            EditorGUILayout.LabelField("Combat Vũ Khí — 13 clips", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            weaponPrefix = EditorGUILayout.TextField("Weapon Prefix", weaponPrefix);
            if (EditorGUI.EndChangeCheck())
                RebuildWeaponSlots();

            EditorGUILayout.HelpBox(
                $"Clip sẽ tên: {weaponPrefix}_Idle, {weaponPrefix}_Atk1, ...\n" +
                "Đổi prefix để setup vũ khí khác (GreatSword, Katana...)",
                MessageType.Info);
            EditorGUILayout.Space(5);

            foreach (var slot in combatWeaponSlots)
                DrawSlotField(slot);

            DrawPreviewSection();
        }

        // ── Tab 5: Export ──

        private void DrawExportTab()
        {
            EditorGUILayout.LabelField("Xuất Animator Controller", EditorStyles.boldLabel);

            // Validation
            if (modelAvatar == null)
            {
                EditorGUILayout.HelpBox("CHƯA CÓ AVATAR! Quay tab 1 chọn Model FBX trước.", MessageType.Error);
            }
            else if (!modelAvatar.isHuman)
            {
                EditorGUILayout.HelpBox("Avatar KHÔNG PHẢI Humanoid! Animation sẽ không retarget được.", MessageType.Error);
            }

            // Count
            int total = explorationSlots.Length + combatSharedSlots.Length + combatWeaponSlots.Length;
            int assigned = CountAssigned();

            GUI.color = assigned == total ? Color.green : Color.yellow;
            EditorGUILayout.LabelField($"Clip đã gán: {assigned} / {total}", EditorStyles.boldLabel);
            GUI.color = Color.white;

            if (assigned < total)
            {
                var missing = GetMissingSlots();
                EditorGUILayout.HelpBox($"Còn thiếu:\n{string.Join("\n", missing.Select(s => $"  • {s.targetName}"))}", MessageType.Warning);
            }

            // Options
            EditorGUILayout.Space(10);
            extractClips = EditorGUILayout.Toggle(
                new GUIContent("Extract Clips thành .anim riêng",
                    "OFF (khuyến nghị): Animator tham chiếu clip trực tiếp từ FBX → luôn đúng Humanoid.\n" +
                    "ON: Copy clip ra file .anim riêng → gọn folder nhưng cần Avatar đúng."),
                extractClips);

            if (extractClips)
            {
                EditorGUILayout.HelpBox(
                    "Extract Mode: Clip sẽ copy ra Assets/Animations/Player/Clips/ với tên chuẩn.\n" +
                    "Animation vẫn hoạt động Humanoid vì Animator có Avatar gán sẵn.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Reference Mode (khuyến nghị): Clip giữ nguyên trong FBX.\n" +
                    "Animator Controller tham chiếu trực tiếp → không bao giờ mất Humanoid data.",
                    MessageType.Info);
            }

            // Output preview
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Output:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"  Controller: {outputFolder}/{controllerName}.controller");
            EditorGUILayout.LabelField($"  Avatar: {(modelAvatar != null ? modelAvatar.name : "NONE")}");
            if (extractClips)
                EditorGUILayout.LabelField($"  Clips: {outputFolder}/Clips/ ({assigned} file .anim)");

            // Animator structure preview
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "Animator Controller sẽ tạo:\n\n" +
                "Base Layer (1 layer duy nhất):\n" +
                "├── BlendTree 'Locomotion' (1D, MoveSpeed)\n" +
                "│   ├── Idle     → MoveSpeed = 0\n" +
                "│   ├── Walk     → MoveSpeed = 0.3\n" +
                "│   ├── Run      → MoveSpeed = 0.6\n" +
                "│   └── Sprint   → MoveSpeed = 1.0\n" +
                "├── BlendTree 'CombatStrafe' (2D, MoveX + MoveY)\n" +
                "│   ├── Center   → Weapon_Idle\n" +
                "│   ├── Forward  → Weapon_Walk_Fwd\n" +
                "│   ├── Back     → Weapon_Walk_Back\n" +
                "│   ├── Left     → Weapon_Walk_Left\n" +
                "│   └── Right    → Weapon_Walk_Right\n" +
                "├── Jump, DoubleJump, Fall, Land, Dash\n" +
                "├── Dodge × 4, Death, Skill_Charge\n" +
                "├── Weapon_Atk1/2/3, HitLight/Heavy, Knockback\n" +
                "└── Weapon_Equip, Weapon_Unequip\n\n" +
                "Parameters: MoveSpeed, MoveX, MoveY, IsGrounded, InCombat",
                MessageType.None);

            // EXPORT BUTTON
            EditorGUILayout.Space(20);
            GUI.enabled = modelAvatar != null && assigned > 0;
            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.3f);
            if (GUILayout.Button("XUẤT ANIMATOR CONTROLLER", GUILayout.Height(45)))
            {
                BuildAnimatorController();
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;

            // Auto-assign button
            EditorGUILayout.Space(10);
            if (GUILayout.Button("Gán Avatar lên Animator trong Scene (chọn Player GO trước)"))
            {
                AssignAvatarToSelectedAnimator();
            }
        }

        // ═══════════════════════════════════════════════════════
        // Slot drawing
        // ═══════════════════════════════════════════════════════

        private void DrawSlotField(ClipSlot slot)
        {
            EditorGUILayout.BeginHorizontal();

            // Clip field
            slot.clip = (AnimationClip)EditorGUILayout.ObjectField(
                slot.targetName, slot.clip, typeof(AnimationClip), false);

            // Status indicators
            if (slot.clip != null)
            {
                // Loop check
                if (slot.shouldLoop)
                {
                    GUI.color = slot.clip.isLooping ? Color.green : Color.red;
                    if (GUILayout.Button(slot.clip.isLooping ? "Loop" : "NO LOOP!", GUILayout.Width(65)))
                    {
                        // Try to fix loop setting
                        var settings = AnimationUtility.GetAnimationClipSettings(slot.clip);
                        settings.loopTime = true;
                        AnimationUtility.SetAnimationClipSettings(slot.clip, settings);
                        Debug.Log($"[AnimSetup] Set loop ON: {slot.clip.name}");
                    }
                    GUI.color = Color.white;
                }

                // Duration
                GUILayout.Label($"{slot.clip.length:F2}s", GUILayout.Width(45));

                // Preview button
                if (GUILayout.Button("▶", GUILayout.Width(25)))
                    previewClip = slot.clip;
            }
            else
            {
                // Hint
                GUI.color = new Color(0.5f, 0.5f, 0.5f);
                GUILayout.Label(slot.mixamoHint, GUILayout.Width(180));
                GUI.color = Color.white;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawPreviewSection()
        {
            if (previewClip == null) return;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField($"▶ Preview: {previewClip.name}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                $"   Thời lượng: {previewClip.length:F2}s | " +
                $"Loop: {previewClip.isLooping} | " +
                $"FPS: {previewClip.frameRate} | " +
                $"Humanoid: {previewClip.humanMotion}");

            if (modelFBX != null)
            {
                if (modelPreviewEditor == null || modelPreviewEditor.target != modelFBX)
                {
                    if (modelPreviewEditor != null) DestroyImmediate(modelPreviewEditor);
                    modelPreviewEditor = UnityEditor.Editor.CreateEditor(modelFBX);
                }
                modelPreviewEditor.OnInteractivePreviewGUI(
                    GUILayoutUtility.GetRect(200, 200), EditorStyles.helpBox);
            }
        }

        // ═══════════════════════════════════════════════════════
        // Counting
        // ═══════════════════════════════════════════════════════

        private int CountAssigned()
        {
            int c = 0;
            foreach (var s in explorationSlots) if (s.clip != null) c++;
            foreach (var s in combatSharedSlots) if (s.clip != null) c++;
            foreach (var s in combatWeaponSlots) if (s.clip != null) c++;
            return c;
        }

        private List<ClipSlot> GetMissingSlots()
        {
            var missing = new List<ClipSlot>();
            foreach (var s in explorationSlots) if (s.clip == null) missing.Add(s);
            foreach (var s in combatSharedSlots) if (s.clip == null) missing.Add(s);
            foreach (var s in combatWeaponSlots) if (s.clip == null) missing.Add(s);
            return missing;
        }

        // ═══════════════════════════════════════════════════════
        // Build Animator Controller
        // ═══════════════════════════════════════════════════════

        private void BuildAnimatorController()
        {
            EnsureFolder(outputFolder);

            // Resolve clips (reference or extract)
            var clipMap = new Dictionary<string, AnimationClip>();
            ResolveClips(explorationSlots, clipMap);
            ResolveClips(combatSharedSlots, clipMap);
            ResolveClips(combatWeaponSlots, clipMap);

            // Create Animator Controller
            string controllerPath = $"{outputFolder}/{controllerName}.controller";

            // Delete old if exists
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
                AssetDatabase.DeleteAsset(controllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            // Parameters
            controller.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("InCombat", AnimatorControllerParameterType.Bool);

            var sm = controller.layers[0].stateMachine;
            var subAssets = new List<UnityEngine.Object>();

            // ── Blend Tree: Locomotion (Idle→Walk→Run→Sprint) ──
            var locoTree = new BlendTree
            {
                name = "Locomotion",
                blendParameter = "MoveSpeed",
                blendType = BlendTreeType.Simple1D,
                useAutomaticThresholds = false
            };

            AddToBlendTree(locoTree, clipMap, "Explore_Idle", 0f);
            AddToBlendTree(locoTree, clipMap, "Explore_Walk", 0.3f);
            AddToBlendTree(locoTree, clipMap, "Explore_Run", 0.6f);
            AddToBlendTree(locoTree, clipMap, "Explore_Sprint", 1.0f);

            var locoState = sm.AddState("Locomotion", new Vector3(250, 0));
            locoState.motion = locoTree;
            sm.defaultState = locoState;
            subAssets.Add(locoTree);

            // ── Blend Tree: Combat Strafe (2D, MoveX + MoveY) ──
            string p = weaponPrefix;
            var combatTree = new BlendTree
            {
                name = "CombatStrafe",
                blendType = BlendTreeType.FreeformDirectional2D,
                blendParameter = "MoveX",
                blendParameterY = "MoveY"
            };

            AddToBlendTree2D(combatTree, clipMap, $"{p}_Idle", 0, 0);
            AddToBlendTree2D(combatTree, clipMap, $"{p}_Walk_Fwd", 0, 1);
            AddToBlendTree2D(combatTree, clipMap, $"{p}_Walk_Back", 0, -1);
            AddToBlendTree2D(combatTree, clipMap, $"{p}_Walk_Left", -1, 0);
            AddToBlendTree2D(combatTree, clipMap, $"{p}_Walk_Right", 1, 0);

            var combatLocoState = sm.AddState("CombatStrafe", new Vector3(250, 60));
            combatLocoState.motion = combatTree;
            subAssets.Add(combatTree);

            // ── Flat States (everything not in blend trees) ──
            var blendTreeClips = new HashSet<string>
            {
                "Explore_Idle", "Explore_Walk", "Explore_Run", "Explore_Sprint",
                $"{p}_Idle", $"{p}_Walk_Fwd", $"{p}_Walk_Back", $"{p}_Walk_Left", $"{p}_Walk_Right"
            };

            float yPos = 140;
            foreach (var kvp in clipMap)
            {
                if (blendTreeClips.Contains(kvp.Key)) continue;

                var state = sm.AddState(kvp.Key, new Vector3(250, yPos));
                state.motion = kvp.Value;
                yPos += 40;
            }

            // Save blend trees as sub-assets
            foreach (var obj in subAssets)
                AssetDatabase.AddObjectToAsset(obj, controller);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Result
            int clipCount = clipMap.Count;
            string msg =
                $"Hoàn tất!\n\n" +
                $"Controller: {controllerPath}\n" +
                $"Avatar: {modelAvatar.name}\n" +
                $"Clips: {clipCount} clip ({(extractClips ? "extracted .anim" : "FBX references")})\n\n" +
                $"Blend Trees:\n" +
                $"  • Locomotion (Idle→Walk→Run→Sprint) — MoveSpeed\n" +
                $"  • CombatStrafe (4-way) — MoveX + MoveY\n\n" +
                $"Bước tiếp:\n" +
                $"1. Kéo Controller lên Animator component trên Player model\n" +
                $"2. Gán Avatar: {modelAvatar.name}\n" +
                $"   (hoặc bấm nút 'Gán Avatar lên Animator' ở tab Xuất)";

            EditorUtility.DisplayDialog("Animation Setup", msg, "OK");
            Selection.activeObject = controller;
            EditorGUIUtility.PingObject(controller);
        }

        private void ResolveClips(ClipSlot[] slots, Dictionary<string, AnimationClip> map)
        {
            foreach (var slot in slots)
            {
                if (slot.clip == null) continue;

                if (extractClips)
                {
                    // Extract: copy to standalone .anim
                    string clipFolder = $"{outputFolder}/Clips";
                    EnsureFolder(clipFolder);
                    string targetPath = $"{clipFolder}/{slot.targetName}.anim";

                    var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(targetPath);
                    if (existing != null)
                    {
                        map[slot.targetName] = existing;
                    }
                    else
                    {
                        var copy = Instantiate(slot.clip);
                        copy.name = slot.targetName;

                        // Preserve loop setting
                        var settings = AnimationUtility.GetAnimationClipSettings(copy);
                        settings.loopTime = slot.shouldLoop;
                        AnimationUtility.SetAnimationClipSettings(copy, settings);

                        AssetDatabase.CreateAsset(copy, targetPath);
                        map[slot.targetName] = copy;
                    }
                }
                else
                {
                    // Reference: use clip directly from FBX
                    map[slot.targetName] = slot.clip;
                }
            }
        }

        private void AddToBlendTree(BlendTree tree, Dictionary<string, AnimationClip> map, string key, float threshold)
        {
            if (map.TryGetValue(key, out var clip))
                tree.AddChild(clip, threshold);
        }

        private void AddToBlendTree2D(BlendTree tree, Dictionary<string, AnimationClip> map, string key, float x, float y)
        {
            if (map.TryGetValue(key, out var clip))
                tree.AddChild(clip, new Vector2(x, y));
        }

        // ═══════════════════════════════════════════════════════
        // Avatar assignment
        // ═══════════════════════════════════════════════════════

        private void AssignAvatarToSelectedAnimator()
        {
            if (modelAvatar == null)
            {
                EditorUtility.DisplayDialog("Error", "Chưa có Avatar! Chọn Model FBX ở tab 1 trước.", "OK");
                return;
            }

            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("Error", "Chọn Player GameObject trong Scene trước.", "OK");
                return;
            }

            // Find Animator on selected or children
            var animator = selected.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                EditorUtility.DisplayDialog("Error", $"Không tìm thấy Animator trên '{selected.name}' hoặc child.", "OK");
                return;
            }

            Undo.RecordObject(animator, "Assign Avatar");
            animator.avatar = modelAvatar;
            EditorUtility.SetDirty(animator);

            // Also assign controller if available
            string controllerPath = $"{outputFolder}/{controllerName}.controller";
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
            if (controller != null && animator.runtimeAnimatorController == null)
            {
                animator.runtimeAnimatorController = controller;
                EditorUtility.SetDirty(animator);
            }

            Debug.Log($"[AnimSetup] Avatar '{modelAvatar.name}' gán lên Animator '{animator.gameObject.name}'." +
                (controller != null ? $" Controller '{controllerName}' cũng đã gán." : ""));

            EditorUtility.DisplayDialog("Thành công",
                $"Avatar: {modelAvatar.name}\n" +
                $"Animator: {animator.gameObject.name}\n" +
                (controller != null ? $"Controller: {controllerName}" : "Controller: chưa gán (tạo ở tab Xuất)"),
                "OK");
        }

        // ═══════════════════════════════════════════════════════
        // Utility
        // ═══════════════════════════════════════════════════════

        private void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private void OnDestroy()
        {
            if (modelPreviewEditor != null)
                DestroyImmediate(modelPreviewEditor);
        }

        // ═══════════════════════════════════════════════════════
        // ClipSlot data class
        // ═══════════════════════════════════════════════════════

        [Serializable]
        private class ClipSlot
        {
            public string targetName;    // "Explore_Idle", "Sword_Atk1"
            public string baseName;      // same, for lookup when prefix changes
            public string mixamoHint;    // "Search: 'Breathing Idle'"
            public bool shouldLoop;
            public AnimationClip clip;

            public ClipSlot(string name, string hint, bool loop)
            {
                targetName = name;
                baseName = name;
                mixamoHint = hint;
                shouldLoop = loop;
            }
        }
    }
}
#endif
