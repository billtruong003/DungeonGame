using UnityEngine;
using UnityEngine.EventSystems;
using BillVRCore.Hand;
using BillVRCore.Input;

namespace BillVRCore.UI
{
    [RequireComponent(typeof(VRHand))]
    public class UIPointer : MonoBehaviour
    {
        [Header("Ray")]
        [SerializeField] private float maxDistance = 10f;
        [SerializeField] private LayerMask uiLayers = ~0;
        [SerializeField] private float rayWidth = 0.003f;

        [Header("Visual")]
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private GameObject cursorPrefab;
        [SerializeField] private Color idleColor = new(0.6f, 0.6f, 0.8f, 0.4f);
        [SerializeField] private Color hoverColor = new(0.3f, 0.8f, 1f, 0.8f);
        [SerializeField] private Color pressColor = new(0.2f, 1f, 0.6f, 0.9f);

        [Header("Input")]
        [SerializeField] private bool useTriggerForClick = true;

        public bool IsHoveringUI { get; private set; }
        public GameObject HoveredObject { get; private set; }
        public RaycastHit? LastHit { get; private set; }

        private VRHand _hand;
        private Transform _cursor;
        private Camera _eventCamera;
        private PointerEventData _pointerData;
        private GameObject _pressedObject;
        private GameObject _dragObject;
        private bool _wasPressed;
        private float _pressTime;

        private void Awake()
        {
            _hand = GetComponent<VRHand>();
            SetupLineRenderer();
            SetupCursor();
            SetupEventCamera();
        }

        private void OnEnable()
        {
            if (_pointerData == null && EventSystem.current != null)
                _pointerData = new PointerEventData(EventSystem.current);
        }

        private void Update()
        {
            if (InputManager.Instance == null || _hand.GrabHandler.IsHolding)
            {
                HidePointer();
                return;
            }

            PerformRaycast();
            ProcessInput();
            UpdateVisuals();
        }

        private void PerformRaycast()
        {
            Transform origin = _hand.PalmTransform != null ? _hand.PalmTransform : transform;
            Ray ray = new Ray(origin.position, origin.forward);

            IsHoveringUI = false;
            HoveredObject = null;
            LastHit = null;

            if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, uiLayers, QueryTriggerInteraction.Collide))
                return;

