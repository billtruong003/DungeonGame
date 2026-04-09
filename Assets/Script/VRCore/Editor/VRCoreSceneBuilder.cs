#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.XR;
using VRCore.Hand;
using VRCore.Input;
using VRCore.Interaction;
using VRCore.Interaction.Gadgets;
using VRCore.Tracking;
using VRCore.Locomotion;

namespace VRCore.Editor
{
    public static class VRCoreSceneBuilder
    {
        [MenuItem("VRCore/Apply VR Performance Settings", priority = 42)]
        public static void ApplyVRPerformanceSettings()
        {
            Application.targetFrameRate = 120;
            QualitySettings.vSyncCount = 0;
            Time.fixedDeltaTime = 0.01111f;
            Debug.Log("[VRCore] Performance: 120fps target, vSync off, physics 90Hz.");
        }

        public static GameObject BuildPlayerRig()
        {
            var existing = Object.FindFirstObjectByType<VRCoreBootstrap>();
            if (existing != null)
            {
                Debug.LogWarning("[VRCore] Player rig already exists in scene.");
                return existing.gameObject;
            }

            ApplyVRPerformanceSettings();

            var root = new GameObject("[VRCore] Player");
            Undo.RegisterCreatedObjectUndo(root, "Create VRCore Player");
            root.transform.position = Vector3.zero;

            root.AddComponent<VRCoreBootstrap>();

            var playerBody = root.AddComponent<VRPlayerBody>();

            var trackingContainer = new GameObject("TrackingSpace");
            trackingContainer.transform.SetParent(root.transform, false);

            var cameraOffset = new GameObject("CameraOffset");
            cameraOffset.transform.SetParent(trackingContainer.transform, false);
            cameraOffset.transform.localPosition = new Vector3(0f, 1.6f, 0f);

            var cameraGo = new GameObject("Main Camera");
            cameraGo.transform.SetParent(cameraOffset.transform, false);
            cameraGo.tag = "MainCamera";
            cameraGo.AddComponent<Camera>();
            cameraGo.AddComponent<TrackedHeadDriver>();
            cameraGo.AddComponent<AudioListener>();

            RemoveExistingMainCameras(cameraGo);

            var leftController = CreateControllerTarget("LeftController", cameraOffset.transform,
                new Vector3(-0.2f, -0.1f, 0.3f), XRNode.LeftHand);
            var rightController = CreateControllerTarget("RightController", cameraOffset.transform,
                new Vector3(0.2f, -0.1f, 0.3f), XRNode.RightHand);

            var leftHand = CreateHand("LeftHand", HandSide.Left, leftController.transform, cameraOffset.transform);
            var rightHand = CreateHand("RightHand", HandSide.Right, rightController.transform, cameraOffset.transform);

            SetupLocomotion(root, cameraOffset.transform);

            root.AddComponent<VRCore.DebugTools.VRCoreDebugOverlay>();

            SerializedObject bodyObj = new SerializedObject(playerBody);
            SetSerializedField(bodyObj, "headCamera", cameraGo.transform);
            SetSerializedField(bodyObj, "trackingContainer", trackingContainer.transform);
            bodyObj.ApplyModifiedProperties();

            CreateHighlightMaterial();

            Selection.activeGameObject = root;
            Debug.Log("[VRCore] Player rig created with full locomotion.");
            return root;
        }

        private static void SetupLocomotion(GameObject root, Transform cameraOffset)
        {
            var locoGo = new GameObject("Locomotion");
            locoGo.transform.SetParent(root.transform, false);

            locoGo.AddComponent<LocomotionStateMachine>();
            locoGo.AddComponent<JoystickMoveProvider>();

            var teleport = locoGo.AddComponent<TeleportProvider>();
            var teleportLine = locoGo.AddComponent<LineRenderer>();
            teleportLine.startWidth = 0.01f;
            teleportLine.endWidth = 0.005f;
            teleportLine.material = new Material(Shader.Find("Sprites/Default"));
            teleportLine.enabled = false;

            locoGo.AddComponent<SnapTurnProvider>();
            locoGo.AddComponent<ClimbProvider>();
        }

