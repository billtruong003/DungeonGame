using UnityEngine;
using BillVRCore.Hand;
using BillVRCore.Input;
using BillVRCore.Tracking;

namespace BillVRCore.DebugTools
{
    /// <summary>
    /// Full desktop VR simulator. Drives camera, controller targets, and input
    /// so you can test grab/interact without a headset.
    /// Add to the [BillVR] Player root or any scene object.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class DesktopVRSimulator : MonoBehaviour
    {
        [Header("Camera")]
        [SerializeField] private float mouseSensitivity = 2.5f;
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float sprintMultiplier = 2f;

        [Header("Hand")]
        [SerializeField] private float handDepthSpeed = 0.3f;
        [SerializeField] private float handMoveSpeed = 4f;
        [SerializeField] private float handVerticalSpeed = 3f;
        [SerializeField] private float handReturnSpeed = 6f;
        [SerializeField] private float handStartDepth = 0.4f;
        [SerializeField] private float handStartDown = -0.25f;
        [Tooltip("Rotation offset applied to controller targets so the hand model faces the correct direction")]
        [SerializeField] private Vector3 handRotationOffset = new Vector3(0f, 0f, 0f);

        [Header("Keys")]
        [SerializeField] private KeyCode switchHandKey = KeyCode.Tab;
        [SerializeField] private KeyCode grabKey = KeyCode.G;
        [SerializeField] private KeyCode triggerKey = KeyCode.T;
        [SerializeField] private KeyCode primaryButtonKey = KeyCode.B;
        [SerializeField] private KeyCode secondaryButtonKey = KeyCode.N;
        [SerializeField] private KeyCode thumbstickClickKey = KeyCode.R;
        [SerializeField] private KeyCode toggleHudKey = KeyCode.F2;

        [Header("HUD")]
        [SerializeField] private bool showHud = true;

        public HandSide ActiveHand { get; private set; } = HandSide.Right;
        public SimulatorInputState InputState { get; private set; } = new();

        private Camera _cam;
        private Transform _camTransform;
        private float _pitch;
        private float _yaw;

        // Controller targets — simulator drives these instead of XR tracking
        private Transform _leftTarget;
        private Transform _rightTarget;
        private Vector3 _leftLocalOffset;
        private Vector3 _rightLocalOffset;
        private float _leftDepth;
        private float _rightDepth;

        private bool _initialized;

        private GUIStyle _hudStyle;
        private GUIStyle _hudHeaderStyle;
        private GUIStyle _hudKeyStyle;

        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (_initialized) return;

            // Force desktop input mode
            if (InputManager.Instance != null)
                InputManager.Instance.SwitchMode(InputMode.Desktop);

            _cam = Camera.main;
            if (_cam == null) return;
            _camTransform = _cam.transform;

            // Get initial camera angle
            Vector3 euler = _camTransform.eulerAngles;
            _yaw = euler.y;
            _pitch = euler.x;
            if (_pitch > 180f) _pitch -= 360f;

            // Disable XR tracking drivers — desktop takes over
            DisableXRDrivers();

            // Find or create controller targets
            FindControllerTargets();

            // Setup hand offsets
            _leftLocalOffset = new Vector3(-0.2f, handStartDown, handStartDepth);
            _rightLocalOffset = new Vector3(0.2f, handStartDown, handStartDepth);
            _leftDepth = handStartDepth;
            _rightDepth = handStartDepth;

            // Replace the input provider with our simulator-aware one
            if (InputManager.Instance != null)
                InputManager.Instance.SetCustomProvider(new SimulatorBridgeInput(this), InputMode.Desktop);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            _initialized = true;
            Debug.Log("[BillVR] Desktop Simulator active. Press F2 for controls.");
        }

        private void DisableXRDrivers()
        {
            var headDrivers = FindObjectsByType<TrackedHeadDriver>(FindObjectsSortMode.None);
            foreach (var d in headDrivers) d.enabled = false;

            var controllerDrivers = FindObjectsByType<TrackedControllerDriver>(FindObjectsSortMode.None);
            foreach (var d in controllerDrivers) d.enabled = false;
        }

        private void FindControllerTargets()
        {
            // Find VRHands and use their followTarget
            var hands = FindObjectsByType<VRHand>(FindObjectsSortMode.None);
            foreach (var h in hands)
            {
                if (h.Side == HandSide.Left && h.FollowTarget != null)
                    _leftTarget = h.FollowTarget;
                else if (h.Side == HandSide.Right && h.FollowTarget != null)
                    _rightTarget = h.FollowTarget;
            }

            // Fallback: create targets parented to camera
            if (_leftTarget == null)
            {
                var go = new GameObject("[Sim] LeftTarget");
                go.transform.SetParent(_camTransform, false);
                _leftTarget = go.transform;
            }
            if (_rightTarget == null)
            {
                var go = new GameObject("[Sim] RightTarget");
                go.transform.SetParent(_camTransform, false);
                _rightTarget = go.transform;
            }
        }

