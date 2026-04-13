using System;
using UnityEngine;
using UnityEngine.Events;

namespace BillVRCore.Interaction
{
    [RequireComponent(typeof(Grabbable))]
    public class GrabbableHeldJoint : MonoBehaviour
    {
        [Header("Connection")]
        [SerializeField] private Grabbable parentGrabbable;
        [SerializeField] private Vector3 pivotOffset;

        [Header("X Axis Limits")]
        [SerializeField] private float xMinLimit;
        [SerializeField] private float xMaxLimit;
        [SerializeField] private float xReturnSpring;

        [Header("Y Axis Limits")]
        [SerializeField] private float yMinLimit;
        [SerializeField] private float yMaxLimit;
        [SerializeField] private float yReturnSpring;

        [Header("Z Axis Limits")]
        [SerializeField] private float zMinLimit;
        [SerializeField] private float zMaxLimit;
        [SerializeField] private float zReturnSpring;

        [Header("Held Mass")]
        [SerializeField] [Min(0.1f)] private float heldMassScale = 1f;

        [Header("Event Threshold")]
        [SerializeField] [Range(0f, 1f)] private float eventThreshold = 0.05f;

        [Header("Events")]
        [SerializeField] private UnityEvent onReachMin;
        [SerializeField] private UnityEvent onReachMax;
        [SerializeField] private UnityEvent<float> onSlideChanged;

        public float NormalizedPosition { get; private set; }
        public bool IsAtMin { get; private set; }
        public bool IsAtMax { get; private set; }

        public event Action OnReachMinEvent;
        public event Action OnReachMaxEvent;
        public event Action<float> OnSlideEvent;

        private Grabbable _grabbable;
        private Hand.VRHand _holdingHand;
        private Vector3 _localOrigin;
        private Vector3 _localStartOrigin;
        private bool _triggeredMin;
        private bool _triggeredMax;
        private bool _grabFrame;
        private Rigidbody _rb;
        private float _originalMass;

        private void Awake()
        {
            _grabbable = GetComponent<Grabbable>();
            _rb = GetComponent<Rigidbody>();
            if (_rb != null) _originalMass = _rb.mass;
        }

        private void Start()
        {
            if (parentGrabbable == null) return;

            _localOrigin = parentGrabbable.transform.InverseTransformPoint(transform.position) + pivotOffset;
            _localStartOrigin = _localOrigin;

            _grabbable.OnGrabEvent += OnGrabbed;
            _grabbable.OnReleaseEvent += OnReleased;
            _grabbable.SetSingleHandOnly(true);
            _grabbable.SetParentOnGrab(false);
            _grabbable.SetDisableGrabTracking(true);

            if (HasNoRange())
                _triggeredMin = true;
        }

        private void FixedUpdate()
        {
            if (_grabFrame) { _grabFrame = false; return; }
            UpdateJoint();
        }

        private void OnGrabbed(Hand.VRHand hand, Grabbable grab)
        {
            _holdingHand = hand;
            _grabFrame = true;
            if (_rb != null) _rb.mass = _originalMass * heldMassScale;
        }

        private void OnReleased(Hand.VRHand hand, Grabbable grab)
        {
            _holdingHand = null;
            if (_rb != null) _rb.mass = _originalMass;
        }

        private void UpdateJoint()
        {
            if (parentGrabbable == null) return;

            if (parentGrabbable.IsHeld && _grabbable.IsHeld && _holdingHand != null)
                UpdateHeldPosition();
            else
                UpdateReturnPosition();

            CheckThresholdEvents();
            UpdateNormalizedPosition();
        }

        private void UpdateHeldPosition()
        {
            Transform parentTransform = parentGrabbable.transform;
            Vector3 handWorldPos = _holdingHand.PalmTransform != null
                ? _holdingHand.PalmTransform.position
                : _holdingHand.transform.position;

            Vector3 handLocal = parentTransform.InverseTransformPoint(handWorldPos) + pivotOffset;

            Vector3 clampedTarget = new Vector3(
                Mathf.Clamp(handLocal.x, _localOrigin.x + xMinLimit, _localOrigin.x + xMaxLimit),
                Mathf.Clamp(handLocal.y, _localOrigin.y + yMinLimit, _localOrigin.y + yMaxLimit),
                Mathf.Clamp(handLocal.z, _localOrigin.z + zMinLimit, _localOrigin.z + zMaxLimit)
            );

            Vector3 worldTarget = parentTransform.TransformPoint(clampedTarget - pivotOffset);
            Vector3 delta = worldTarget - transform.position;

            transform.position += delta;
            ClampLocalPosition();
        }

        private void UpdateReturnPosition()
        {
            Vector3 currentLocal = parentGrabbable.transform.InverseTransformPoint(transform.position);
            float dt = Time.fixedDeltaTime;

            Vector3 returnTarget = new Vector3(
                xReturnSpring > 0 ? Mathf.MoveTowards(currentLocal.x, _localStartOrigin.x, dt * xReturnSpring) : currentLocal.x,
                yReturnSpring > 0 ? Mathf.MoveTowards(currentLocal.y, _localStartOrigin.y, dt * yReturnSpring) : currentLocal.y,
                zReturnSpring > 0 ? Mathf.MoveTowards(currentLocal.z, _localStartOrigin.z, dt * zReturnSpring) : currentLocal.z
            );

            if (Vector3.Distance(currentLocal, returnTarget) > 0.0001f)
                transform.position = parentGrabbable.transform.TransformPoint(returnTarget);
        }