        private static GameObject CreateControllerTarget(string name, Transform parent, Vector3 localPos, XRNode node)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            var driver = go.AddComponent<TrackedControllerDriver>();
            driver.SetNode(node);

            return go;
        }

        private static GameObject CreateHand(string name, HandSide side, Transform followTarget, Transform parent)
        {
            var handGo = new GameObject(name);
            handGo.transform.SetParent(parent, false);
            handGo.transform.localPosition = followTarget.localPosition;
            handGo.layer = LayerMask.NameToLayer("HandPhysics");
            if (handGo.layer == -1) handGo.layer = 0;

            var rb = handGo.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = false;
            rb.mass = 1f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var collider = handGo.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.08f, 0.03f, 0.1f);
            collider.center = new Vector3(0f, -0.01f, 0.02f);

            var palmGo = new GameObject("Palm");
            palmGo.transform.SetParent(handGo.transform, false);
            palmGo.transform.localPosition = new Vector3(0f, -0.02f, 0.05f);
            palmGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var hand = handGo.AddComponent<VRHand>();
            hand.SetSide(side);
            hand.SetFollowTarget(followTarget);
            hand.SetPalmTransform(palmGo.transform);
            EditorUtility.SetDirty(hand);

            handGo.AddComponent<GrabHandler>();
            handGo.AddComponent<HandHighlighter>();
            handGo.AddComponent<HandHaptics>();
            handGo.AddComponent<DistanceGrabber>();

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "HandVisual";
            visual.transform.SetParent(handGo.transform, false);
            visual.transform.localScale = new Vector3(0.08f, 0.03f, 0.1f);
            Object.DestroyImmediate(visual.GetComponent<BoxCollider>());

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = side == HandSide.Left ? new Color(0.2f, 0.5f, 1f) : new Color(1f, 0.3f, 0.3f);
            visual.GetComponent<MeshRenderer>().sharedMaterial = mat;