        private void Update()
        {
            if (!_initialized) { Initialize(); return; }
            if (_cam == null) return;

            HandleSwitchHand();
            HandleCamera();
            HandleMovement();
            HandleHandPosition();
            HandleInput();
        }

        private void HandleSwitchHand()
        {
            if (UnityEngine.Input.GetKeyDown(switchHandKey))
                ActiveHand = ActiveHand == HandSide.Right ? HandSide.Left : HandSide.Right;
        }

        private void HandleCamera()
        {
            // Right mouse held → mouselook
            if (!UnityEngine.Input.GetMouseButton(1)) return;

            float mx = UnityEngine.Input.GetAxis("Mouse X") * mouseSensitivity;
            float my = UnityEngine.Input.GetAxis("Mouse Y") * mouseSensitivity;

            _yaw += mx;
            _pitch -= my;
            _pitch = Mathf.Clamp(_pitch, -89f, 89f);

            _camTransform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void HandleMovement()
        {
            float h = 0f, v = 0f, up = 0f;

            if (UnityEngine.Input.GetKey(KeyCode.W)) v += 1f;
            if (UnityEngine.Input.GetKey(KeyCode.S)) v -= 1f;
            if (UnityEngine.Input.GetKey(KeyCode.A)) h -= 1f;
            if (UnityEngine.Input.GetKey(KeyCode.D)) h += 1f;
            if (UnityEngine.Input.GetKey(KeyCode.Space)) up += 1f;
            if (UnityEngine.Input.GetKey(KeyCode.LeftControl)) up -= 1f;

            if (h == 0f && v == 0f && up == 0f) return;

            float speed = moveSpeed;
            if (UnityEngine.Input.GetKey(KeyCode.LeftShift)) speed *= sprintMultiplier;

            Vector3 forward = _camTransform.forward;
            Vector3 right = _camTransform.right;
            forward.y = 0f; forward.Normalize();
            right.y = 0f; right.Normalize();

            Vector3 move = (forward * v + right * h + Vector3.up * up).normalized * speed * Time.deltaTime;

            // Move the entire player (or just camera if no player body)
            var playerBody = GetComponentInParent<Locomotion.VRPlayerBody>();
            if (playerBody != null)
                playerBody.transform.position += move;
            else
                _camTransform.position += move;
        }

        private void HandleHandPosition()
        {
            // Determine which hand to control
            ref Vector3 offset = ref (ActiveHand == HandSide.Left ? ref _leftLocalOffset : ref _rightLocalOffset);
            ref float depth = ref (ActiveHand == HandSide.Left ? ref _leftDepth : ref _rightDepth);
            Transform target = ActiveHand == HandSide.Left ? _leftTarget : _rightTarget;
            Transform otherTarget = ActiveHand == HandSide.Left ? _rightTarget : _leftTarget;

            // Not holding right mouse (camera mode) → mouse moves hand XY
            if (!UnityEngine.Input.GetMouseButton(1))
            {
                float mx = UnityEngine.Input.GetAxis("Mouse X") * handMoveSpeed * Time.deltaTime;
                float my = UnityEngine.Input.GetAxis("Mouse Y") * handMoveSpeed * Time.deltaTime;
                offset.x += mx;
                offset.y += my;
            }

            // Scroll wheel → hand depth (forward/back)
            float scroll = UnityEngine.Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                // Middle mouse held → vertical movement instead
                if (UnityEngine.Input.GetMouseButton(2))
                {
                    offset.y += scroll * handVerticalSpeed;
                }
                else
                {
                    depth += scroll * handDepthSpeed;
                    depth = Mathf.Clamp(depth, 0.1f, 1.2f);
                    offset.z = depth;
                }
            }

            // Apply hand positions relative to camera
            Quaternion handRot = _camTransform.rotation * Quaternion.Euler(handRotationOffset);
            if (target != null)
            {
                Vector3 worldPos = _camTransform.position
                    + _camTransform.right * offset.x
                    + _camTransform.up * offset.y
                    + _camTransform.forward * offset.z;
                target.position = worldPos;
                target.rotation = handRot;
            }

            // Other hand: gentle follow at its last offset
            ref Vector3 otherOffset = ref (ActiveHand == HandSide.Left ? ref _rightLocalOffset : ref _leftLocalOffset);
            if (otherTarget != null)
            {
                Vector3 otherWorld = _camTransform.position
                    + _camTransform.right * otherOffset.x
                    + _camTransform.up * otherOffset.y
                    + _camTransform.forward * otherOffset.z;
                otherTarget.position = Vector3.Lerp(otherTarget.position, otherWorld, Time.deltaTime * handReturnSpeed);
                otherTarget.rotation = Quaternion.Slerp(otherTarget.rotation, handRot, Time.deltaTime * handReturnSpeed);
            }
        }

