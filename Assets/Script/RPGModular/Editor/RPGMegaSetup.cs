#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace RPGModular.Editor
{
    /// <summary>
    /// MegaSetup — Tự động gắn tất cả component RPGModular lên Player GameObject.
    /// Tạo cấu trúc đầy đủ: Player root + AnimationController child + Hitbox + LockOnPoint.
    /// Menu: RPG > Mega Setup Player
    /// </summary>
    public static class RPGMegaSetup
    {
        [MenuItem("RPG/Mega Setup Player", false, 100)]
        public static void SetupPlayer()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("RPG Mega Setup",
                    "Chọn 1 GameObject trong scene làm Player root trước khi chạy setup.", "OK");
                return;
            }

            Undo.RegisterCompleteObjectUndo(selected, "RPG Mega Setup Player");

            // ═══════════════════════════════════════════════════════
            // Layer setup
            // ═══════════════════════════════════════════════════════
            selected.tag = "Player";

            // ═══════════════════════════════════════════════════════
            // Core Components
            // ═══════════════════════════════════════════════════════
            AddIfMissing<PlayerCore>(selected);
            AddIfMissing<CharacterStats>(selected);
            AddIfMissing<HealthSystem>(selected);
            AddIfMissing<PlayerInputHandler>(selected);
            AddIfMissing<PlayerController>(selected);

            // ═══════════════════════════════════════════════════════
            // Combat Components
            // ═══════════════════════════════════════════════════════
            AddIfMissing<CombatStateMachine>(selected);
            AddIfMissing<PlayerDamageHandler>(selected);
            AddIfMissing<CombatLocomotion>(selected);
            AddIfMissing<LockOnSystem>(selected);
            AddIfMissing<AutoAttackSystem>(selected);
            AddIfMissing<FocusGauge>(selected);

            // ═══════════════════════════════════════════════════════
            // Weapon + Visual
            // ═══════════════════════════════════════════════════════
            AddIfMissing<WeaponHandler>(selected);
            AddIfMissing<WeaponVisualHandler>(selected);

            // ═══════════════════════════════════════════════════════
            // Locomotion
            // ═══════════════════════════════════════════════════════
            AddIfMissing<LocomotionStateMachine>(selected);

            // ═══════════════════════════════════════════════════════
            // Progression
            // ═══════════════════════════════════════════════════════
            AddIfMissing<Inventory>(selected);
            AddIfMissing<EquipmentSystem>(selected);
            AddIfMissing<LevelSystem>(selected);
            AddIfMissing<StatusEffectSystem>(selected);

            // ═══════════════════════════════════════════════════════
            // Skill
            // ═══════════════════════════════════════════════════════
            AddIfMissing<PlayerSkillBook>(selected);
            AddIfMissing<SkillBar>(selected);
            AddIfMissing<SkillCaster>(selected);
            AddIfMissing<ComboTracker>(selected);

            // ═══════════════════════════════════════════════════════
            // Child: AnimationController (trên child có Animator)
            // ═══════════════════════════════════════════════════════
            Transform modelChild = selected.transform.childCount > 0
                ? selected.transform.GetChild(0)
                : null;

            if (modelChild != null)
            {
                AddIfMissing<Animator>(modelChild.gameObject);
                AddIfMissing<AnimationController>(modelChild.gameObject);
            }
            else
            {
                Debug.LogWarning("[MegaSetup] Không tìm thấy child object cho Animator. " +
                    "Tạo 1 child GameObject chứa model 3D, rồi chạy lại setup.");
            }

            // ═══════════════════════════════════════════════════════
            // Child: HitboxManager (trên child riêng hoặc root)
            // ═══════════════════════════════════════════════════════
            var hitboxGO = FindOrCreateChild(selected, "Hitboxes");
            AddIfMissing<HitboxManager>(hitboxGO);

            // ═══════════════════════════════════════════════════════
            // Child: LockOnPoint
            // ═══════════════════════════════════════════════════════
            var lockOnPoint = FindOrCreateChild(selected, "LockOnPoint");
            lockOnPoint.transform.localPosition = new Vector3(0, 1.2f, 0);

            // ═══════════════════════════════════════════════════════
            // Collider + Rigidbody (nếu chưa có)
            // ═══════════════════════════════════════════════════════
            var cc = AddIfMissing<CharacterController>(selected);
            if (cc != null)
            {
                cc.center = new Vector3(0, 0.9f, 0);
                cc.height = 1.8f;
                cc.radius = 0.3f;
            }

            EditorUtility.SetDirty(selected);
            Debug.Log($"[MegaSetup] Player setup hoàn tất! {CountComponents(selected)} components trên '{selected.name}'.");
        }

        [MenuItem("RPG/Mega Setup Singletons", false, 101)]
        public static void SetupSingletons()
        {
            // Tạo GameObject chứa các singleton không nằm trên player
            var singletonsGO = FindOrCreateRoot("[RPG_Singletons]");
            AddIfMissing<LootSystem>(singletonsGO);
            AddIfMissing<DeathSystem>(singletonsGO);
            AddIfMissing<DialogueSystem>(singletonsGO);
            AddIfMissing<ShopService>(singletonsGO);
            AddIfMissing<ZoneSystem>(singletonsGO);
            AddIfMissing<SaveLoadSystem>(singletonsGO);
            AddIfMissing<CraftingSystem>(singletonsGO);
            AddIfMissing<WeaponEnhancement>(singletonsGO);
            AddIfMissing<TamerSystem>(singletonsGO);

            EditorUtility.SetDirty(singletonsGO);
            Debug.Log($"[MegaSetup] Singletons setup hoàn tất trên '{singletonsGO.name}'.");
        }

        [MenuItem("RPG/Mega Setup SpawnZone", false, 102)]
        public static void SetupSpawnZone()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                var go = new GameObject("SpawnZone_New");
                selected = go;
                Undo.RegisterCreatedObjectUndo(go, "Create SpawnZone");
            }

            AddIfMissing<PackManager>(selected);
            AddIfMissing<VAT_MobSpawner>(selected);

            EditorUtility.SetDirty(selected);
            Debug.Log($"[MegaSetup] SpawnZone setup hoàn tất trên '{selected.name}'.");
        }

        [MenuItem("RPG/Setup Quest Tracker on Player", false, 103)]
        public static void SetupQuestTracker()
        {
            var player = FindPlayerInScene();
            if (player == null) return;

            AddIfMissing<QuestTracker>(player);
            EditorUtility.SetDirty(player);
            Debug.Log("[MegaSetup] QuestTracker đã gắn lên Player.");
        }

        // ═══════════════════════════════════════════════════════
        // Validation
        // ═══════════════════════════════════════════════════════

        [MenuItem("RPG/Validate Player Setup", false, 200)]
        public static void ValidatePlayer()
        {
            var player = FindPlayerInScene();
            if (player == null)
            {
                Debug.LogError("[Validate] Không tìm thấy GameObject có tag 'Player' trong scene!");
                return;
            }

            int issues = 0;
            issues += Check<PlayerCore>(player, "PlayerCore");
            issues += Check<CharacterStats>(player, "CharacterStats");
            issues += Check<HealthSystem>(player, "HealthSystem");
            issues += Check<PlayerInputHandler>(player, "PlayerInputHandler");
            issues += Check<PlayerController>(player, "PlayerController");
            issues += Check<CombatStateMachine>(player, "CombatStateMachine");
            issues += Check<PlayerDamageHandler>(player, "PlayerDamageHandler");
            issues += Check<LockOnSystem>(player, "LockOnSystem");
            issues += Check<WeaponHandler>(player, "WeaponHandler");
            issues += Check<AutoAttackSystem>(player, "AutoAttackSystem");
            issues += Check<LocomotionStateMachine>(player, "LocomotionStateMachine");
            issues += Check<Inventory>(player, "Inventory");
            issues += Check<EquipmentSystem>(player, "EquipmentSystem");
            issues += Check<LevelSystem>(player, "LevelSystem");
            issues += Check<PlayerSkillBook>(player, "PlayerSkillBook");
            issues += Check<SkillBar>(player, "SkillBar");
            issues += Check<SkillCaster>(player, "SkillCaster");
            issues += Check<ComboTracker>(player, "ComboTracker");
            issues += Check<StatusEffectSystem>(player, "StatusEffectSystem");
            issues += Check<FocusGauge>(player, "FocusGauge");
            issues += Check<WeaponVisualHandler>(player, "WeaponVisualHandler");

            // Check children
            var animCtrl = player.GetComponentInChildren<AnimationController>();
            if (animCtrl == null) { Debug.LogWarning("[Validate] THIẾU AnimationController (trên child)"); issues++; }

            var hitbox = player.GetComponentInChildren<HitboxManager>();
            if (hitbox == null) { Debug.LogWarning("[Validate] THIẾU HitboxManager (trên child)"); issues++; }

            if (issues == 0)
                Debug.Log($"[Validate] Player '{player.name}' — TẤT CẢ OK! {CountComponents(player)} components.");
            else
                Debug.LogWarning($"[Validate] Player '{player.name}' — {issues} component thiếu! Chạy RPG > Mega Setup Player.");
        }

        // ═══════════════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════════════

        private static T AddIfMissing<T>(GameObject go) where T : Component
        {
            var existing = go.GetComponent<T>();
            if (existing != null) return existing;
            return Undo.AddComponent<T>(go);
        }

        private static GameObject FindOrCreateChild(GameObject parent, string name)
        {
            var t = parent.transform.Find(name);
            if (t != null) return t.gameObject;

            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            Undo.RegisterCreatedObjectUndo(child, $"Create {name}");
            return child;
        }

        private static GameObject FindOrCreateRoot(string name)
        {
            var existing = GameObject.Find(name);
            if (existing != null) return existing;

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            return go;
        }

        private static GameObject FindPlayerInScene()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                EditorUtility.DisplayDialog("RPG Validate",
                    "Không tìm thấy GameObject có tag 'Player' trong scene.", "OK");
            }
            return player;
        }

        private static int Check<T>(GameObject go, string label) where T : Component
        {
            if (go.GetComponent<T>() == null)
            {
                Debug.LogWarning($"[Validate] THIẾU {label}");
                return 1;
            }
            return 0;
        }

        private static int CountComponents(GameObject go)
        {
            return go.GetComponents<Component>().Length;
        }
    }
}
#endif