            return handGo;
        }

        [MenuItem("VRCore/Create Test Grabbables", priority = 21)]
        public static void MenuCreateTestGrabbables() => CreateTestGrabbables();

        public static void CreateTestGrabbables(int count = 5)
        {
            float spacing = 0.3f;
            float startX = -(count - 1) * spacing * 0.5f;

            for (int i = 0; i < count; i++)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"Grab_Basic_{i}";
                cube.transform.position = new Vector3(startX + i * spacing, 1f, 0.5f);
                cube.transform.localScale = Vector3.one * 0.1f;
                SetGrabbableLayer(cube);

                var rb = cube.AddComponent<Rigidbody>();
                rb.mass = 0.5f;
                rb.linearDamping = 0.5f;
                cube.AddComponent<Grabbable>();

                ColorObject(cube, (float)i / count);
                Undo.RegisterCreatedObjectUndo(cube, "Create Test Grabbable");
            }

            Debug.Log($"[VRCore] Created {count} basic grabbables.");
        }

        [MenuItem("VRCore/Create Diverse Test Objects", priority = 22)]
        public static void CreateDiverseTestObjects()
        {
            float z = 0.6f;

            CreateHeavyCube(new Vector3(-1f, 1f, z));
            CreateHeavyCube(new Vector3(-0.7f, 1f, z), 5f);

            CreateLightSphere(new Vector3(-0.3f, 1f, z));
            CreateLightSphere(new Vector3(0f, 1f, z), 0.05f);

            CreateTwoHandBar(new Vector3(0.5f, 1f, z));

            CreatePhysicsButtonObj(new Vector3(-1f, 0.9f, z + 0.5f));
            CreatePhysicsLeverObj(new Vector3(-0.5f, 0.9f, z + 0.5f));
            CreatePhysicsDialObj(new Vector3(0f, 0.9f, z + 0.5f));
            CreatePhysicsSliderObj(new Vector3(0.5f, 0.9f, z + 0.5f));

            CreateClimbWall(new Vector3(2f, 0f, 1f));

            CreateTable(new Vector3(0f, 0f, 0.5f));

            Debug.Log("[VRCore] Created diverse test objects.");
        }

        private static void CreateHeavyCube(Vector3 pos, float mass = 2f)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = $"Grab_Heavy_{mass}kg";
            cube.transform.position = pos;
            cube.transform.localScale = Vector3.one * 0.15f;
            SetGrabbableLayer(cube);

            var rb = cube.AddComponent<Rigidbody>();
            rb.mass = mass;
            cube.AddComponent<Grabbable>();

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.4f, 0.4f, 0.4f);
            cube.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Undo.RegisterCreatedObjectUndo(cube, "Heavy Cube");
        }

        private static void CreateLightSphere(Vector3 pos, float scale = 0.08f)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Grab_LightSphere";
            sphere.transform.position = pos;
            sphere.transform.localScale = Vector3.one * scale;
            SetGrabbableLayer(sphere);

            var rb = sphere.AddComponent<Rigidbody>();
            rb.mass = 0.1f;
            sphere.AddComponent<Grabbable>();

            ColorObject(sphere, 0.6f);
            Undo.RegisterCreatedObjectUndo(sphere, "Light Sphere");
        }

        private static void CreateTwoHandBar(Vector3 pos)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bar.name = "Grab_TwoHand_Bar";
            bar.transform.position = pos;
            bar.transform.localScale = new Vector3(0.04f, 0.2f, 0.04f);
            bar.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            SetGrabbableLayer(bar);

            var rb = bar.AddComponent<Rigidbody>();
            rb.mass = 1f;
            bar.AddComponent<TwoHandGrabbable>();

            var gripL = new GameObject("GripLeft");
            gripL.transform.SetParent(bar.transform, false);
            gripL.transform.localPosition = new Vector3(0f, -0.12f, 0f);

            var gripR = new GameObject("GripRight");
            gripR.transform.SetParent(bar.transform, false);
            gripR.transform.localPosition = new Vector3(0f, 0.12f, 0f);

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.8f, 0.6f, 0.2f);
            bar.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Undo.RegisterCreatedObjectUndo(bar, "Two Hand Bar");
        }

        private static void CreatePhysicsButtonObj(Vector3 pos)
        {
            var buttonBase = new GameObject("Gadget_Button");
            buttonBase.transform.position = pos;

            var baseCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseCube.name = "Base";
            baseCube.transform.SetParent(buttonBase.transform, false);
            baseCube.transform.localScale = new Vector3(0.1f, 0.02f, 0.1f);

            var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cap.name = "ButtonCap";
            cap.transform.SetParent(buttonBase.transform, false);
            cap.transform.localPosition = new Vector3(0f, 0.025f, 0f);
            cap.transform.localScale = new Vector3(0.06f, 0.015f, 0.06f);
            SetGrabbableLayer(cap);

            var rb = cap.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.mass = 0.1f;
            cap.AddComponent<Grabbable>();
            cap.AddComponent<PhysicsButton>();

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = Color.red;
            cap.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Undo.RegisterCreatedObjectUndo(buttonBase, "Physics Button");
        }

        private static void CreatePhysicsLeverObj(Vector3 pos)
        {
            var leverBase = new GameObject("Gadget_Lever");
            leverBase.transform.position = pos;

            var pivotBase = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pivotBase.name = "Pivot";
            pivotBase.transform.SetParent(leverBase.transform, false);
            pivotBase.transform.localScale = new Vector3(0.04f, 0.04f, 0.04f);

            var handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            handle.name = "Handle";
            handle.transform.SetParent(leverBase.transform, false);
            handle.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            handle.transform.localScale = new Vector3(0.02f, 0.08f, 0.02f);
            SetGrabbableLayer(handle);

            var rb = handle.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.mass = 0.3f;
            var grab = handle.AddComponent<Grabbable>();
            grab.SetParentOnGrab(false);

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.2f, 0.7f, 0.3f);
            handle.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Undo.RegisterCreatedObjectUndo(leverBase, "Physics Lever");
        }

        private static void CreatePhysicsDialObj(Vector3 pos)
        {
            var dial = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dial.name = "Gadget_Dial";
            dial.transform.position = pos;
            dial.transform.localScale = new Vector3(0.08f, 0.015f, 0.08f);
            SetGrabbableLayer(dial);

            var rb = dial.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.mass = 0.2f;
            var grab = dial.AddComponent<Grabbable>();
            grab.SetParentOnGrab(false);

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.3f, 0.3f, 0.8f);
            dial.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Undo.RegisterCreatedObjectUndo(dial, "Physics Dial");
        }

        private static void CreatePhysicsSliderObj(Vector3 pos)
        {
            var sliderBase = new GameObject("Gadget_Slider");
            sliderBase.transform.position = pos;

            var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rail.name = "Rail";
            rail.transform.SetParent(sliderBase.transform, false);
            rail.transform.localScale = new Vector3(0.3f, 0.01f, 0.04f);

            var handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.name = "SliderHandle";
            handle.transform.SetParent(sliderBase.transform, false);
            handle.transform.localScale = new Vector3(0.04f, 0.04f, 0.04f);
            SetGrabbableLayer(handle);

            var rb = handle.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.mass = 0.2f;
            var grab = handle.AddComponent<Grabbable>();
            grab.SetParentOnGrab(false);

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.8f, 0.8f, 0.2f);
            handle.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Undo.RegisterCreatedObjectUndo(sliderBase, "Physics Slider");
        }

        private static void CreateClimbWall(Vector3 pos)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "ClimbWall";
            wall.transform.position = pos;
            wall.transform.localScale = new Vector3(2f, 3f, 0.2f);

            int layer = LayerMask.NameToLayer("Grabbable");
            wall.layer = layer != -1 ? layer : 0;

            var grab = wall.AddComponent<Grabbable>();
            grab.SetParentOnGrab(false);
            grab.SetSingleHandOnly(false);
            wall.AddComponent<Climbable>();

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.5f, 0.35f, 0.2f);
            wall.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Undo.RegisterCreatedObjectUndo(wall, "Climb Wall");
        }

        private static void CreateTable(Vector3 pos)
        {
            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "Table";
            table.transform.position = pos + Vector3.up * 0.45f;
            table.transform.localScale = new Vector3(1.5f, 0.05f, 0.8f);

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.6f, 0.5f, 0.35f);
            table.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Undo.RegisterCreatedObjectUndo(table, "Table");
        }

        public static void CreateFloor()
        {
            var existing = GameObject.Find("Floor");
            if (existing != null) return;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(5f, 1f, 5f);
            Undo.RegisterCreatedObjectUndo(floor, "Create Floor");
        }

        private static void SetGrabbableLayer(GameObject go)
        {
            int layer = LayerMask.NameToLayer("Grabbable");
            go.layer = layer != -1 ? layer : 0;
        }

        private static void ColorObject(GameObject go, float hue)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = Color.HSVToRGB(hue, 0.7f, 0.9f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private static void RemoveExistingMainCameras(GameObject exclude)
        {
            var cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var cam in cameras)
            {
                if (cam.gameObject != exclude && cam.CompareTag("MainCamera"))
                    Undo.DestroyObjectImmediate(cam.gameObject);
            }
        }

        private static void CreateHighlightMaterial()
        {
            string matPath = "Assets/VRCore/Data/HighlightMaterial.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(matPath) != null) return;

            var shader = Shader.Find("VRCore/OutlineHighlight");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");

            var mat = new Material(shader);
            mat.color = new Color(0.3f, 0.8f, 1f, 0.6f);

            if (!System.IO.Directory.Exists("Assets/VRCore/Data"))
                System.IO.Directory.CreateDirectory("Assets/VRCore/Data");

            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();
        }

        private static void SetSerializedField(SerializedObject obj, string fieldName, Object value)
        {
            var prop = obj.FindProperty(fieldName);
            if (prop != null)
            {
                prop.objectReferenceValue = value;
            }
        }
    }
}
#endif