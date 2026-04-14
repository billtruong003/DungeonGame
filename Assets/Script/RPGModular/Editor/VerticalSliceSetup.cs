#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.AI;
using RPGModular.Editor;

namespace RPGModular.Testing
{
    /// <summary>
    /// One-click Vertical Slice scene setup.
    /// Menu: RPG > Testing > Setup Vertical Slice Scene
    ///
    /// Tao ra:
    /// - Player (full components)
    /// - Singletons (DialogueSystem, ShopService, LootSystem, v.v.)
    /// - 3 Dummy enemies (Standing, Aggressive, Boss)
    /// - 1 NPC Quest Giver
    /// - 1 NPC Trainer
    /// - 1 NPC Merchant
    /// - NavMesh Surface
    /// - Ground plane
    /// </summary>
    public static class VerticalSliceSetup
    {
        [MenuItem("RPG/Testing/Setup Vertical Slice Scene", false, 300)]
        public static void SetupScene()
        {
            // ═══════════════════════════════════════════════════════
            // 1. Ground
            // ═══════════════════════════════════════════════════════
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(10, 1, 10);
            ground.transform.position = Vector3.zero;
            Undo.RegisterCreatedObjectUndo(ground, "Create Ground");

            // ═══════════════════════════════════════════════════════
            // 2. Player
            // ═══════════════════════════════════════════════════════
            var playerGO = new GameObject("Player");
            playerGO.tag = "Player";
            playerGO.transform.position = new Vector3(0, 0, 0);
            Undo.RegisterCreatedObjectUndo(playerGO, "Create Player");

            // Player model placeholder
            var playerModel = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playerModel.name = "Model";
            playerModel.transform.SetParent(playerGO.transform);
            playerModel.transform.localPosition = new Vector3(0, 1f, 0);
            Object.DestroyImmediate(playerModel.GetComponent<Collider>());

            // Run mega setup
            Selection.activeGameObject = playerGO;
            RPGMegaSetup.SetupPlayer();

            // Quest Tracker
            if (playerGO.GetComponent<QuestTracker>() == null)
                Undo.AddComponent<QuestTracker>(playerGO);

            // ═══════════════════════════════════════════════════════
            // 3. Singletons
            // ═══════════════════════════════════════════════════════
            RPGMegaSetup.SetupSingletons();

            // ═══════════════════════════════════════════════════════
            // 4. Dummy Enemies
            // ═══════════════════════════════════════════════════════
            CreateDummy("DummyEnemy_Standing", new Vector3(5, 0, 5),
                DummyEnemy_VerticalSlice.DummyMode.StandingDummy, Color.blue);

            CreateDummy("DummyEnemy_Aggressive", new Vector3(-5, 0, 5),
                DummyEnemy_VerticalSlice.DummyMode.AggressiveAI, Color.red);

            CreateDummy("DummyEnemy_Boss", new Vector3(0, 0, 15),
                DummyEnemy_VerticalSlice.DummyMode.BossTest, new Color(0.5f, 0, 0.5f));

            // ═══════════════════════════════════════════════════════
            // 5. NPCs
            // ═══════════════════════════════════════════════════════
            CreateNPCPlaceholder("NPC_QuestGiver", new Vector3(-8, 0, 0), Color.green, NPCRole.QuestGiver);
            CreateNPCPlaceholder("NPC_Trainer", new Vector3(-10, 0, 0), Color.cyan, NPCRole.Trainer);
            CreateNPCPlaceholder("NPC_Merchant", new Vector3(-12, 0, 0), Color.yellow, NPCRole.Merchant);

            // ═══════════════════════════════════════════════════════
            // 6. Directional Light
            // ═══════════════════════════════════════════════════════
            if (Object.FindFirstObjectByType<Light>() == null)
            {
                var lightGO = new GameObject("Directional Light");
                var light = lightGO.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1f;
                lightGO.transform.rotation = Quaternion.Euler(50, -30, 0);
                Undo.RegisterCreatedObjectUndo(lightGO, "Create Light");
            }

            Debug.Log("[VerticalSlice] Scene setup hoan tat! Bake NavMesh truoc khi Play (Window > AI > Navigation).");
            EditorUtility.DisplayDialog("Vertical Slice",
                "Scene da duoc setup!\n\n" +
                "BUOC TIEP THEO:\n" +
                "1. Bake NavMesh: Window > AI > Navigation > Bake\n" +
                "2. Tao WeaponData: Right-click > Create > RPG > Weapon Data\n" +
                "3. Keo weapon vao WeaponHandler tren Player\n" +
                "4. Play & test!", "OK");
        }

        private static void CreateDummy(string name, Vector3 pos,
            DummyEnemy_VerticalSlice.DummyMode mode, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = new Vector3(1, 1, 1);

            // Set color
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Standard"));
                mat.color = color;
                renderer.material = mat;
            }

            // Components
            var nav = go.AddComponent<NavMeshAgent>();
            nav.speed = 3.5f;
            nav.stoppingDistance = 2f;

            var dummy = go.AddComponent<DummyEnemy_VerticalSlice>();

            // Set mode via serialized field
            var so = new SerializedObject(dummy);
            so.FindProperty("mode").enumValueIndex = (int)mode;
            so.FindProperty("normalColor").colorValue = color;
            so.ApplyModifiedProperties();

            // LockOn point
            var lockOn = new GameObject("LockOnPoint");
            lockOn.transform.SetParent(go.transform);
            lockOn.transform.localPosition = new Vector3(0, 1.2f, 0);

            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        }

        private static void CreateNPCPlaceholder(string name, Vector3 pos, Color color, NPCRole role)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.position = pos;

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Standard"));
                mat.color = color;
                renderer.material = mat;
            }

            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");

            Debug.Log($"[VerticalSlice] NPC '{name}' ({role}) da duoc tao. " +
                      $"Can tao NPCData asset (Create > Game > NPC Data) roi gan vao.");
        }
    }
}
#endif