            var canvas = hit.collider.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            IsHoveringUI = true;
            HoveredObject = hit.collider.gameObject;
            LastHit = hit;
        }

        private void ProcessInput()
        {
            if (EventSystem.current == null) return;
            if (_pointerData == null)
                _pointerData = new PointerEventData(EventSystem.current);

            IVRInput input = InputManager.Instance.Input;
            bool pressed = useTriggerForClick
                ? input.TriggerHeld(_hand.Side)
                : input.GrabHeld(_hand.Side);

            bool justPressed = pressed && !_wasPressed;
            bool justReleased = !pressed && _wasPressed;
            _wasPressed = pressed;

            if (!IsHoveringUI)
            {
                if (_dragObject != null)
                {
                    ExecuteEvents.Execute(_dragObject, _pointerData, ExecuteEvents.endDragHandler);
                    _dragObject = null;
                }
                if (_pressedObject != null)
                {
                    ExecuteEvents.Execute(_pressedObject, _pointerData, ExecuteEvents.pointerUpHandler);
                    _pressedObject = null;
                }
                return;
            }

            UpdatePointerData();

            if (justPressed)
            {
                _pressedObject = HoveredObject;
                _pressTime = Time.unscaledTime;
                _pointerData.pressPosition = _pointerData.position;
                _pointerData.pointerPressRaycast = _pointerData.pointerCurrentRaycast;

                ExecuteEvents.Execute(HoveredObject, _pointerData, ExecuteEvents.pointerDownHandler);
                _hand.Haptics.PlayUIHaptic();
            }

            if (pressed && _pressedObject != null)
            {
                float dragThreshold = EventSystem.current.pixelDragThreshold;
                if (Vector2.Distance(_pointerData.position, _pointerData.pressPosition) > dragThreshold)
                {
                    if (_dragObject == null)
                    {
                        _dragObject = _pressedObject;
                        ExecuteEvents.Execute(_dragObject, _pointerData, ExecuteEvents.beginDragHandler);
                    }
                    ExecuteEvents.Execute(_dragObject, _pointerData, ExecuteEvents.dragHandler);
                }
            }

            if (justReleased)
            {
                if (_dragObject != null)
                {
                    ExecuteEvents.Execute(_dragObject, _pointerData, ExecuteEvents.endDragHandler);
                    ExecuteEvents.Execute(HoveredObject, _pointerData, ExecuteEvents.dropHandler);
                    _dragObject = null;
                }

                ExecuteEvents.Execute(_pressedObject, _pointerData, ExecuteEvents.pointerUpHandler);

                if (_pressedObject == HoveredObject && Time.unscaledTime - _pressTime < 0.3f)
                    ExecuteEvents.Execute(HoveredObject, _pointerData, ExecuteEvents.pointerClickHandler);

                _pressedObject = null;
            }

            Vector2 scroll = InputManager.Instance.Input.JoystickAxis(_hand.Side);
            if (Mathf.Abs(scroll.y) > 0.3f)
            {
                _pointerData.scrollDelta = new Vector2(0f, scroll.y * 10f);
                ExecuteEvents.Execute(HoveredObject, _pointerData, ExecuteEvents.scrollHandler);
            }
        }

        private void UpdatePointerData()
        {
            if (!LastHit.HasValue || _eventCamera == null) return;

            _pointerData.position = _eventCamera.WorldToScreenPoint(LastHit.Value.point);
            _pointerData.delta = _pointerData.position - _pointerData.pressPosition;

            var result = new RaycastResult
            {
                gameObject = HoveredObject,
                worldPosition = LastHit.Value.point,
                worldNormal = LastHit.Value.normal,
                distance = LastHit.Value.distance
            };
            _pointerData.pointerCurrentRaycast = result;
        }

        private void UpdateVisuals()
        {
            if (lineRenderer == null) return;

            Transform origin = _hand.PalmTransform != null ? _hand.PalmTransform : transform;
            Vector3 endPoint = LastHit.HasValue
                ? LastHit.Value.point
                : origin.position + origin.forward * maxDistance;

            lineRenderer.enabled = true;
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, origin.position);
            lineRenderer.SetPosition(1, endPoint);

            Color color = _wasPressed ? pressColor : (IsHoveringUI ? hoverColor : idleColor);
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;

            if (_cursor != null)
            {
                _cursor.gameObject.SetActive(IsHoveringUI && LastHit.HasValue);
                if (LastHit.HasValue)
                {
                    _cursor.position = LastHit.Value.point;
                    _cursor.rotation = Quaternion.LookRotation(-LastHit.Value.normal);
                    _cursor.localScale = Vector3.one * (_wasPressed ? 0.008f : 0.012f);
                }
            }
        }

        private void HidePointer()
        {
            if (lineRenderer != null) lineRenderer.enabled = false;
            if (_cursor != null) _cursor.gameObject.SetActive(false);
            IsHoveringUI = false;
            HoveredObject = null;
        }

        private void SetupLineRenderer()
        {
            if (lineRenderer != null) return;

            lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            lineRenderer.startWidth = rayWidth;
            lineRenderer.endWidth = rayWidth * 0.5f;
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.positionCount = 2;
            lineRenderer.enabled = false;
        }

        private void SetupCursor()
        {
            if (cursorPrefab != null)
            {
                _cursor = Instantiate(cursorPrefab).transform;
                _cursor.gameObject.SetActive(false);
                return;
            }

            var cursorGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cursorGo.name = "UICursor";
            Object.Destroy(cursorGo.GetComponent<Collider>());
            cursorGo.transform.localScale = Vector3.one * 0.012f;

            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = hoverColor;
            cursorGo.GetComponent<MeshRenderer>().sharedMaterial = mat;

            _cursor = cursorGo.transform;
            _cursor.gameObject.SetActive(false);
        }

        private void SetupEventCamera()
        {
            var camGo = new GameObject("[UIPointer] EventCamera");
            camGo.transform.SetParent(transform, false);
            _eventCamera = camGo.AddComponent<Camera>();
            _eventCamera.clearFlags = CameraClearFlags.Nothing;
            _eventCamera.cullingMask = 0;
            _eventCamera.nearClipPlane = 0.01f;
            _eventCamera.farClipPlane = maxDistance;
            _eventCamera.enabled = false;
        }

        public void SetMaxDistance(float dist) => maxDistance = dist;
        public void SetUILayers(LayerMask layers) => uiLayers = layers;
        public void SetUseTrigger(bool useTrigger) => useTriggerForClick = useTrigger;
    }
}
