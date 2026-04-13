using UnityEngine;
using BillVRCore.Input;
using BillVRCore.Interaction;

namespace BillVRCore.Hand
{
    [DefaultExecutionOrder(-10)]
    [RequireComponent(typeof(Rigidbody))]
    public class VRHand : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private HandSide side;
        [SerializeField] private Transform followTarget;
        [SerializeField] private Transform palmTransform;

        [Header("Position Follow")]
        [SerializeField] private float positionStrength = 60f;
        [SerializeField] private float maxVelocity = 20f;
        [SerializeField] private float baseDrag = 20f;
        [SerializeField] private float dragDamperMultiplier = 3f;
        [SerializeField] private float dragDamperDistance = 0.025f;
        [SerializeField] private float minVelocityStep = 1f;
        [SerializeField] private float velocityDistanceScale = 5f;

        [Header("Rotation Follow")]
        [SerializeField] private float rotationStrength = 100f;
        [SerializeField] private float baseAngularDrag = 20f;
        [SerializeField] private float angularDragDamper = 5f;
        [SerializeField] private float angularDragDamperAngle = 3f;

        [Header("Dynamic Mass")]
        [SerializeField] private float minMass = 0.25f;
        [SerializeField] private float maxMass = 10f;
        [SerializeField] private float distanceMassWeight = 10f;
        [SerializeField] private float distanceMassRange = 0.5f;
        [SerializeField] private float angleMassWeight = 10f;
        [SerializeField] private float angleMassRange = 45f;

        [Header("Teleport")]
        [SerializeField] private float maxFollowDistance = 0.5f;

        [Header("Throw")]
        [SerializeField] private float throwPowerMultiplier = 1.25f;

        [Header("Grab Return")]
        [SerializeField] private float gentleGrabReturnSpeed = 1f;

        public HandSide Side => side;
        public Transform FollowTarget => followTarget;
        public Transform PalmTransform => palmTransform;
        public Rigidbody Rb { get; private set; }
        public GrabHandler GrabHandler { get; private set; }
        public HandHaptics Haptics { get; private set; }
        public HandAnimator Animator { get; private set; }
        public ThrowTracker ThrowTracker { get; private set; }
        public float ThrowPower => throwPowerMultiplier;
        public Vector3 Velocity => Rb.linearVelocity;
        public Vector3 AngularVelocity => Rb.angularVelocity;
        public float Speed => Rb.linearVelocity.magnitude;
        public bool IsHolding => GrabHandler != null && GrabHandler.IsHolding;
        public Grabbable HeldObject => GrabHandler?.HeldObject;
        public float CurrentMass => Rb.mass;

        private Vector3 _grabPositionOffset;
        private Quaternion _grabRotationOffset = Quaternion.identity;
        private Vector3 _lastFollowPosition;
        private Quaternion _lastFollowRotation;
        private Vector3 _lastFrameFollowPosition;
        private Quaternion _lastFrameFollowRotation;
        private float _targetMass;
        private int _maxDistanceFrameCount;

        private readonly Vector3[] _positionHistory = new Vector3[4];

        public Vector3 GrabPositionOffset
        {
            get => _grabPositionOffset;
            set => _grabPositionOffset = value;
        }

        public Quaternion GrabRotationOffset
        {
            get => _grabRotationOffset;
            set => _grabRotationOffset = value;
        }

        public Vector3[] PositionHistory => _positionHistory;

        private void Awake()
        {
            Rb = GetComponent<Rigidbody>();
            ConfigureRigidbody();

            GrabHandler = gameObject.GetOrAddComponent<GrabHandler>();
            Haptics = gameObject.GetOrAddComponent<HandHaptics>();
            Animator = GetComponent<HandAnimator>();
            ThrowTracker = new ThrowTracker(this);

            EnsureStabilizer();
        }

        private void ConfigureRigidbody()
        {
            Rb.useGravity = false;
            Rb.isKinematic = false;
            Rb.interpolation = RigidbodyInterpolation.None;
            Rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Rb.mass = minMass;
            Rb.linearDamping = baseDrag;
            Rb.angularDamping = baseAngularDrag;
            Rb.solverIterations = 30;
            Rb.solverVelocityIterations = 20;
        }

        private void EnsureStabilizer()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var stabilizers = cam.GetComponents<HandStabilizer>();
            foreach (var s in stabilizers)
                if (s != null && s.TargetHand == this) return;

