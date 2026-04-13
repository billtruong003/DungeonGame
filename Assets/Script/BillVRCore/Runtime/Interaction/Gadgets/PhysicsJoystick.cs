using System;
using UnityEngine;
using UnityEngine.Events;

namespace BillVRCore.Interaction.Gadgets
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Grabbable))]
    public class PhysicsJoystick : MonoBehaviour
    {
        [Header("Joystick")]
        [SerializeField] private float maxAngle = 30f;
        [SerializeField] private float springForce = 100f;
        [SerializeField] private float damperForce = 15f;
        [SerializeField] private float deadzone = 0.1f;

        [Header("Axis")]
        [SerializeField] private JoystickAxis xAxis = JoystickAxis.LocalX;
        [SerializeField] private JoystickAxis yAxis = JoystickAxis.LocalZ;
        [SerializeField] private bool invertX;
        [SerializeField] private bool invertY;

        [Header("Events")]
        [SerializeField] private UnityEvent<Vector2> onValueChanged;
        [SerializeField] private UnityEvent onCentered;

        public Vector2 Value { get; private set; }
        public Vector2 RawValue { get; private set; }
        public float XAxis => Value.x;
        public float YAxis => Value.y;
        public bool IsCentered => Value.sqrMagnitude < deadzone * deadzone;
        public event Action<Vector2> OnValueEvent;

        public enum JoystickAxis { LocalX, LocalZ }

        private Rigidbody _rb;
        private ConfigurableJoint _joint;
        private Quaternion _startRotation;
        private bool _wasCentered = true;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.linearDamping = 2f;
            _rb.angularDamping = 5f;

            _startRotation = transform.localRotation;
            SetupJoint();
        }

        private void SetupJoint()
        {
            _joint = gameObject.AddComponent<ConfigurableJoint>();

            _joint.xMotion = ConfigurableJointMotion.Locked;
            _joint.yMotion = ConfigurableJointMotion.Locked;
            _joint.zMotion = ConfigurableJointMotion.Locked;
            _joint.angularYMotion = ConfigurableJointMotion.Locked;

            _joint.angularXMotion = ConfigurableJointMotion.Limited;
            _joint.angularZMotion = ConfigurableJointMotion.Limited;

            _joint.lowAngularXLimit = new SoftJointLimit { limit = -maxAngle };
            _joint.highAngularXLimit = new SoftJointLimit { limit = maxAngle };
            _joint.angularZLimit = new SoftJointLimit { limit = maxAngle };

            var spring = new SoftJointLimitSpring { spring = springForce * 0.5f, damper = damperForce * 0.5f };
            _joint.angularXLimitSpring = spring;
            _joint.angularYZLimitSpring = spring;

            var drive = new JointDrive
            {
                positionSpring = springForce,
                positionDamper = damperForce,
                maximumForce = 1e+06f
            };
            _joint.angularXDrive = drive;
            _joint.angularYZDrive = drive;

            _joint.targetRotation = Quaternion.identity;
        }

        private void FixedUpdate()
        {
            Vector2 prev = Value;

            Quaternion delta = Quaternion.Inverse(_startRotation) * transform.localRotation;
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;

            float rawX = GetAxisValue(xAxis, delta);
            float rawY = GetAxisValue(yAxis, delta);

            rawX = Mathf.Clamp(rawX / maxAngle, -1f, 1f);
            rawY = Mathf.Clamp(rawY / maxAngle, -1f, 1f);

            if (invertX) rawX = -rawX;
            if (invertY) rawY = -rawY;

            RawValue = new Vector2(rawX, rawY);

            float x = Mathf.Abs(rawX) < deadzone ? 0f : rawX;
            float y = Mathf.Abs(rawY) < deadzone ? 0f : rawY;
            Value = new Vector2(x, y);

            if (Vector2.Distance(prev, Value) > 0.001f)
            {
                onValueChanged?.Invoke(Value);
                OnValueEvent?.Invoke(Value);
            }

            bool centered = IsCentered;
            if (centered && !_wasCentered)
                onCentered?.Invoke();
            _wasCentered = centered;
        }

        private float GetAxisValue(JoystickAxis axis, Quaternion delta)
        {
            Vector3 euler = delta.eulerAngles;

            float raw = axis switch
            {
                JoystickAxis.LocalX => euler.x,
                JoystickAxis.LocalZ => euler.z,
                _ => 0f
            };

            if (raw > 180f) raw -= 360f;
            return raw;
        }

        public void SetMaxAngle(float angle) { maxAngle = angle; }
        public void SetSpring(float force) { springForce = force; }
        public void SetDeadzone(float dz) { deadzone = dz; }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.02f);
            Gizmos.DrawRay(transform.position, transform.up * 0.1f);
        }
    }
}
