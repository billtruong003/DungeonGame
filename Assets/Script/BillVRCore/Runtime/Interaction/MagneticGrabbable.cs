using UnityEngine;
using BillVRCore.Hand;

namespace BillVRCore.Interaction
{
    [RequireComponent(typeof(Grabbable))]
    public class MagneticGrabbable : MonoBehaviour
    {
        [Header("Magnetic Pull")]
        [SerializeField] private float magnetRadius = 0.3f;
        [SerializeField] private float pullForce = 8f;
        [SerializeField] private float alignTorque = 5f;
        [SerializeField] private float pullDamping = 3f;

        [Header("Snap")]
        [SerializeField] private Transform snapPoint;
        [SerializeField] private float snapDistance = 0.08f;

        [Header("Filter")]
        [SerializeField] private bool onlyWhenGripHeld = true;

        public bool IsMagnetActive { get; private set; }
        public VRHand NearestHand { get; private set; }

        private Grabbable _grabbable;
        private Rigidbody _rb;
        private readonly Collider[] _handBuffer = new Collider[4];

        private void Awake()
        {
            _grabbable = GetComponent<Grabbable>();
            _rb = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            if (_grabbable.IsHeld) { IsMagnetActive = false; return; }

            NearestHand = FindNearestHand();
            IsMagnetActive = NearestHand != null;

            if (!IsMagnetActive) return;

            ApplyMagneticForce(NearestHand);
        }

        private VRHand FindNearestHand()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, magnetRadius,
                _handBuffer, 1 << LayerMask.NameToLayer("HandPhysics"));

            VRHand closest = null;
            float closestDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var hand = _handBuffer[i].GetComponentInParent<VRHand>();
                if (hand == null || hand.GrabHandler.IsHolding) continue;

                if (onlyWhenGripHeld)
                {
                    var input = hand.GetInput();
                    if (input == null || input.GripStrength(hand.Side) < 0.3f) continue;
                }

                float dist = Vector3.Distance(transform.position, hand.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = hand;
                }
            }

            return closest;
        }

        private void ApplyMagneticForce(VRHand hand)
        {
            Vector3 targetPos = hand.PalmTransform != null ? hand.PalmTransform.position : hand.transform.position;
            Vector3 toHand = targetPos - transform.position;
            float distance = toHand.magnitude;

            if (distance < 0.001f) return;

            float strength = 1f - Mathf.Clamp01(distance / magnetRadius);
            strength *= strength;

            Vector3 force = toHand.normalized * (pullForce * strength);
            force -= _rb.linearVelocity * pullDamping;
            _rb.AddForce(force, ForceMode.Acceleration);

            if (snapPoint != null)
            {
                Quaternion targetRot = hand.transform.rotation;
                Quaternion delta = targetRot * Quaternion.Inverse(transform.rotation);
                delta.ToAngleAxis(out float angle, out Vector3 axis);
                if (!float.IsInfinity(axis.x) && angle > 0.1f)
                {
                    if (angle > 180f) angle -= 360f;
                    _rb.AddTorque(axis * (angle * Mathf.Deg2Rad * alignTorque * strength), ForceMode.Acceleration);
                }
            }

            if (distance < snapDistance)
            {
                hand.GrabHandler.TryGrab(_grabbable);
                IsMagnetActive = false;
            }
        }

        public void SetMagnetRadius(float radius) => magnetRadius = radius;
        public void SetPullForce(float force) => pullForce = force;
        public void SetSnapDistance(float dist) => snapDistance = dist;
    }
}