            var stabilizer = cam.gameObject.AddComponent<HandStabilizer>();
            stabilizer.SetHand(this);
        }

        private void FixedUpdate()
        {
            if (followTarget == null) return;

            UpdatePositionHistory();
            UpdateDynamicMass();
            PhysicsFollowPosition();
            PhysicsFollowRotation();
            CheckMaxDistance();
            UpdateThrowTracking();
        }

        private void Update()
        {
            UpdateGrabReturnOffset();
            UpdateLastFrameFollow();
        }

        private void PhysicsFollowPosition()
        {
            Vector3 targetPos = ComputeMoveToPosition();
            // Always use actual rigidbody position — NOT followTarget.
            // Using followTarget when holding caused velocity to be near-zero,
            // making the hand freeze in place while the joint fought gravity.
            Vector3 currentPos = transform.position;
            float distance = Vector3.Distance(targetPos, currentPos);

            Vector3 targetVelocity = (targetPos - currentPos) * positionStrength;

            targetVelocity.x = Mathf.Clamp(targetVelocity.x, -maxVelocity, maxVelocity);
            targetVelocity.y = Mathf.Clamp(targetVelocity.y, -maxVelocity, maxVelocity);
            targetVelocity.z = Mathf.Clamp(targetVelocity.z, -maxVelocity, maxVelocity);

            float inverseDelta = 0.01111f / Time.fixedDeltaTime;

            // Lower drag when holding to let the hand+object system move freely
            float drag = IsHolding
                ? Mathf.Lerp(baseDrag * 0.5f, baseDrag * 0.2f, distance / dragDamperDistance)
                : Mathf.Lerp(baseDrag * dragDamperMultiplier, baseDrag, distance / dragDamperDistance);
            Rb.linearDamping = drag * inverseDelta;

            float step = minVelocityStep * (Time.fixedDeltaTime / 0.01111f);
            step *= 1f + distance * velocityDistanceScale;

            // When holding, snap velocity directly — no smoothing, reduces jitter
            if (IsHolding)
            {
                Rb.linearVelocity = targetVelocity;
            }
            else
            {
                Vector3 currentVel = Rb.linearVelocity;
                Rb.linearVelocity = new Vector3(
                    Mathf.MoveTowards(currentVel.x, targetVelocity.x, step * 0.2f + Mathf.Abs(currentVel.x) * 0.67f),
                    Mathf.MoveTowards(currentVel.y, targetVelocity.y, step * 0.2f + Mathf.Abs(currentVel.y) * 0.67f),
                    Mathf.MoveTowards(currentVel.z, targetVelocity.z, step * 0.2f + Mathf.Abs(currentVel.z) * 0.67f)
                );
            }
        }

        private void PhysicsFollowRotation()
        {
            if (rotationStrength <= 0f) return;

            Quaternion targetRot = ComputeMoveToRotation();
            Quaternion delta = targetRot * Quaternion.Inverse(Rb.rotation);
            delta.ToAngleAxis(out float angle, out Vector3 axis);

            if (float.IsInfinity(axis.x) || float.IsNaN(axis.x)) return;
            if (angle > 180f) angle -= 360f;

            float inverseDelta = 0.01111f / Time.fixedDeltaTime;
            float absAngle = Mathf.Abs(angle);

            float angDrag = IsHolding
                ? baseAngularDrag * 0.3f
                : Mathf.Lerp(baseAngularDrag * angularDragDamper, baseAngularDrag, absAngle / angularDragDamperAngle);
            Rb.angularDamping = angDrag * inverseDelta;

            Rb.angularVelocity = axis * (angle * Mathf.Deg2Rad * rotationStrength);
        }

        private void UpdateDynamicMass()
        {
            Vector3 currentPos = transform.position;
            Quaternion currentRot = transform.rotation;

            Vector3 targetPos = ComputeMoveToPosition();
            Quaternion targetRot = ComputeMoveToRotation();

            float distLerp = Mathf.Clamp01(Vector3.Distance(targetPos, currentPos) / distanceMassRange);
            float angleLerp = Mathf.Clamp01(Mathf.Abs(Quaternion.Angle(targetRot, currentRot)) / angleMassRange);

            float totalWeight = distanceMassWeight + angleMassWeight;
            float distMass = Mathf.Lerp(minMass, maxMass, distLerp) * distanceMassWeight / totalWeight;
            float angleMass = Mathf.Lerp(minMass, maxMass, angleLerp) * angleMassWeight / totalWeight;

            _targetMass = distMass + angleMass;

            // When holding: hand mass must be at least the object's mass so the joint
            // pulls the object toward the hand, not the hand toward the object.
            if (IsHolding && HeldObject != null)
                _targetMass = Mathf.Max(_targetMass, HeldObject.Rb.mass);

            Rb.mass = _targetMass;
        }

        private Vector3 ComputeMoveToPosition()
        {
            return followTarget.position + _grabPositionOffset;
        }

        private Quaternion ComputeMoveToRotation()
        {
            return followTarget.rotation * _grabRotationOffset;
        }

        private void CheckMaxDistance()
        {
            if (followTarget == null) return;

            float distance = Vector3.Distance(Rb.position, followTarget.position);
            if (distance <= maxFollowDistance)
            {
                _maxDistanceFrameCount = 0;
                return;
            }

            _maxDistanceFrameCount++;

            if (IsHolding && _maxDistanceFrameCount > 3)
            {
                GrabHandler.ForceRelease();
            }

            Rb.position = followTarget.position;
            Rb.rotation = followTarget.rotation;
            Rb.linearVelocity = Vector3.zero;
            Rb.angularVelocity = Vector3.zero;
            _grabPositionOffset = Vector3.zero;
            _grabRotationOffset = Quaternion.identity;
            _maxDistanceFrameCount = 0;
        }

        private void UpdateGrabReturnOffset()
        {
            if (followTarget == null) return;

            if (IsHolding && HeldObject != null && !HeldObject.MaintainGrabOffset)
            {
                float deltaDist = Vector3.Distance(followTarget.position, _lastFrameFollowPosition);
                float deltaAngle = Quaternion.Angle(followTarget.rotation, _lastFrameFollowRotation);
                float dt60 = Time.deltaTime * 60f;

                _grabPositionOffset = Vector3.MoveTowards(_grabPositionOffset, Vector3.zero, deltaDist * gentleGrabReturnSpeed * dt60);
                _grabRotationOffset = Quaternion.RotateTowards(_grabRotationOffset, Quaternion.identity, deltaAngle * gentleGrabReturnSpeed * dt60);
            }
            else if (!IsHolding)
            {
                float returnRate = Time.deltaTime * 15f;
                _grabPositionOffset = Vector3.Lerp(_grabPositionOffset, Vector3.zero, returnRate);
                _grabRotationOffset = Quaternion.Slerp(_grabRotationOffset, Quaternion.identity, returnRate);
            }
        }

        private void UpdateLastFrameFollow()
        {
            if (followTarget == null) return;
            _lastFrameFollowPosition = followTarget.position;
            _lastFrameFollowRotation = followTarget.rotation;
        }

        private void UpdatePositionHistory()
        {
            for (int i = _positionHistory.Length - 1; i > 0; i--)
                _positionHistory[i] = _positionHistory[i - 1];
            _positionHistory[0] = transform.localPosition;
        }

        private void UpdateThrowTracking()
        {
            if (!IsHolding || HeldObject == null) return;
            ThrowTracker.RecordVelocity(HeldObject.Rb.linearVelocity, HeldObject.Rb.angularVelocity);
        }

        public Vector3 GetSmoothedThrowVelocity() => ThrowTracker.GetThrowVelocity();
        public Vector3 GetSmoothedThrowAngularVelocity() => ThrowTracker.GetThrowAngularVelocity();
        public IVRInput GetInput() => InputManager.Instance?.Input;

        public void SetFollowTarget(Transform target)
        {
            followTarget = target;
            if (target != null)
            {
                _lastFollowPosition = target.position;
                _lastFollowRotation = target.rotation;
                _lastFrameFollowPosition = target.position;
                _lastFrameFollowRotation = target.rotation;
            }
        }

        public void SetPalmTransform(Transform palm) => palmTransform = palm;
        public void SetSide(HandSide handSide) => side = handSide;
        public void SetPositionStrength(float strength) => positionStrength = strength;
        public void SetRotationStrength(float strength) => rotationStrength = strength;
        public void SetThrowPower(float power) => throwPowerMultiplier = power;
        public void SetMaxFollowDistance(float distance) => maxFollowDistance = distance;

        public bool TryGrab(Grabbable target) => GrabHandler.TryGrab(target);
        public void ForceGrab(Grabbable target) => GrabHandler.ForceGrab(target);
        public void Release(bool applyThrow = true) => GrabHandler.Release(applyThrow);

        public float GetDistanceToFollow() =>
            followTarget != null ? Vector3.Distance(Rb.position, followTarget.position) : 0f;

        public float GetFingerCurl(FingerType finger) =>
            InputManager.Instance?.GetFingerCurl(side, finger) ?? 0f;

        public void TeleportTo(Vector3 position, Quaternion rotation)
        {
            Rb.position = position;
            Rb.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            Rb.linearVelocity = Vector3.zero;
            Rb.angularVelocity = Vector3.zero;
            _grabPositionOffset = Vector3.zero;
            _grabRotationOffset = Quaternion.identity;
            ThrowTracker.Clear();
        }
    }
}