        private void ClampLocalPosition()
        {
            Vector3 local = parentGrabbable.transform.InverseTransformPoint(transform.position);
            local = new Vector3(
                Mathf.Clamp(local.x, _localStartOrigin.x + xMinLimit, _localStartOrigin.x + xMaxLimit),
                Mathf.Clamp(local.y, _localStartOrigin.y + yMinLimit, _localStartOrigin.y + yMaxLimit),
                Mathf.Clamp(local.z, _localStartOrigin.z + zMinLimit, _localStartOrigin.z + zMaxLimit)
            );
            transform.position = parentGrabbable.transform.TransformPoint(local);
        }

        private void UpdateNormalizedPosition()
        {
            Vector3 local = parentGrabbable.transform.InverseTransformPoint(transform.position);
            Vector3 range = new Vector3(
                xMaxLimit - xMinLimit,
                yMaxLimit - yMinLimit,
                zMaxLimit - zMinLimit
            );

            float maxRange = Mathf.Max(Mathf.Abs(range.x), Mathf.Abs(range.y), Mathf.Abs(range.z));
            if (maxRange < 0.0001f) { NormalizedPosition = 0f; return; }

            float dominantAxis;
            if (Mathf.Abs(range.x) >= Mathf.Abs(range.y) && Mathf.Abs(range.x) >= Mathf.Abs(range.z))
                dominantAxis = Mathf.InverseLerp(_localStartOrigin.x + xMinLimit, _localStartOrigin.x + xMaxLimit, local.x);
            else if (Mathf.Abs(range.y) >= Mathf.Abs(range.z))
                dominantAxis = Mathf.InverseLerp(_localStartOrigin.y + yMinLimit, _localStartOrigin.y + yMaxLimit, local.y);
            else
                dominantAxis = Mathf.InverseLerp(_localStartOrigin.z + zMinLimit, _localStartOrigin.z + zMaxLimit, local.z);

            float prevNorm = NormalizedPosition;
            NormalizedPosition = dominantAxis;

            if (!Mathf.Approximately(prevNorm, NormalizedPosition))
            {
                onSlideChanged?.Invoke(NormalizedPosition);
                OnSlideEvent?.Invoke(NormalizedPosition);
            }
        }

        private void CheckThresholdEvents()
        {
            Vector3 local = parentGrabbable.transform.InverseTransformPoint(transform.position);

            bool atMax = local.x >= _localStartOrigin.x + xMaxLimit - Mathf.Abs(xMaxLimit) * eventThreshold - 0.001f
                      && local.y >= _localStartOrigin.y + yMaxLimit - Mathf.Abs(yMaxLimit) * eventThreshold - 0.001f
                      && local.z >= _localStartOrigin.z + zMaxLimit - Mathf.Abs(zMaxLimit) * eventThreshold - 0.001f;

            bool atMin = local.x <= _localStartOrigin.x + xMinLimit + Mathf.Abs(xMinLimit) * eventThreshold + 0.001f
                      && local.y <= _localStartOrigin.y + yMinLimit + Mathf.Abs(yMinLimit) * eventThreshold + 0.001f
                      && local.z <= _localStartOrigin.z + zMinLimit + Mathf.Abs(zMinLimit) * eventThreshold + 0.001f;

            if (atMax && !_triggeredMax)
            {
                IsAtMax = true;
                IsAtMin = false;
                _triggeredMax = true;
                _triggeredMin = false;
                onReachMax?.Invoke();
                OnReachMaxEvent?.Invoke();
            }

            if (atMin && !_triggeredMin)
            {
                IsAtMin = true;
                IsAtMax = false;
                _triggeredMin = true;
                _triggeredMax = false;
                onReachMin?.Invoke();
                OnReachMinEvent?.Invoke();
            }
        }

        public void SnapToMin()
        {
            if (parentGrabbable == null) return;
            transform.position = parentGrabbable.transform.TransformPoint(
                _localStartOrigin + pivotOffset + new Vector3(xMinLimit, yMinLimit, zMinLimit));
        }

        public void SnapToMax()
        {
            if (parentGrabbable == null) return;
            transform.position = parentGrabbable.transform.TransformPoint(
                _localStartOrigin + pivotOffset + new Vector3(xMaxLimit, yMaxLimit, zMaxLimit));
        }

        public void ResetOrigin()
        {
            if (parentGrabbable == null) return;
            _localOrigin = parentGrabbable.transform.InverseTransformPoint(transform.position) + pivotOffset;
            _localStartOrigin = _localOrigin;
        }

        private bool HasNoRange()
        {
            return Mathf.Approximately(xMinLimit, 0f) && Mathf.Approximately(xMaxLimit, 0f)
                && Mathf.Approximately(yMinLimit, 0f) && Mathf.Approximately(yMaxLimit, 0f)
                && Mathf.Approximately(zMinLimit, 0f) && Mathf.Approximately(zMaxLimit, 0f);
        }

        private void OnDestroy()
        {
            if (_grabbable != null)
            {
                _grabbable.OnGrabEvent -= OnGrabbed;
                _grabbable.OnReleaseEvent -= OnReleased;
            }
        }

        public void SetParentGrabbable(Grabbable parent) => parentGrabbable = parent;
        public void SetReturnSpring(float x, float y, float z) { xReturnSpring = x; yReturnSpring = y; zReturnSpring = z; }
        public void SetLimits(Vector3 min, Vector3 max)
        {
            xMinLimit = min.x; yMinLimit = min.y; zMinLimit = min.z;
            xMaxLimit = max.x; yMaxLimit = max.y; zMaxLimit = max.z;
        }
    }
}
