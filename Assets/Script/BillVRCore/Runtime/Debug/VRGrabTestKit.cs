using UnityEngine;
using BillVRCore.Hand;
using BillVRCore.Interaction;

namespace BillVRCore.DebugTools
{
    /// <summary>
    /// Drop this on any GameObject to spawn a grab test environment at runtime.
    /// Press T to toggle the diagnostic overlay. Press Y to respawn all test objects.
    /// </summary>
    public class VRGrabTestKit : MonoBehaviour
    {
        [Header("Test Objects")]
        [SerializeField] private Vector3 spawnCenter = new(0f, 1f, 0.6f);
        [SerializeField] private bool spawnOnStart = true;

        [Header("Diagnostics")]
        [SerializeField] private bool showDiagnostics = true;
        [SerializeField] private KeyCode toggleKey = KeyCode.T;
        [SerializeField] private KeyCode respawnKey = KeyCode.Y;
        [SerializeField] private int diagnosticFontSize = 12;

        private GameObject _testRoot;
        private GUIStyle _diagStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _warnStyle;

        private string _leftDiag = "";
        private string _rightDiag = "";
        private string _sceneDiag = "";
        private float _lastDiagTime;

        private VRHand _leftHand;
        private VRHand _rightHand;

        private void Start()
        {
            if (spawnOnStart)
                SpawnTestObjects();
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(toggleKey))
                showDiagnostics = !showDiagnostics;
            if (UnityEngine.Input.GetKeyDown(respawnKey))
                SpawnTestObjects();

            if (_leftHand == null || _rightHand == null)
                FindHands();

            if (showDiagnostics && Time.unscaledTime - _lastDiagTime > 0.1f)
            {
                _lastDiagTime = Time.unscaledTime;
                RebuildDiagnostics();
            }
        }

        private void OnGUI()
        {
            if (!showDiagnostics) return;

            if (_diagStyle == null)
            {
                _diagStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = diagnosticFontSize,
                    normal = { textColor = Color.white },
                    richText = true
                };
                _headerStyle = new GUIStyle(_diagStyle) { fontSize = diagnosticFontSize + 2 };
                _warnStyle = new GUIStyle(_diagStyle) { normal = { textColor = new Color(1f, 0.6f, 0.2f) } };
            }

            float y = Screen.height - 420;
            float x = 10;
            float w = 380;

            // Background
            GUI.Box(new Rect(x - 4, y - 4, w + 8, 416), "");

            GUI.Label(new Rect(x, y, w, 24), "<color=yellow><b>GRAB TEST KIT</b></color>", _headerStyle);
            y += 22;

            GUI.Label(new Rect(x, y, w, 200), _leftDiag, _diagStyle);
            y += CountLines(_leftDiag) * (diagnosticFontSize + 2) + 4;

            GUI.Label(new Rect(x, y, w, 200), _rightDiag, _diagStyle);
            y += CountLines(_rightDiag) * (diagnosticFontSize + 2) + 4;

