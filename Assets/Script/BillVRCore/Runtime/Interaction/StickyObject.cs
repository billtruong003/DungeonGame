using System;
using UnityEngine;

namespace BillVRCore.Interaction
{
    [RequireComponent(typeof(Grabbable))]
    public class StickyObject : MonoBehaviour
    {
        [Header("Stick Settings")]
        [SerializeField] private float minStickVelocity = 1f;
        [SerializeField] private float stickBreakForce = 500f;
        [SerializeField] private float stickPenetration = 0.01f;
        [SerializeField] private LayerMask stickLayers = ~0;

        [Header("Lifetime")]
        [SerializeField] private float autoUnstickTime;
        [SerializeField] private bool destroyOnUnstick;

        public bool IsStuck { get; private set; }
        public GameObject StuckTo { get; private set; }
        public event Action<StickyObject, Collision> OnStuck;
        public event Action<StickyObject> OnUnstuck;

        private Grabbable _grabbable;
        private Rigidbody _rb;
        private FixedJoint _stickJoint;
        private float _stuckTime;

        private void Awake()
        {
            _grabbable = GetComponent<Grabbable>();
            _rb = GetComponent<Rigidbody>();
            _grabbable.OnGrabEvent += OnGrabbed;
        }

        private void Update()
        {
            if (!IsStuck || autoUnstickTime <= 0f) return;

            if (Time.time - _stuckTime > autoUnstickTime)
                Unstick();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (IsStuck || _grabbable.IsHeld) return;
            if ((stickLayers & (1 << collision.gameObject.layer)) == 0) return;
            if (collision.relativeVelocity.magnitude < minStickVelocity) return;

            Stick(collision);
        }

        public void Stick(Collision collision)
        {
            ContactPoint contact = collision.GetContact(0);

            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            transform.position = contact.point + contact.normal * stickPenetration;

            var targetRb = collision.rigidbody;
            if (targetRb != null)
            {
                _stickJoint = gameObject.AddComponent<FixedJoint>();
                _stickJoint.connectedBody = targetRb;
                _stickJoint.breakForce = stickBreakForce;
                _stickJoint.breakTorque = stickBreakForce;
            }
            else
            {
                _rb.isKinematic = true;
                transform.SetParent(collision.transform);
            }

            IsStuck = true;
            StuckTo = collision.gameObject;
            _stuckTime = Time.time;
            OnStuck?.Invoke(this, collision);
        }

        public void Unstick()
        {
            if (!IsStuck) return;

            if (_stickJoint != null)
                Destroy(_stickJoint);

            _rb.isKinematic = false;
            transform.SetParent(null);

            IsStuck = false;
            var prev = StuckTo;
            StuckTo = null;
            OnUnstuck?.Invoke(this);

            if (destroyOnUnstick)
                Destroy(gameObject, 0.1f);
        }

        private void OnGrabbed(Hand.VRHand hand, Grabbable grab)
        {
            if (IsStuck) Unstick();
        }

        private void OnJointBreak(float breakForce)
        {
            IsStuck = false;
            StuckTo = null;
            OnUnstuck?.Invoke(this);
        }

        private void OnDestroy()
        {
            if (_grabbable != null)
                _grabbable.OnGrabEvent -= OnGrabbed;
        }

        public void SetMinStickVelocity(float vel) => minStickVelocity = vel;
        public void SetBreakForce(float force) => stickBreakForce = force;
    }
}