        private void HandleInput()
        {
            var s = InputState;

            s.prevGrip = s.grip;
            s.prevTrigger = s.trigger;
            s.prevPrimary = s.primary;
            s.prevSecondary = s.secondary;
            s.prevThumbstickClick = s.thumbstickClick;
            s.prevMenu = s.menu;

            // G = hold to grab (hold key = grip, release key = release)
            // Left click = also grabs (unless right-clicking for rotation)
            bool momentaryGrip = UnityEngine.Input.GetMouseButton(0) && !UnityEngine.Input.GetMouseButton(1);
            s.grip = UnityEngine.Input.GetKey(grabKey) || momentaryGrip;

            s.trigger = UnityEngine.Input.GetKey(triggerKey);
            s.primary = UnityEngine.Input.GetKey(primaryButtonKey);
            s.secondary = UnityEngine.Input.GetKey(secondaryButtonKey);
            s.thumbstickClick = UnityEngine.Input.GetKey(thumbstickClickKey);
            s.menu = UnityEngine.Input.GetKeyDown(KeyCode.Escape);

            // Joystick via arrow keys (for locomotion testing)
            float jx = 0f, jy = 0f;
            if (UnityEngine.Input.GetKey(KeyCode.UpArrow)) jy += 1f;
            if (UnityEngine.Input.GetKey(KeyCode.DownArrow)) jy -= 1f;
            if (UnityEngine.Input.GetKey(KeyCode.LeftArrow)) jx -= 1f;
            if (UnityEngine.Input.GetKey(KeyCode.RightArrow)) jx += 1f;
            s.joystick = new Vector2(jx, jy);

            if (UnityEngine.Input.GetKeyDown(toggleHudKey))
                showHud = !showHud;
        }

        private void OnGUI()
        {
            if (!showHud || !_initialized) return;

            if (_hudStyle == null)
            {
                _hudStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, richText = true, normal = { textColor = Color.white } };
                _hudHeaderStyle = new GUIStyle(_hudStyle) { fontSize = 13 };
                _hudKeyStyle = new GUIStyle(_hudStyle) { normal = { textColor = new Color(0.7f, 0.9f, 1f) } };
            }

            float x = Screen.width - 290;
            float y = 10;
            float w = 280;

            // Background
            GUI.Box(new Rect(x - 4, y - 4, w + 8, 320), "");

            string handColor = ActiveHand == HandSide.Right ? "cyan" : "orange";
            string handName = ActiveHand == HandSide.Right ? "RIGHT" : "LEFT";
            GUI.Label(new Rect(x, y, w, 20), $"<color=yellow><b>DESKTOP VR SIM</b></color>  Active: <color={handColor}><b>{handName}</b></color>", _hudHeaderStyle);
            y += 20;

            string gripState = InputState.grip ? "<color=lime>ON</color>" : "off";
            string trigState = InputState.trigger ? "<color=lime>ON</color>" : "off";
            string primState = InputState.primary ? "<color=lime>ON</color>" : "off";
            GUI.Label(new Rect(x, y, w, 16), $"  Grip: {gripState}  Trigger: {trigState}  Btn: {primState}", _hudStyle);
            y += 18;

            GUI.Label(new Rect(x, y, w, 16), "", _hudStyle);
            y += 4;