            GUI.Label(new Rect(x, y, w, 200), _sceneDiag, _diagStyle);
        }

        private void FindHands()
        {
            var hands = FindObjectsByType<VRHand>(FindObjectsSortMode.None);
            foreach (var h in hands)
            {
                if (h.Side == HandSide.Left) _leftHand = h;
                else _rightHand = h;
            }
        }

        private void RebuildDiagnostics()
        {
            _leftDiag = BuildHandDiag(_leftHand, "LEFT");
            _rightDiag = BuildHandDiag(_rightHand, "RIGHT");
            _sceneDiag = BuildSceneDiag();
        }

        private string BuildHandDiag(VRHand hand, string label)
        {
            if (hand == null) return $"<color=red>{label}: Not Found</color>\n";

            var sb = new System.Text.StringBuilder(256);
            var gh = hand.GrabHandler;

            string stateColor = gh.State switch
            {
                GrabState.Grabbing => "lime",
                GrabState.Hovering => "yellow",
                _ => "white"
            };

            sb.Append($"<color={stateColor}><b>{label} [{gh.State}]</b></color>\n");
            sb.Append($"  Mass: {hand.Rb.mass:F2}kg | Vel: {hand.Speed:F1}m/s\n");
            sb.Append($"  Pos: {hand.transform.position:F2}\n");

            if (hand.FollowTarget != null)
            {
                float followDist = Vector3.Distance(hand.transform.position, hand.FollowTarget.position);
                string distColor = followDist > 0.1f ? "red" : (followDist > 0.03f ? "yellow" : "white");
                sb.Append($"  Follow dist: <color={distColor}>{followDist:F3}m</color>\n");
            }

            if (gh.IsHolding && gh.HeldObject != null)
            {
                var held = gh.HeldObject;
                sb.Append($"  <color=lime>Holding: {held.name}</color>\n");
                sb.Append($"    Obj mass: {held.Rb.mass:F2}kg\n");
                sb.Append($"    Obj vel: {held.Rb.linearVelocity.magnitude:F1}m/s\n");
                sb.Append($"    Gravity: {(held.Rb.useGravity ? "ON" : "OFF")}\n");

                float massRatio = hand.Rb.mass / Mathf.Max(held.Rb.mass, 0.001f);
                string ratioColor = massRatio < 0.5f ? "red" : (massRatio < 1f ? "yellow" : "lime");
                sb.Append($"    Mass ratio (hand/obj): <color={ratioColor}>{massRatio:F2}</color>\n");

                // Velocity tracking info
                float separation = Vector3.Distance(hand.FollowTarget != null ? hand.FollowTarget.position : hand.transform.position, held.transform.position);
                string sepColor = separation > 0.15f ? "red" : (separation > 0.05f ? "yellow" : "white");
                string modeStr = held.GrabMode == GrabMode.Default ? $"default({gh.State})" : held.GrabMode.ToString();
                sb.Append($"    Mode: {modeStr} | Tracking: <color=lime>velocity</color>\n");
                sb.Append($"    Separation: <color={sepColor}>{separation:F3}m</color>\n");
                var tracker = held.GetComponent<GrabCollisionTracker>();
                sb.Append($"    Contacts: {(tracker != null && tracker.HasContact ? $"<color=yellow>{tracker.ContactCount}</color>" : "0")}\n");
            }
            else if (gh.HoveredObject != null)
            {
                sb.Append($"  <color=yellow>Hover: {gh.HoveredObject.name}</color>\n");
            }

            return sb.ToString();
        }

        private string BuildSceneDiag()
        {
            var sb = new System.Text.StringBuilder(128);
            sb.Append("<color=cyan><b>Scene</b></color>\n");
            sb.Append($"  Physics rate: {(1f / Time.fixedDeltaTime):F0}Hz\n");

            var grabbables = FindObjectsByType<Grabbable>(FindObjectsSortMode.None);
            int heldCount = 0;
            foreach (var g in grabbables)
                if (g.IsHeld) heldCount++;
            sb.Append($"  Grabbables: {grabbables.Length} (held: {heldCount})\n");
            sb.Append($"  [T] toggle | [Y] respawn\n");

            return sb.ToString();
        }

        public void SpawnTestObjects()
        {
            if (_testRoot != null)
                Destroy(_testRoot);

            _testRoot = new GameObject("[GrabTestKit] Objects");
            _testRoot.transform.position = spawnCenter;

            int grabbableLayer = LayerMask.NameToLayer("Grabbable");

            // Row 1: Mass test (increasing mass)
            float[] masses = { 0.1f, 0.5f, 1f, 2f, 5f };
            for (int i = 0; i < masses.Length; i++)
            {
                float x = (i - 2) * 0.2f;
                var obj = CreateTestCube(
                    $"Mass_{masses[i]:F1}kg",
                    new Vector3(x, 0f, 0f),
                    Vector3.one * 0.08f,
                    masses[i],
                    Color.Lerp(Color.green, Color.red, i / (float)(masses.Length - 1)),
                    grabbableLayer
                );
                AddLabel(obj, $"{masses[i]:F1}kg");
            }

            // Row 2: Size test (same mass, different sizes)
            float[] scales = { 0.04f, 0.08f, 0.12f, 0.18f, 0.25f };
            for (int i = 0; i < scales.Length; i++)
            {
                float x = (i - 2) * 0.3f;
                var obj = CreateTestCube(
                    $"Size_{scales[i]:F2}",
                    new Vector3(x, 0f, -0.35f),
                    Vector3.one * scales[i],
                    0.5f,
                    Color.Lerp(Color.cyan, Color.blue, i / (float)(scales.Length - 1)),
                    grabbableLayer
                );
                AddLabel(obj, $"{scales[i]:F2}m");
            }

            // Row 3: Special cases
            // Sphere (rolling)
            var sphere = CreateTestSphere("Roll_Sphere", new Vector3(-0.4f, 0f, -0.7f), 0.05f, 0.3f, Color.yellow, grabbableLayer);
            AddLabel(sphere, "Sphere");

            // No-gravity object
            var noGrav = CreateTestCube("NoGravity", new Vector3(-0.1f, 0f, -0.7f), Vector3.one * 0.08f, 0.5f, Color.magenta, grabbableLayer);
            noGrav.GetComponent<Rigidbody>().useGravity = false;
            AddLabel(noGrav, "NoGrav");

            // Heavy brick
            var heavy = CreateTestCube("Heavy_10kg", new Vector3(0.2f, 0f, -0.7f), new Vector3(0.15f, 0.08f, 0.08f), 10f, new Color(0.3f, 0.3f, 0.3f), grabbableLayer);
            AddLabel(heavy, "10kg!");

            // Tiny object
            var tiny = CreateTestCube("Tiny", new Vector3(0.5f, 0f, -0.7f), Vector3.one * 0.025f, 0.05f, Color.white, grabbableLayer);
            AddLabel(tiny, "Tiny");

            // Shelf/table for objects to sit on
            CreateShelf(new Vector3(0f, -0.05f, 0f));
            CreateShelf(new Vector3(0f, -0.05f, -0.35f));
            CreateShelf(new Vector3(0f, -0.05f, -0.7f));

            Debug.Log("[BillVR] GrabTestKit: Spawned test objects. Press T for diagnostics, Y to respawn.");
        }

        private GameObject CreateTestCube(string name, Vector3 localPos, Vector3 scale, float mass, Color color, int layer)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(_testRoot.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            if (layer >= 0) go.layer = layer;

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.5f;
            go.AddComponent<Grabbable>();

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat == null) mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;

            return go;
        }

        private GameObject CreateTestSphere(string name, Vector3 localPos, float radius, float mass, Color color, int layer)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(_testRoot.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * radius * 2f;
            if (layer >= 0) go.layer = layer;

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            go.AddComponent<Grabbable>();

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat == null) mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;

            return go;
        }

        private void CreateShelf(Vector3 localPos)
        {
            var shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shelf.name = "TestShelf";
            shelf.transform.SetParent(_testRoot.transform, false);
            shelf.transform.localPosition = localPos;
            shelf.transform.localScale = new Vector3(1.2f, 0.02f, 0.25f);

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (mat == null) mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(0.5f, 0.45f, 0.4f, 1f);
            shelf.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private void AddLabel(GameObject target, string text)
        {
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(target.transform, false);
            labelGo.transform.localPosition = Vector3.up * (target.transform.localScale.y * 0.5f + 0.03f);

            var tm = labelGo.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 24;
            tm.characterSize = 0.012f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.white;

            var billboard = labelGo.AddComponent<LabelBillboard>();
            billboard.Init();
        }

        private int CountLines(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            int count = 1;
            for (int i = 0; i < s.Length; i++)
                if (s[i] == '\n') count++;
            return count;
        }
    }

    public class LabelBillboard : MonoBehaviour
    {
        private Transform _cam;

        public void Init()
        {
            var cam = Camera.main;
            if (cam != null) _cam = cam.transform;
        }

        private void LateUpdate()
        {
            if (_cam == null)
            {
                var cam = Camera.main;
                if (cam != null) _cam = cam.transform;
                else return;
            }

            transform.rotation = Quaternion.LookRotation(transform.position - _cam.position);
        }
    }
}
