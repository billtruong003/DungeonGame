#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace RPGModular.Editor
{
    public class RPGModularSetupWizard : EditorWindow
    {
        // ═══════════════════════════════════════════════════════
        //  Constants
        // ═══════════════════════════════════════════════════════

        private static class Layers
        {
            public const string Player = "Player";
            public const string Enemy = "Enemy";
            public const string Ground = "Ground";
            public const string Interactable = "Interactable";
            public const string Hitbox = "Hitbox";
        }

        private static class Styles
        {
            public static GUIStyle Title;
            public static GUIStyle Subtitle;
            public static GUIStyle SectionHeader;
            public static GUIStyle StatusOK;
            public static GUIStyle StatusMissing;
            public static GUIStyle StatusWarn;
            public static GUIStyle BoxArea;
            public static GUIStyle TabButton;
            public static GUIStyle TabButtonActive;
            public static GUIStyle RichLabel;
            public static bool Initialized;

            public static void Init()
            {
                if (Initialized) return;
                Initialized = true;

                Title = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 18,
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(0, 0, 8, 4)
                };

                Subtitle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Italic,
                    padding = new RectOffset(0, 0, 0, 8)
                };

                SectionHeader = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 13,
                    padding = new RectOffset(2, 0, 8, 4)
                };

                StatusOK = new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = new Color(0.3f, 0.85f, 0.3f) },
                    fontStyle = FontStyle.Bold
                };

                StatusMissing = new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = new Color(0.9f, 0.3f, 0.3f) },
                    fontStyle = FontStyle.Bold
                };

                StatusWarn = new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = new Color(0.95f, 0.75f, 0.2f) },
                    fontStyle = FontStyle.Bold
                };

                BoxArea = new GUIStyle("HelpBox")
                {
                    padding = new RectOffset(10, 10, 8, 8),
                    margin = new RectOffset(4, 4, 4, 4)
                };

                TabButton = new GUIStyle(EditorStyles.toolbarButton)
                {
                    fixedHeight = 28,
                    fontSize = 11,
                    fontStyle = FontStyle.Normal
                };

                TabButtonActive = new GUIStyle(TabButton)
                {
                    fontStyle = FontStyle.Bold
                };

                RichLabel = new GUIStyle(EditorStyles.label) { richText = true };
            }
        }

        // ═══════════════════════════════════════════════════════
        //  State
        // ═══════════════════════════════════════════════════════

        private enum Tab { Player, Enemy, Camera, Layers, Validate, QuickCreate }

        private Tab _currentTab = Tab.Player;
        private Vector2 _scroll;

        // Player Setup
        private GameObject _playerRoot;
        private bool _addCharacterController = true;
        private float _controllerRadius = 0.35f;
        private float _controllerHeight = 1.8f;
        private Vector3 _controllerCenter = new Vector3(0, 0.9f, 0);
        private bool _addCoreComponents = true;
        private bool _addCombatComponents = true;
        private bool _addInputHandler = true;
        private bool _setupHitboxes = true;
        private bool _autoWireDependencies = true;
        private bool _setupAnimController = true;
        private bool _autoFindBones = true;

        // Enemy Setup
        private GameObject _enemyRoot;
        private EnemyData _enemyData;
        private bool _createNewEnemyData;
        private string _newEnemyName = "New Enemy";
        private bool _addEnemyLockOnPoint = true;
        private bool _addEnemyHitboxes = true;

        // Camera Setup
        private GameObject _cameraObject;
        private Transform _cameraTarget;

        // Quick Create
        private string _weaponName = "New Weapon";
        private WeaponType _weaponType = WeaponType.Sword;
        private string _createPath = "Assets/Data/Weapons";

        // Validation
        private List<ValidationEntry> _validationResults = new List<ValidationEntry>();

        // ═══════════════════════════════════════════════════════
        //  Menu
        // ═══════════════════════════════════════════════════════

        [MenuItem("RPGModular/Setup Wizard %#r")]
        static void Open()
        {
            var win = GetWindow<RPGModularSetupWizard>("RPGModular Setup");
            win.minSize = new Vector2(480, 600);
        }

        [MenuItem("RPGModular/Quick Setup Player %#p")]
        static void QuickSetupPlayer()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("RPGModular", "Select a GameObject in the scene first.", "OK");
                return;
            }

            if (EditorUtility.DisplayDialog("RPGModular Quick Setup",
                $"Add all RPGModular Player components to '{selected.name}'?\n\nThis will add:\n" +
                "- CharacterController\n- PlayerController\n- LocomotionStateMachine\n- CombatStateMachine\n" +
                "- CharacterStats\n- HealthSystem\n- WeaponHandler\n- PlayerInputHandler\n" +
                "- LockOnSystem\n- AutoAttackSystem\n- CombatLocomotion\n- HitboxManager\n" +
                "- AnimationController (on Animator child)",
                "Setup", "Cancel"))
            {
                Undo.RegisterCompleteObjectUndo(selected, "RPGModular Quick Setup Player");
                var wizard = CreateInstance<RPGModularSetupWizard>();
                wizard._playerRoot = selected;
                wizard.ExecutePlayerSetup();
                DestroyImmediate(wizard);
                EditorUtility.DisplayDialog("RPGModular", "Player setup complete!", "OK");
            }
        }

        // ═══════════════════════════════════════════════════════
        //  Main GUI
        // ═══════════════════════════════════════════════════════

        private void OnGUI()
        {
            Styles.Init();

            // Title
            GUILayout.Label("RPGModular Setup Wizard", Styles.Title);
            GUILayout.Label("One-click setup for Player, Enemy, Camera & more", Styles.Subtitle);

            DrawSeparator();

            // Tab Bar
            EditorGUILayout.BeginHorizontal();
            foreach (Tab tab in Enum.GetValues(typeof(Tab)))
            {
                bool active = _currentTab == tab;
                var style = active ? Styles.TabButtonActive : Styles.TabButton;
                if (active) GUI.backgroundColor = new Color(0.3f, 0.6f, 1f);
                if (GUILayout.Button(GetTabLabel(tab), style))
                    _currentTab = tab;
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Content
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            switch (_currentTab)
            {
                case Tab.Player: DrawPlayerTab(); break;
                case Tab.Enemy: DrawEnemyTab(); break;
                case Tab.Camera: DrawCameraTab(); break;
                case Tab.Layers: DrawLayersTab(); break;
                case Tab.Validate: DrawValidateTab(); break;
                case Tab.QuickCreate: DrawQuickCreateTab(); break;
            }

            EditorGUILayout.EndScrollView();
        }

        private string GetTabLabel(Tab tab)
        {
            switch (tab)
            {
                case Tab.Player: return "Player";
                case Tab.Enemy: return "Enemy";
                case Tab.Camera: return "Camera";
                case Tab.Layers: return "Layers";
                case Tab.Validate: return "Validate";
                case Tab.QuickCreate: return "Create";
                default: return tab.ToString();
            }
        }

        // ═══════════════════════════════════════════════════════
        //  Player Tab
        // ═══════════════════════════════════════════════════════

        private void DrawPlayerTab()
        {
            GUILayout.Label("Player Setup", Styles.SectionHeader);
            EditorGUILayout.HelpBox(
                "Drag your player root GameObject below. The wizard will add all required components, " +
                "detect the Animator on a child, create hitboxes, find weapon bone slots, and wire everything together.",
                MessageType.Info);

            EditorGUILayout.Space(4);

            // Target
            EditorGUILayout.BeginVertical(Styles.BoxArea);
            _playerRoot = (GameObject)EditorGUILayout.ObjectField("Player Root", _playerRoot, typeof(GameObject), true);

            if (_playerRoot != null)
            {
                EditorGUILayout.Space(2);
                var animator = _playerRoot.GetComponentInChildren<Animator>();
                StatusLabel("Animator", animator != null, animator != null ? $"Found on '{animator.gameObject.name}'" : "No Animator found in children!");
                StatusLabel("CharacterController", _playerRoot.GetComponent<CharacterController>() != null);

                int existingCount = CountExistingPlayerComponents(_playerRoot);
                if (existingCount > 0)
                    EditorGUILayout.HelpBox($"{existingCount} RPGModular component(s) already exist. Existing components will be reused.", MessageType.Warning);
            }
            EditorGUILayout.EndVertical();

            if (_playerRoot == null)
            {
                EditorGUILayout.HelpBox("Select a player GameObject to begin setup.", MessageType.Warning);
                return;
            }

            // Options
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(Styles.BoxArea);
            GUILayout.Label("CharacterController", EditorStyles.boldLabel);
            _addCharacterController = EditorGUILayout.Toggle("Add/Configure CharacterController", _addCharacterController);
            if (_addCharacterController)
            {
                EditorGUI.indentLevel++;
                _controllerRadius = EditorGUILayout.Slider("Radius", _controllerRadius, 0.1f, 1f);
                _controllerHeight = EditorGUILayout.Slider("Height", _controllerHeight, 0.5f, 3f);
                _controllerCenter = EditorGUILayout.Vector3Field("Center", _controllerCenter);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(Styles.BoxArea);
            GUILayout.Label("Components", EditorStyles.boldLabel);
            _addCoreComponents = EditorGUILayout.Toggle("Core (Stats, Health, Locomotion)", _addCoreComponents);
            _addCombatComponents = EditorGUILayout.Toggle("Combat (CombatSM, Weapon, LockOn, AutoAtk)", _addCombatComponents);
            _addInputHandler = EditorGUILayout.Toggle("Input Handler", _addInputHandler);
            _setupAnimController = EditorGUILayout.Toggle("Animation Controller (on Animator child)", _setupAnimController);
            _setupHitboxes = EditorGUILayout.Toggle("Create Hitboxes (MainHand, OffHand, Body)", _setupHitboxes);
            _autoFindBones = EditorGUILayout.Toggle("Auto-Find Weapon Bone Slots", _autoFindBones);
            _autoWireDependencies = EditorGUILayout.Toggle("Auto-Wire All Dependencies", _autoWireDependencies);
            EditorGUILayout.EndVertical();

            // Execute
            EditorGUILayout.Space(8);
            GUI.backgroundColor = new Color(0.3f, 0.85f, 0.3f);
            if (GUILayout.Button("Setup Player", GUILayout.Height(38)))
            {
                Undo.RegisterCompleteObjectUndo(_playerRoot, "RPGModular Setup Player");
                ExecutePlayerSetup();
                EditorUtility.DisplayDialog("RPGModular", "Player setup complete!\n\nCheck the Inspector for all components.", "OK");
            }
            GUI.backgroundColor = Color.white;

            // Component Overview
            if (_playerRoot != null)
            {
                EditorGUILayout.Space(8);
                DrawPlayerComponentOverview(_playerRoot);
            }
        }

        private void ExecutePlayerSetup()
        {
            var root = _playerRoot;
            if (root == null) return;

            // 1. CharacterController
            if (_addCharacterController)
            {
                var cc = EnsureComponent<CharacterController>(root);
                cc.radius = _controllerRadius;
                cc.height = _controllerHeight;
                cc.center = _controllerCenter;
            }

            // 2. Core Components
            if (_addCoreComponents)
            {
                EnsureComponent<CharacterStats>(root);
                EnsureComponent<HealthSystem>(root);
                EnsureComponent<LocomotionStateMachine>(root);
            }

            // 3. Input
            if (_addInputHandler)
            {
                EnsureComponent<PlayerInputHandler>(root);
            }

            // 4. Combat
            if (_addCombatComponents)
            {
                EnsureComponent<CombatStateMachine>(root);
                EnsureComponent<WeaponHandler>(root);
                EnsureComponent<LockOnSystem>(root);
                EnsureComponent<AutoAttackSystem>(root);
                EnsureComponent<CombatLocomotion>(root);
                EnsureComponent<HitboxManager>(root);
            }

            // 5. PlayerController (bridge)
            if (_addCoreComponents && _addCombatComponents)
            {
                EnsureComponent<PlayerController>(root);
            }

            // 6. AnimationController on Animator child
            if (_setupAnimController)
            {
                var animator = root.GetComponentInChildren<Animator>();
                if (animator != null)
                {
                    EnsureComponent<AnimationController>(animator.gameObject);
                }
                else
                {
                    Debug.LogWarning("[RPGModular] No Animator found in children. AnimationController was not added. " +
                                     "Add an Animator to your model child first.");
                }
            }

            // 7. Hitboxes
            if (_setupHitboxes)
            {
                CreateHitboxes(root);
            }

            // 8. Auto-find weapon bones
            if (_autoFindBones)
            {
                AutoFindWeaponBones(root);
            }

            // 9. Wire dependencies
            if (_autoWireDependencies)
            {
                WirePlayerDependencies(root);
            }

            EditorUtility.SetDirty(root);
        }

        private void CreateHitboxes(GameObject root)
        {
            var hitboxManager = root.GetComponent<HitboxManager>();
            if (hitboxManager == null)
                hitboxManager = root.GetComponentInChildren<HitboxManager>();
            if (hitboxManager == null) return;

            var hitboxParent = hitboxManager.gameObject;

            // MainHand Hitbox
            var mainHandHB = FindOrCreateHitbox(hitboxParent, "MainHandHitbox");
            // OffHand Hitbox
            var offHandHB = FindOrCreateHitbox(hitboxParent, "OffHandHitbox");
            // Body Hitbox
            var bodyHB = FindOrCreateHitbox(hitboxParent, "BodyHitbox");

            // Wire to HitboxManager via serialized fields
            SetSerializedField(hitboxManager, "mainHandHitbox", mainHandHB.GetComponent<DamageHitbox>());
            SetSerializedField(hitboxManager, "offHandHitbox", offHandHB.GetComponent<DamageHitbox>());
            SetSerializedField(hitboxManager, "bodyHitbox", bodyHB.GetComponent<DamageHitbox>());
        }

        private GameObject FindOrCreateHitbox(GameObject parent, string name)
        {
            var existing = parent.transform.Find(name);
            if (existing != null) return existing.gameObject;

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            go.transform.SetParent(parent.transform, false);

            // Add collider
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = name == "BodyHitbox" ? new Vector3(0.8f, 1.5f, 0.5f) : new Vector3(0.3f, 0.3f, 0.8f);
            if (name == "BodyHitbox") col.center = new Vector3(0, 0.75f, 0);

            // Add DamageHitbox
            go.AddComponent<DamageHitbox>();

            // Set layer
            int hitboxLayer = LayerMask.NameToLayer(Layers.Hitbox);
            if (hitboxLayer >= 0) go.layer = hitboxLayer;

            go.SetActive(false);
            return go;
        }

        private void AutoFindWeaponBones(GameObject root)
        {
            var weaponHandler = root.GetComponent<WeaponHandler>();
            if (weaponHandler == null) return;

            var allTransforms = root.GetComponentsInChildren<Transform>(true);

            // Common bone name patterns
            string[] rightHandNames = { "Hand.R", "RightHand", "hand_r", "Hand_R", "Bip001 R Hand", "mixamorig:RightHand", "RightHandSlot" };
            string[] leftHandNames = { "Hand.L", "LeftHand", "hand_l", "Hand_L", "Bip001 L Hand", "mixamorig:LeftHand", "LeftHandSlot" };
            string[] spineNames = { "Spine", "Spine1", "Spine2", "spine_01", "Bip001 Spine", "mixamorig:Spine" };

            var mainHand = FindBoneByNames(allTransforms, rightHandNames);
            var offHand = FindBoneByNames(allTransforms, leftHandNames);
            var sheath = FindBoneByNames(allTransforms, spineNames);

            // Create slot children on bones if found
            Transform mainHandSlot = mainHand != null ? FindOrCreateChild(mainHand, "MainHandSlot") : null;
            Transform offHandSlot = offHand != null ? FindOrCreateChild(offHand, "OffHandSlot") : null;
            Transform mainSheath = sheath != null ? FindOrCreateChild(sheath, "MainHandSheath") : null;
            Transform offSheath = sheath != null ? FindOrCreateChild(sheath, "OffHandSheath") : null;

            SetSerializedField(weaponHandler, "mainHandSlot", mainHandSlot);
            SetSerializedField(weaponHandler, "offHandSlot", offHandSlot);
            SetSerializedField(weaponHandler, "mainHandSheath", mainSheath);
            SetSerializedField(weaponHandler, "offHandSheath", offSheath);

            if (mainHand != null)
                Debug.Log($"[RPGModular] Main hand bone: '{mainHand.name}' -> slot created");
            else
                Debug.LogWarning("[RPGModular] Could not find right hand bone. Assign mainHandSlot manually in WeaponHandler.");

            if (offHand != null)
                Debug.Log($"[RPGModular] Off hand bone: '{offHand.name}' -> slot created");

            if (sheath != null)
                Debug.Log($"[RPGModular] Sheath bone: '{sheath.name}' -> sheath slots created");
        }

        private Transform FindBoneByNames(Transform[] allTransforms, string[] names)
        {
            foreach (var name in names)
            {
                var found = allTransforms.FirstOrDefault(t =>
                    string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase));
                if (found != null) return found;
            }

            // Fuzzy match
            foreach (var name in names)
            {
                var found = allTransforms.FirstOrDefault(t =>
                    t.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
                if (found != null) return found;
            }

            return null;
        }

        private Transform FindOrCreateChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing;

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            return go.transform;
        }

        private void WirePlayerDependencies(GameObject root)
        {
            var animController = root.GetComponentInChildren<AnimationController>();
            var stats = root.GetComponent<CharacterStats>();
            var health = root.GetComponent<HealthSystem>();
            var input = root.GetComponent<PlayerInputHandler>();
            var locoSM = root.GetComponent<LocomotionStateMachine>();
            var combatSM = root.GetComponent<CombatStateMachine>();
            var weaponHandler = root.GetComponent<WeaponHandler>();
            var lockOn = root.GetComponent<LockOnSystem>();
            var autoAttack = root.GetComponent<AutoAttackSystem>();
            var combatLoco = root.GetComponent<CombatLocomotion>();
            var hitbox = root.GetComponentInChildren<HitboxManager>();
            var playerCtrl = root.GetComponent<PlayerController>();
            var cc = root.GetComponent<CharacterController>();

            // PlayerController
            if (playerCtrl != null)
            {
                SetSerializedField(playerCtrl, "locomotion", locoSM);
                SetSerializedField(playerCtrl, "combat", combatSM);
                SetSerializedField(playerCtrl, "input", input);
                SetSerializedField(playerCtrl, "weaponHandler", weaponHandler);
                SetSerializedField(playerCtrl, "lockOn", lockOn);
                SetSerializedField(playerCtrl, "health", health);
                SetSerializedField(playerCtrl, "autoAttack", autoAttack);
                SetSerializedField(playerCtrl, "animController", animController);
            }

            // LocomotionStateMachine (uses [field: SerializeField] properties -> backing fields)
            if (locoSM != null)
            {
                SetAutoProperty(locoSM, "AnimController", animController);
                SetAutoProperty(locoSM, "Stats", stats);
                SetAutoProperty(locoSM, "Health", health);
                SetAutoProperty(locoSM, "Input", input);
                SetAutoProperty(locoSM, "Controller", cc);
            }

            // CombatStateMachine (uses [field: SerializeField] properties -> backing fields)
            if (combatSM != null)
            {
                SetAutoProperty(combatSM, "AnimController", animController);
                SetAutoProperty(combatSM, "Stats", stats);
                SetAutoProperty(combatSM, "Health", health);
                SetAutoProperty(combatSM, "Weapons", weaponHandler);
                SetAutoProperty(combatSM, "CombatLoco", combatLoco);
                SetAutoProperty(combatSM, "PlayerInput", input);
                SetAutoProperty(combatSM, "Hitbox", hitbox);
                SetAutoProperty(combatSM, "LockOn", lockOn);
                SetAutoProperty(combatSM, "AutoAttack", autoAttack);
            }

            // HealthSystem
            if (health != null)
            {
                SetSerializedField(health, "stats", stats);
            }

            // WeaponHandler
            if (weaponHandler != null)
            {
                SetSerializedField(weaponHandler, "stats", stats);
                SetSerializedField(weaponHandler, "animController", animController);
            }

            // AutoAttackSystem
            if (autoAttack != null)
            {
                SetSerializedField(autoAttack, "weaponHandler", weaponHandler);
                SetSerializedField(autoAttack, "lockOn", lockOn);
                SetSerializedField(autoAttack, "stats", stats);
                SetSerializedField(autoAttack, "animController", animController);
            }

            // CombatLocomotion
            if (combatLoco != null)
            {
                SetSerializedField(combatLoco, "controller", cc);
                SetSerializedField(combatLoco, "animController", animController);
            }

            // HitboxManager
            if (hitbox != null)
            {
                SetSerializedField(hitbox, "animController", animController);
            }

            // Mark everything dirty
            var allComponents = root.GetComponentsInChildren<Component>(true);
            foreach (var c in allComponents)
            {
                if (c != null) EditorUtility.SetDirty(c);
            }
        }

        private void DrawPlayerComponentOverview(GameObject root)
        {
            EditorGUILayout.BeginVertical(Styles.BoxArea);
            GUILayout.Label("Component Status", Styles.SectionHeader);

            StatusLabel("CharacterController", root.GetComponent<CharacterController>() != null);
            StatusLabel("PlayerController", root.GetComponent<PlayerController>() != null);
            StatusLabel("CharacterStats", root.GetComponent<CharacterStats>() != null);
            StatusLabel("HealthSystem", root.GetComponent<HealthSystem>() != null);
            StatusLabel("LocomotionStateMachine", root.GetComponent<LocomotionStateMachine>() != null);
            StatusLabel("CombatStateMachine", root.GetComponent<CombatStateMachine>() != null);
            StatusLabel("WeaponHandler", root.GetComponent<WeaponHandler>() != null);
            StatusLabel("PlayerInputHandler", root.GetComponent<PlayerInputHandler>() != null);
            StatusLabel("LockOnSystem", root.GetComponent<LockOnSystem>() != null);
            StatusLabel("AutoAttackSystem", root.GetComponent<AutoAttackSystem>() != null);
            StatusLabel("CombatLocomotion", root.GetComponent<CombatLocomotion>() != null);
            StatusLabel("HitboxManager", root.GetComponentInChildren<HitboxManager>() != null);

            EditorGUILayout.Space(2);
            var animCtrl = root.GetComponentInChildren<AnimationController>();
            StatusLabel("AnimationController", animCtrl != null,
                animCtrl != null ? $"on '{animCtrl.gameObject.name}'" : "Missing (needs Animator child)");

            EditorGUILayout.EndVertical();
        }

        private int CountExistingPlayerComponents(GameObject root)
        {
            int count = 0;
            if (root.GetComponent<PlayerController>() != null) count++;
            if (root.GetComponent<CharacterStats>() != null) count++;
            if (root.GetComponent<HealthSystem>() != null) count++;
            if (root.GetComponent<LocomotionStateMachine>() != null) count++;
            if (root.GetComponent<CombatStateMachine>() != null) count++;
            if (root.GetComponent<WeaponHandler>() != null) count++;
            if (root.GetComponent<PlayerInputHandler>() != null) count++;
            if (root.GetComponent<LockOnSystem>() != null) count++;
            if (root.GetComponent<AutoAttackSystem>() != null) count++;
            if (root.GetComponent<CombatLocomotion>() != null) count++;
            if (root.GetComponentInChildren<HitboxManager>() != null) count++;
            if (root.GetComponentInChildren<AnimationController>() != null) count++;
            return count;
        }

        // ═══════════════════════════════════════════════════════
        //  Enemy Tab
        // ═══════════════════════════════════════════════════════

        private void DrawEnemyTab()
        {
            GUILayout.Label("Enemy Setup", Styles.SectionHeader);
            EditorGUILayout.HelpBox(
                "Set up an enemy GameObject with EnemyBase, AnimationController, lock-on point, and hitboxes.",
                MessageType.Info);

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginVertical(Styles.BoxArea);
            _enemyRoot = (GameObject)EditorGUILayout.ObjectField("Enemy Root", _enemyRoot, typeof(GameObject), true);

            if (_enemyRoot != null)
            {
                var animator = _enemyRoot.GetComponentInChildren<Animator>();
                StatusLabel("Animator", animator != null, animator != null ? $"on '{animator.gameObject.name}'" : "Missing!");
                StatusLabel("EnemyBase", _enemyRoot.GetComponent<EnemyBase>() != null);
            }
            EditorGUILayout.EndVertical();

            if (_enemyRoot == null)
            {
                EditorGUILayout.HelpBox("Select an enemy GameObject to begin setup.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(Styles.BoxArea);
            GUILayout.Label("Enemy Data", EditorStyles.boldLabel);

            _createNewEnemyData = EditorGUILayout.Toggle("Create New EnemyData", _createNewEnemyData);

            if (_createNewEnemyData)
            {
                _newEnemyName = EditorGUILayout.TextField("Enemy Name", _newEnemyName);
            }
            else
            {
                _enemyData = (EnemyData)EditorGUILayout.ObjectField("Enemy Data Asset", _enemyData, typeof(EnemyData), false);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(Styles.BoxArea);
            GUILayout.Label("Options", EditorStyles.boldLabel);
            _addEnemyLockOnPoint = EditorGUILayout.Toggle("Create Lock-On Point", _addEnemyLockOnPoint);
            _addEnemyHitboxes = EditorGUILayout.Toggle("Create Hitboxes", _addEnemyHitboxes);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);
            GUI.backgroundColor = new Color(0.85f, 0.3f, 0.3f);
            if (GUILayout.Button("Setup Enemy", GUILayout.Height(38)))
            {
                Undo.RegisterCompleteObjectUndo(_enemyRoot, "RPGModular Setup Enemy");
                ExecuteEnemySetup();
                EditorUtility.DisplayDialog("RPGModular", "Enemy setup complete!", "OK");
            }
            GUI.backgroundColor = Color.white;

            if (_enemyRoot != null)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.BeginVertical(Styles.BoxArea);
                GUILayout.Label("Component Status", Styles.SectionHeader);
                StatusLabel("EnemyBase", _enemyRoot.GetComponent<EnemyBase>() != null);
                StatusLabel("AnimationController", _enemyRoot.GetComponentInChildren<AnimationController>() != null);

                var lockOnPt = _enemyRoot.transform.Find("LockOnPoint");
                StatusLabel("Lock-On Point", lockOnPt != null);

                var hitboxMgr = _enemyRoot.GetComponentInChildren<HitboxManager>();
                StatusLabel("HitboxManager", hitboxMgr != null);
                EditorGUILayout.EndVertical();
            }
        }

        private void ExecuteEnemySetup()
        {
            var root = _enemyRoot;
            if (root == null) return;

            // EnemyData
            EnemyData data = _enemyData;
            if (_createNewEnemyData)
            {
                data = ScriptableObject.CreateInstance<EnemyData>();
                data.nameKey = _newEnemyName;

                string dir = "Assets/Data/Enemies";
                if (!System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);

                string path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{_newEnemyName}.asset");
                AssetDatabase.CreateAsset(data, path);
                AssetDatabase.SaveAssets();
                Debug.Log($"[RPGModular] Created EnemyData: {path}");
            }

            // EnemyBase
            var enemy = EnsureComponent<EnemyBase>(root);
            if (data != null)
                SetSerializedField(enemy, "data", data);

            // Set enemy layer
            int enemyLayer = LayerMask.NameToLayer(Layers.Enemy);
            if (enemyLayer >= 0)
                SetLayerRecursive(root, enemyLayer);

            // AnimationController on Animator child
            var animator = root.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                var animCtrl = EnsureComponent<AnimationController>(animator.gameObject);
                SetSerializedField(enemy, "animController", animCtrl);
            }

            // Lock-On Point
            if (_addEnemyLockOnPoint)
            {
                Transform lockOnPt = root.transform.Find("LockOnPoint");
                if (lockOnPt == null)
                {
                    var go = new GameObject("LockOnPoint");
                    Undo.RegisterCreatedObjectUndo(go, "Create LockOnPoint");
                    go.transform.SetParent(root.transform, false);
                    go.transform.localPosition = new Vector3(0, 1.2f, 0);
                    lockOnPt = go.transform;
                }
                SetSerializedField(enemy, "lockOnPoint", lockOnPt);
            }

            // Hitboxes
            if (_addEnemyHitboxes)
            {
                var hitboxMgr = EnsureComponent<HitboxManager>(root);
                var mainHB = FindOrCreateHitbox(root, "EnemyMainHitbox");
                SetSerializedField(hitboxMgr, "mainHandHitbox", mainHB.GetComponent<DamageHitbox>());

                if (animator != null)
                    SetSerializedField(hitboxMgr, "animController", root.GetComponentInChildren<AnimationController>());
            }

            // Collider for target detection
            var col = root.GetComponent<CapsuleCollider>();
            if (col == null)
            {
                col = root.AddComponent<CapsuleCollider>();
                col.radius = 0.4f;
                col.height = 1.8f;
                col.center = new Vector3(0, 0.9f, 0);
            }

            EditorUtility.SetDirty(root);
        }

        // ═══════════════════════════════════════════════════════
        //  Camera Tab
        // ═══════════════════════════════════════════════════════

        private void DrawCameraTab()
        {
            GUILayout.Label("Camera Setup", Styles.SectionHeader);
            EditorGUILayout.HelpBox(
                "Set up the CameraController on the Main Camera and assign its target. " +
                "You can also create a fresh camera.",
                MessageType.Info);

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginVertical(Styles.BoxArea);
            _cameraObject = (GameObject)EditorGUILayout.ObjectField("Camera Object", _cameraObject, typeof(GameObject), true);
            _cameraTarget = (Transform)EditorGUILayout.ObjectField("Target (Player)", _cameraTarget, typeof(Transform), true);

            // Auto-detect
            if (_cameraObject == null)
            {
                var mainCam = Camera.main;
                if (mainCam != null)
                {
                    EditorGUILayout.HelpBox($"Main Camera detected: '{mainCam.name}'. Click Auto-Detect to use it.", MessageType.Info);
                    if (GUILayout.Button("Auto-Detect Main Camera"))
                        _cameraObject = mainCam.gameObject;
                }
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
            if (GUILayout.Button("Setup Camera", GUILayout.Height(34)))
            {
                if (_cameraObject == null)
                {
                    EditorUtility.DisplayDialog("RPGModular", "Assign a Camera object first.", "OK");
                }
                else
                {
                    Undo.RegisterCompleteObjectUndo(_cameraObject, "RPGModular Setup Camera");
                    var camCtrl = EnsureComponent<CameraController>(_cameraObject);
                    if (_cameraTarget != null)
                        SetSerializedField(camCtrl, "target", _cameraTarget);
                    EditorUtility.SetDirty(_cameraObject);
                    EditorUtility.DisplayDialog("RPGModular", "Camera setup complete!", "OK");
                }
            }

            if (GUILayout.Button("Create New Camera", GUILayout.Height(34)))
            {
                var go = new GameObject("RPGModular_Camera");
                Undo.RegisterCreatedObjectUndo(go, "Create RPGModular Camera");
                go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
                go.AddComponent<CameraController>();
                go.tag = "MainCamera";

                if (_cameraTarget != null)
                {
                    var ctrl = go.GetComponent<CameraController>();
                    SetSerializedField(ctrl, "target", _cameraTarget);
                    go.transform.position = _cameraTarget.position + new Vector3(0, 2, -5);
                }

                _cameraObject = go;
                Selection.activeGameObject = go;
                EditorUtility.DisplayDialog("RPGModular", "Camera created!", "OK");
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        // ═══════════════════════════════════════════════════════
        //  Layers Tab
        // ═══════════════════════════════════════════════════════

        private void DrawLayersTab()
        {
            GUILayout.Label("Layer Setup", Styles.SectionHeader);
            EditorGUILayout.HelpBox(
                "RPGModular requires specific layers for physics queries (lock-on, aggro detection, ground check, hitboxes). " +
                "Click the button to auto-create missing layers.",
                MessageType.Info);

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginVertical(Styles.BoxArea);
            GUILayout.Label("Required Layers", EditorStyles.boldLabel);

            string[] requiredLayers = { Layers.Player, Layers.Enemy, Layers.Ground, Layers.Interactable, Layers.Hitbox };

            foreach (var layer in requiredLayers)
            {
                int idx = LayerMask.NameToLayer(layer);
                StatusLabel(layer, idx >= 0, idx >= 0 ? $"Layer {idx}" : "Not created");
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            GUI.backgroundColor = new Color(0.95f, 0.75f, 0.2f);
            if (GUILayout.Button("Create Missing Layers", GUILayout.Height(34)))
            {
                int created = 0;
                foreach (var layer in requiredLayers)
                {
                    if (LayerMask.NameToLayer(layer) < 0)
                    {
                        if (TryAddLayer(layer))
                            created++;
                    }
                }

                if (created > 0)
                    EditorUtility.DisplayDialog("RPGModular", $"Created {created} layer(s).\n\nRemember to assign layers to your GameObjects.", "OK");
                else
                    EditorUtility.DisplayDialog("RPGModular", "All layers already exist!", "OK");
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(8);

            EditorGUILayout.BeginVertical(Styles.BoxArea);
            GUILayout.Label("Recommended Physics Matrix", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Player", "collides with Enemy, Ground, Interactable");
            EditorGUILayout.LabelField("Enemy", "collides with Player, Ground, Enemy");
            EditorGUILayout.LabelField("Hitbox", "collides with Player, Enemy (triggers only)");
            EditorGUILayout.LabelField("Ground", "collides with Player, Enemy");

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Open Physics Settings"))
            {
                SettingsService.OpenProjectSettings("Project/Physics");
            }
            EditorGUILayout.EndVertical();
        }

        private bool TryAddLayer(string layerName)
        {
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

            SerializedProperty layers = tagManager.FindProperty("layers");

            // Layers 0-7 are built-in; search user layers 8-31
            for (int i = 8; i < 32; i++)
            {
                SerializedProperty layerProp = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(layerProp.stringValue))
                {
                    layerProp.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    Debug.Log($"[RPGModular] Created layer '{layerName}' at index {i}");
                    return true;
                }
            }

            Debug.LogWarning($"[RPGModular] No empty layer slots available for '{layerName}'");
            return false;
        }

        // ═══════════════════════════════════════════════════════
        //  Validate Tab
        // ═══════════════════════════════════════════════════════

        private struct ValidationEntry
        {
            public string Message;
            public MessageType Type;
            public GameObject Context;
        }

        private void DrawValidateTab()
        {
            GUILayout.Label("Scene Validation", Styles.SectionHeader);
            EditorGUILayout.HelpBox(
                "Scan the current scene for RPGModular issues: missing references, broken wiring, missing layers, etc.",
                MessageType.Info);

            EditorGUILayout.Space(4);

            GUI.backgroundColor = new Color(0.6f, 0.4f, 0.9f);
            if (GUILayout.Button("Validate Scene", GUILayout.Height(34)))
            {
                RunValidation();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(4);

            if (_validationResults.Count == 0)
            {
                EditorGUILayout.HelpBox("Click 'Validate Scene' to scan for issues.", MessageType.Info);
                return;
            }

            int errors = _validationResults.Count(v => v.Type == MessageType.Error);
            int warnings = _validationResults.Count(v => v.Type == MessageType.Warning);
            int info = _validationResults.Count(v => v.Type == MessageType.Info);

            EditorGUILayout.BeginVertical(Styles.BoxArea);
            GUILayout.Label($"Results: {errors} errors, {warnings} warnings, {info} info", EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();

            foreach (var entry in _validationResults)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox(entry.Message, entry.Type);
                if (entry.Context != null)
                {
                    if (GUILayout.Button("Select", GUILayout.Width(50), GUILayout.Height(38)))
                        Selection.activeGameObject = entry.Context;
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void RunValidation()
        {
            _validationResults.Clear();

            // Find all RPGModular components in scene
            var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            var combatSMs = FindObjectsByType<CombatStateMachine>(FindObjectsSortMode.None);
            var locoSMs = FindObjectsByType<LocomotionStateMachine>(FindObjectsSortMode.None);
            var enemies = FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
            var cameras = FindObjectsByType<CameraController>(FindObjectsSortMode.None);
            var animControllers = FindObjectsByType<AnimationController>(FindObjectsSortMode.None);
            var healthSystems = FindObjectsByType<HealthSystem>(FindObjectsSortMode.None);
            var hitboxManagers = FindObjectsByType<HitboxManager>(FindObjectsSortMode.None);

            // General checks
            if (players.Length == 0)
                AddValidation("No PlayerController found in scene.", MessageType.Warning);
            if (players.Length > 1)
                AddValidation($"Multiple PlayerControllers found ({players.Length}). Expected exactly 1.", MessageType.Error);
            if (cameras.Length == 0)
                AddValidation("No CameraController found in scene.", MessageType.Warning);

            // Player validation
            foreach (var player in players)
            {
                var go = player.gameObject;
                ValidateFieldNotNull(player, "locomotion", "PlayerController.locomotion (LocomotionStateMachine)");
                ValidateFieldNotNull(player, "combat", "PlayerController.combat (CombatStateMachine)");
                ValidateFieldNotNull(player, "input", "PlayerController.input (PlayerInputHandler)");

                if (go.GetComponent<CharacterController>() == null)
                    AddValidation($"'{go.name}': Missing CharacterController (required by LocomotionStateMachine).", MessageType.Error, go);
            }

            // CombatStateMachine validation
            foreach (var csm in combatSMs)
            {
                var go = csm.gameObject;
                ValidateAutoProperty(csm, "AnimController", $"'{go.name}' CombatStateMachine.AnimController");
                ValidateAutoProperty(csm, "Stats", $"'{go.name}' CombatStateMachine.Stats");
                ValidateAutoProperty(csm, "Health", $"'{go.name}' CombatStateMachine.Health");
            }

            // LocomotionStateMachine validation
            foreach (var lsm in locoSMs)
            {
                ValidateAutoProperty(lsm, "AnimController", $"'{lsm.gameObject.name}' LocomotionStateMachine.AnimController");
            }

            // HealthSystem validation
            foreach (var hs in healthSystems)
            {
                ValidateFieldNotNull(hs, "stats", $"'{hs.gameObject.name}' HealthSystem.stats (CharacterStats)");
            }

            // AnimationController checks
            foreach (var ac in animControllers)
            {
                if (ac.GetComponent<Animator>() == null)
                    AddValidation($"'{ac.gameObject.name}': AnimationController has no Animator on the same GameObject.", MessageType.Error, ac.gameObject);
            }

            // HitboxManager checks
            foreach (var hm in hitboxManagers)
            {
                var mainHB = GetSerializedFieldValue<DamageHitbox>(hm, "mainHandHitbox");
                if (mainHB == null)
                    AddValidation($"'{hm.gameObject.name}': HitboxManager.mainHandHitbox is not assigned.", MessageType.Warning, hm.gameObject);
            }

            // Enemy validation
            foreach (var enemy in enemies)
            {
                var data = GetSerializedFieldValue<EnemyData>(enemy, "data");
                if (data == null)
                    AddValidation($"'{enemy.gameObject.name}': EnemyBase has no EnemyData assigned.", MessageType.Error, enemy.gameObject);
            }

            // Camera validation
            foreach (var cam in cameras)
            {
                var target = GetSerializedFieldValue<Transform>(cam, "target");
                if (target == null)
                    AddValidation($"'{cam.gameObject.name}': CameraController has no target assigned.", MessageType.Error, cam.gameObject);
            }

            // Layer checks
            string[] requiredLayers = { Layers.Player, Layers.Enemy, Layers.Ground };
            foreach (var layer in requiredLayers)
            {
                if (LayerMask.NameToLayer(layer) < 0)
                    AddValidation($"Layer '{layer}' does not exist. Go to Layers tab to create it.", MessageType.Warning);
            }

            if (_validationResults.Count == 0)
                AddValidation("All checks passed! Scene is properly configured.", MessageType.Info);
        }

        private void ValidateFieldNotNull(Component component, string fieldName, string label)
        {
            var value = GetSerializedFieldValue<UnityEngine.Object>(component, fieldName);
            if (value == null)
                AddValidation($"Missing: {label} is not assigned.", MessageType.Error, component.gameObject);
        }

        private void ValidateAutoProperty(Component component, string propName, string label)
        {
            // [field: SerializeField] properties have backing fields named <PropName>k__BackingField
            var type = component.GetType();
            var prop = type.GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop == null) return;

            var value = prop.GetValue(component) as UnityEngine.Object;
            if (value == null)
                AddValidation($"Missing: {label} is not assigned.", MessageType.Error, component.gameObject);
        }

        private void AddValidation(string message, MessageType type, GameObject context = null)
        {
            _validationResults.Add(new ValidationEntry { Message = message, Type = type, Context = context });
        }

        // ═══════════════════════════════════════════════════════
        //  Quick Create Tab
        // ═══════════════════════════════════════════════════════

        private void DrawQuickCreateTab()
        {
            GUILayout.Label("Quick Create", Styles.SectionHeader);

            // Weapon Data
            EditorGUILayout.BeginVertical(Styles.BoxArea);
            GUILayout.Label("Create Weapon Data", EditorStyles.boldLabel);
            _weaponName = EditorGUILayout.TextField("Weapon Name", _weaponName);
            _weaponType = (WeaponType)EditorGUILayout.EnumPopup("Weapon Type", _weaponType);
            _createPath = EditorGUILayout.TextField("Save Path", _createPath);

            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color(0.3f, 0.85f, 0.3f);
            if (GUILayout.Button("Create WeaponData Asset", GUILayout.Height(28)))
            {
                CreateWeaponData();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();

            // Enemy Data
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(Styles.BoxArea);
            GUILayout.Label("Create Enemy Data", EditorStyles.boldLabel);

            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color(0.85f, 0.3f, 0.3f);
            if (GUILayout.Button("Create EnemyData Asset", GUILayout.Height(28)))
            {
                var data = ScriptableObject.CreateInstance<EnemyData>();
                data.nameKey = "New Enemy";

                string dir = "Assets/Data/Enemies";
                if (!System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);

                string path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/NewEnemy.asset");
                AssetDatabase.CreateAsset(data, path);
                AssetDatabase.SaveAssets();
                Selection.activeObject = data;
                EditorGUIUtility.PingObject(data);
                EditorUtility.DisplayDialog("RPGModular", $"Created: {path}", "OK");
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();

            // Prefab Creator
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(Styles.BoxArea);
            GUILayout.Label("Create Prefab Templates", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Quickly spawn template GameObjects in the scene.", EditorStyles.miniLabel);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
            if (GUILayout.Button("Empty Player\nTemplate", GUILayout.Height(40)))
            {
                CreatePlayerTemplate();
            }
            if (GUILayout.Button("Empty Enemy\nTemplate", GUILayout.Height(40)))
            {
                CreateEnemyTemplate();
            }
            if (GUILayout.Button("Camera\nRig", GUILayout.Height(40)))
            {
                CreateCameraRig();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void CreateWeaponData()
        {
            var data = ScriptableObject.CreateInstance<WeaponData>();

            // Set name via serialized field
            var so = new SerializedObject(data);
            var nameProp = so.FindProperty("weaponName");
            if (nameProp != null) nameProp.stringValue = _weaponName;
            var typeProp = so.FindProperty("type");
            if (typeProp != null) typeProp.enumValueIndex = (int)_weaponType;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (!System.IO.Directory.Exists(_createPath))
                System.IO.Directory.CreateDirectory(_createPath);

            string path = AssetDatabase.GenerateUniqueAssetPath($"{_createPath}/{_weaponName}.asset");
            AssetDatabase.CreateAsset(data, path);
            AssetDatabase.SaveAssets();

            Selection.activeObject = data;
            EditorGUIUtility.PingObject(data);
            EditorUtility.DisplayDialog("RPGModular", $"Created: {path}", "OK");
        }

        private void CreatePlayerTemplate()
        {
            var root = new GameObject("Player");
            Undo.RegisterCreatedObjectUndo(root, "Create Player Template");

            // Model placeholder
            var model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            model.AddComponent<Animator>();

            _playerRoot = root;
            ExecutePlayerSetup();

            Selection.activeGameObject = root;
            EditorUtility.DisplayDialog("RPGModular",
                "Player template created!\n\n" +
                "Replace 'Model' child with your actual character model (with Animator).\n" +
                "Then re-run setup to wire the AnimationController.",
                "OK");
        }

        private void CreateEnemyTemplate()
        {
            var root = new GameObject("Enemy");
            Undo.RegisterCreatedObjectUndo(root, "Create Enemy Template");

            var model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            model.AddComponent<Animator>();

            _enemyRoot = root;
            _createNewEnemyData = true;
            _newEnemyName = "Template Enemy";
            ExecuteEnemySetup();

            Selection.activeGameObject = root;
            EditorUtility.DisplayDialog("RPGModular",
                "Enemy template created!\n\n" +
                "Replace 'Model' child with your actual enemy model (with Animator).\n" +
                "Configure EnemyData in the Inspector.",
                "OK");
        }

        private void CreateCameraRig()
        {
            var go = new GameObject("RPGModular_Camera");
            Undo.RegisterCreatedObjectUndo(go, "Create Camera Rig");
            go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
            go.AddComponent<CameraController>();
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0, 3, -6);
            go.transform.rotation = Quaternion.Euler(15, 0, 0);

            // Check for existing main camera
            var existingCam = Camera.main;
            if (existingCam != null && existingCam.gameObject != go)
            {
                if (EditorUtility.DisplayDialog("RPGModular",
                    $"A Main Camera already exists: '{existingCam.name}'.\n\nDisable the old camera?",
                    "Disable Old", "Keep Both"))
                {
                    Undo.RecordObject(existingCam.gameObject, "Disable old camera");
                    existingCam.gameObject.SetActive(false);
                }
            }

            _cameraObject = go;
            Selection.activeGameObject = go;
            EditorUtility.DisplayDialog("RPGModular", "Camera rig created!", "OK");
        }

        // ═══════════════════════════════════════════════════════
        //  Utilities
        // ═══════════════════════════════════════════════════════

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var existing = go.GetComponent<T>();
            if (existing != null) return existing;
            return Undo.AddComponent<T>(go);
        }

        private static void SetSerializedField(Component component, string fieldName, UnityEngine.Object value)
        {
            if (component == null || value == null) return;

            var so = new SerializedObject(component);
            var prop = so.FindProperty(fieldName);
            if (prop != null && prop.propertyType == SerializedPropertyType.ObjectReference)
            {
                prop.objectReferenceValue = value;
                so.ApplyModifiedProperties();
            }
        }

        private static void SetAutoProperty(Component component, string propertyName, UnityEngine.Object value)
        {
            if (component == null || value == null) return;

            // [field: SerializeField] properties have backing fields: <PropertyName>k__BackingField
            var so = new SerializedObject(component);
            string backingFieldName = $"<{propertyName}>k__BackingField";
            var prop = so.FindProperty(backingFieldName);
            if (prop != null && prop.propertyType == SerializedPropertyType.ObjectReference)
            {
                prop.objectReferenceValue = value;
                so.ApplyModifiedProperties();
            }
        }

        private static T GetSerializedFieldValue<T>(Component component, string fieldName) where T : UnityEngine.Object
        {
            if (component == null) return null;
            var so = new SerializedObject(component);
            var prop = so.FindProperty(fieldName);
            if (prop != null && prop.propertyType == SerializedPropertyType.ObjectReference)
                return prop.objectReferenceValue as T;
            return null;
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        private static void StatusLabel(string label, bool ok, string detail = null)
        {
            EditorGUILayout.BeginHorizontal();
            var style = ok ? Styles.StatusOK : Styles.StatusMissing;
            GUILayout.Label(ok ? "[OK]" : "[--]", style, GUILayout.Width(32));
            GUILayout.Label(label, GUILayout.Width(200));
            if (!string.IsNullOrEmpty(detail))
                GUILayout.Label(detail, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawSeparator()
        {
            EditorGUILayout.Space(2);
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f, 0.5f));
            EditorGUILayout.Space(2);
        }
    }
}
#endif