            DrawKeyLine(ref y, x, w, "Right Mouse + Move", "Look around");
            DrawKeyLine(ref y, x, w, "WASD", "Move");
            DrawKeyLine(ref y, x, w, "Space / Ctrl", "Up / Down");
            DrawKeyLine(ref y, x, w, "Shift", "Sprint");
            y += 6;
            DrawKeyLine(ref y, x, w, "Mouse Move", "Hand XY");
            DrawKeyLine(ref y, x, w, "Scroll", "Hand depth (Z)");
            DrawKeyLine(ref y, x, w, "Middle + Scroll", "Hand up/down");
            DrawKeyLine(ref y, x, w, "Tab", "Switch hand");
            y += 6;
            DrawKeyLine(ref y, x, w, "Left Click", "Grip (hold)");
            DrawKeyLine(ref y, x, w, "G", "Grip (toggle)");
            DrawKeyLine(ref y, x, w, "T", "Trigger");
            DrawKeyLine(ref y, x, w, "B / N", "Button A / Button B");
            DrawKeyLine(ref y, x, w, "R", "Thumbstick click");
            DrawKeyLine(ref y, x, w, "Arrow Keys", "Joystick");
            DrawKeyLine(ref y, x, w, "F2", "Toggle this HUD");
        }

        private void DrawKeyLine(ref float y, float x, float w, string key, string action)
        {
            GUI.Label(new Rect(x, y, 130, 16), $"  <b>{key}</b>", _hudKeyStyle);
            GUI.Label(new Rect(x + 135, y, w - 135, 16), action, _hudStyle);
            y += 15;
        }

        /// <summary>
        /// Current frame input state, read by SimulatorBridgeInput.
        /// </summary>
        public class SimulatorInputState
        {
            public bool grip, prevGrip;
            public bool trigger, prevTrigger;
            public bool primary, prevPrimary;
            public bool secondary, prevSecondary;
            public bool thumbstickClick, prevThumbstickClick;
            public bool menu, prevMenu;
            public Vector2 joystick;
        }
    }

    /// <summary>
    /// IVRInput bridge that reads from DesktopVRSimulator state.
    /// Routes all input to the active hand.
    /// </summary>
    public class SimulatorBridgeInput : IVRInput
    {
        private readonly DesktopVRSimulator _sim;

        public SimulatorBridgeInput(DesktopVRSimulator sim) => _sim = sim;

        public InputSourceType ActiveSource => InputSourceType.Desktop;

        public void UpdateState() { } // State updated by simulator Update()

        private bool IsActive(HandSide side) => _sim.ActiveHand == side;
        private DesktopVRSimulator.SimulatorInputState S => _sim.InputState;

        // Grip — only fires on active hand
        public bool GrabPressed(HandSide side) => IsActive(side) && S.grip && !S.prevGrip;
        public bool GrabReleased(HandSide side) => IsActive(side) && !S.grip && S.prevGrip;
        public bool GrabHeld(HandSide side) => IsActive(side) && S.grip;
        public float GripStrength(HandSide side) => IsActive(side) && S.grip ? 1f : 0f;

        // Trigger
        public bool TriggerPressed(HandSide side) => IsActive(side) && S.trigger && !S.prevTrigger;
        public bool TriggerReleased(HandSide side) => IsActive(side) && !S.trigger && S.prevTrigger;
        public bool TriggerHeld(HandSide side) => IsActive(side) && S.trigger;
        public float TriggerStrength(HandSide side) => IsActive(side) && S.trigger ? 1f : 0f;

        // Buttons
        public bool PrimaryButtonDown(HandSide side) => IsActive(side) && S.primary && !S.prevPrimary;
        public bool SecondaryButtonDown(HandSide side) => IsActive(side) && S.secondary && !S.prevSecondary;
        public bool JoystickClick(HandSide side) => IsActive(side) && S.thumbstickClick && !S.prevThumbstickClick;
        public bool MenuButtonDown() => S.menu && !S.prevMenu;

        // Joystick — left hand always gets it (locomotion)
        public Vector2 JoystickAxis(HandSide side) => side == HandSide.Left ? S.joystick : Vector2.zero;

        // Finger curl: grip = all fingers closed, trigger = index
        public float FingerCurl(HandSide side, FingerType finger)
        {
            if (!IsActive(side)) return 0f;
            return finger switch
            {
                FingerType.Index => S.trigger ? 1f : (S.grip ? 0.7f : 0f),
                _ => S.grip ? 1f : 0f
            };
        }

        public bool ThumbTouching(HandSide side) => IsActive(side) && (S.primary || S.secondary);
        public bool IndexTouching(HandSide side) => IsActive(side) && S.trigger;
        public bool IsConnected(HandSide side) => true;

        public Pose GetControllerPose(HandSide side)
        {
            var cam = Camera.main;
            if (cam == null) return Pose.identity;
            // Return camera-relative pose — actual positions driven by simulator
            return new Pose(cam.transform.position, cam.transform.rotation);
        }

        public Pose GetHeadPose()
        {
            var cam = Camera.main;
            return cam != null ? new Pose(cam.transform.position, cam.transform.rotation) : Pose.identity;
        }
    }
}
