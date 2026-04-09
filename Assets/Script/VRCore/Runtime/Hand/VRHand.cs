using UnityEngine;
using VRCore.Input;
using VRCore.Interaction;

namespace VRCore.Hand
{
    [DefaultExecutionOrder(-10)]
    [RequireComponent(typeof(Rigidbody))]
    public class VRHand : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private HandSide side;
        [SerializeField] private Transform followTarget;
        [SerializeField] private Transform palmTransform;

        [Header("Physics Follow")]
        [SerializeField] private float positionStrength = 60f;
        [SerializeField] private float rotationStrength = 40f;
        [SerializeField] private float maxFollowDistance = 0.5f;
        [SerializeField] private float maxVelocity = 20f;

        [Header("Throw")]
        [SerializeField] private float throwPowerMultiplier = 1.2f;

        public HandSide Side => side;
        public Transform FollowTarget => followTarget;
        public Transform PalmTransform => palmTransform;
        public Rigidbody Rb { get; private set; }
        public GrabHandler GrabHandler { get; private set; }
        public HandHaptics Haptics { get; private set; }
        public HandAnimator Animator { get; private set; }
        public float ThrowPower => throwPowerMultiplier;
        public Vector3 Velocity => Rb.linearVelocity;
        public Vector3 AngularVelocity => Rb.angularVelocity;
        public float Speed => Rb.linearVelocity.magnitude;
        public bool IsHolding => GrabHandler != null && GrabHandler.IsHolding;
        public Grabbable HeldObject => GrabHandler?.HeldObject;

        private readonly Vector3[] _velocityHistory = new Vector3[5];
        private int _velocityIndex;

        private void Awake()
        {
            Rb = GetComponent<Rigidbody>();
            ConfigureRigidbody();

            GrabHandler = gameObject.GetOrAddComponent<GrabHandler>();
            Haptics = gameObject.GetOrAddComponent<HandHaptics>();
            Animator = GetComponent<HandAnimator>();
        }

        private void ConfigureRigidbody()
        {
            Rb.useGravity = false;
            Rb.isKinematic = false;
            Rb.interpolation = RigidbodyInterpolation.Interpolate;
            Rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Rb.mass = 1f;
            Rb.linearDamping = 0f;
            Rb.angularDamping = 5f;
        }

        private void FixedUpdate()
        {
            if (followTarget == null) return;
            TrackVelocity();
            PhysicsFollow();
            CheckTeleport();
        }

        private void PhysicsFollow()
        {
            Vector3 positionDelta = followTarget.position - Rb.position;
            Vector3 targetVelocity = positionDelta / Time.fixedDeltaTime * positionStrength * Time.fixedDeltaTime;
            targetVelocity = Vector3.ClampMagnitude(targetVelocity, maxVelocity);
            Rb.linearVelocity = targetVelocity;

            Quaternion rotationDelta = followTarget.rotation * Quaternion.Inverse(Rb.rotation);
            rotationDelta.ToAngleAxis(out float angle, out Vector3 axis);
            if (float.IsInfinity(axis.x) || float.IsNaN(axis.x)) return;
            if (angle > 180f) angle -= 360f;

            Rb.angularVelocity = axis * (angle * Mathf.Deg2Rad * rotationStrength);
        }

        private void CheckTeleport()
        {
            if (Vector3.Distance(Rb.position, followTarget.position) <= maxFollowDistance) return;

            Rb.position = followTarget.position;
            Rb.rotation = followTarget.rotation;
            Rb.linearVelocity = Vector3.zero;
            Rb.angularVelocity = Vector3.zero;

            if (IsHolding) GrabHandler.ForceRelease();
        }

        private void TrackVelocity()
        {
            _velocityHistory[_velocityIndex] = Rb.linearVelocity;
            _velocityIndex = (_velocityIndex + 1) % _velocityHistory.Length;
        }

        public Vector3 GetSmoothedThrowVelocity()
        {
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < _velocityHistory.Length; i++)
                sum += _velocityHistory[i];
            return sum / _velocityHistory.Length * throwPowerMultiplier;
        }

        public IVRInput GetInput() => InputManager.Instance?.Input;

        public void SetFollowTarget(Transform target) => followTarget = target;
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
    }
}
