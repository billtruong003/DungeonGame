using System;
using UnityEngine;
using UnityEngine.Events;

namespace BillVRCore.Interaction.Gadgets
{
    [RequireComponent(typeof(Rigidbody))]
    public class PhysicsButton : MonoBehaviour
    {
        [Header("Button")]
        [SerializeField] private PressAxis pressAxis = PressAxis.Y;
        [SerializeField] private float pressDistance = 0.02f;
        [SerializeField] private float activationDepth = 0.6f;
        [SerializeField] private float springForce = 500f;
        [SerializeField] private float damper = 50f;

        [Header("Haptic")]
        [SerializeField] private float pressHaptic = 0.3f;

        [Header("Events")]
        [SerializeField] private UnityEvent onPressed;
        [SerializeField] private UnityEvent onReleased;

        public bool IsPressed { get; private set; }
        public float PressAmount { get; private set; }
        public event Action OnPressedEvent;
        public event Action OnReleasedEvent;

        public enum PressAxis { X, Y, Z, NegX, NegY, NegZ }

        private Rigidbody _rb;
        private ConfigurableJoint _joint;
        private Vector3 _startLocalPos;
        private bool _wasPressed;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.isKinematic = false;
            _rb.linearDamping = 2f;

            _startLocalPos = transform.localPosition;
            SetupJoint();
        }

        private void SetupJoint()
        {
            _joint = gameObject.AddComponent<ConfigurableJoint>();

            _joint.xMotion = ConfigurableJointMotion.Locked;
            _joint.yMotion = ConfigurableJointMotion.Locked;
            _joint.zMotion = ConfigurableJointMotion.Locked;
            _joint.angularXMotion = ConfigurableJointMotion.Locked;
            _joint.angularYMotion = ConfigurableJointMotion.Locked;
            _joint.angularZMotion = ConfigurableJointMotion.Locked;

            var limit = new SoftJointLimit { limit = pressDistance };
            var drive = new JointDrive
            {
                positionSpring = springForce,
                positionDamper = damper,
                maximumForce = 1e+06f
            };

            switch (GetSimpleAxis())
            {
                case 0:
                    _joint.xMotion = ConfigurableJointMotion.Limited;
                    _joint.linearLimit = limit;
                    _joint.xDrive = drive;
                    break;
                case 1:
                    _joint.yMotion = ConfigurableJointMotion.Limited;
                    _joint.linearLimit = limit;
                    _joint.yDrive = drive;
                    break;
                case 2:
                    _joint.zMotion = ConfigurableJointMotion.Limited;
                    _joint.linearLimit = limit;
                    _joint.zDrive = drive;
                    break;
            }
        }

        private void FixedUpdate()
        {
            Vector3 delta = _startLocalPos - transform.localPosition;
            float displacement = GetAxisDisplacement(delta);

            PressAmount = Mathf.Clamp01(displacement / pressDistance);

            _wasPressed = IsPressed;
            IsPressed = PressAmount >= activationDepth;

            if (IsPressed && !_wasPressed)
            {
                onPressed?.Invoke();
                OnPressedEvent?.Invoke();
                TryPlayHaptic();
            }
            else if (!IsPressed && _wasPressed)
            {
                onReleased?.Invoke();
                OnReleasedEvent?.Invoke();
            }
        }

        private float GetAxisDisplacement(Vector3 delta)
        {
            return pressAxis switch
            {
                PressAxis.X => delta.x,
                PressAxis.Y => delta.y,
                PressAxis.Z => delta.z,
                PressAxis.NegX => -delta.x,
                PressAxis.NegY => -delta.y,
                PressAxis.NegZ => -delta.z,
                _ => delta.y
            };
        }

        private int GetSimpleAxis()
        {
            return pressAxis switch
            {
                PressAxis.X or PressAxis.NegX => 0,
                PressAxis.Y or PressAxis.NegY => 1,
                _ => 2
            };
        }

        private void TryPlayHaptic()
        {
            if (pressHaptic <= 0f) return;

            var hands = FindObjectsByType<Hand.VRHand>(FindObjectsSortMode.None);
            foreach (var hand in hands)
            {
                float dist = Vector3.Distance(hand.transform.position, transform.position);
                if (dist < 0.15f)
                    hand.Haptics.PlayHaptic(pressHaptic, 0.04f);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 dir = pressAxis switch
            {
                PressAxis.X => transform.right,
                PressAxis.NegX => -transform.right,
                PressAxis.Y => transform.up,
                PressAxis.NegY => -transform.up,
                PressAxis.Z => transform.forward,
                PressAxis.NegZ => -transform.forward,
                _ => -transform.up
            };

            Gizmos.color = IsPressed ? Color.green : Color.yellow;
            Gizmos.DrawLine(transform.position, transform.position + dir * pressDistance);
            Gizmos.DrawWireSphere(transform.position + dir * pressDistance * activationDepth, 0.005f);
        }
    }
}
