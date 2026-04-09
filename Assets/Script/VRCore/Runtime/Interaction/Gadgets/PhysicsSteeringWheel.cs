using System;
using UnityEngine;
using UnityEngine.Events;

namespace VRCore.Interaction.Gadgets
{
    [RequireComponent(typeof(Rigidbody))]
    public class PhysicsSteeringWheel : MonoBehaviour
    {
        [Header("Wheel")]
        [SerializeField] private float maxAngle = 450f;
        [SerializeField] private float springReturn = 20f;
        [SerializeField] private float damping = 5f;
        [SerializeField] private bool unlimited;

        [Header("Output")]
        [SerializeField] private UnityEvent<float> onAngleChanged;
        [SerializeField] private UnityEvent<float> onNormalizedChanged;

        public float CurrentAngle { get; private set; }
        public float NormalizedValue => unlimited ? 0f : Mathf.InverseLerp(-maxAngle, maxAngle, CurrentAngle);
        public float NormalizedSteering => Mathf.Clamp(CurrentAngle / maxAngle, -1f, 1f);
        public event Action<float> OnAngleEvent;

        private Rigidbody _rb;
        private ConfigurableJoint _joint;
        private float _previousAngle;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.angularDamping = damping;

            SetupJoint();
        }

        private void SetupJoint()
        {
            _joint = gameObject.AddComponent<ConfigurableJoint>();
            _joint.xMotion = ConfigurableJointMotion.Locked;
            _joint.yMotion = ConfigurableJointMotion.Locked;
            _joint.zMotion = ConfigurableJointMotion.Locked;
            _joint.angularXMotion = ConfigurableJointMotion.Locked;
            _joint.angularZMotion = ConfigurableJointMotion.Locked;

            if (unlimited)
            {
                _joint.angularYMotion = ConfigurableJointMotion.Free;
            }
            else
            {
                _joint.angularYMotion = ConfigurableJointMotion.Limited;
                _joint.angularYLimit = new SoftJointLimit { limit = maxAngle };
                _joint.angularYZLimitSpring = new SoftJointLimitSpring
                {
                    spring = springReturn,
                    damper = damping
                };
            }

            _joint.angularYZDrive = new JointDrive
            {
                positionSpring = springReturn,
                positionDamper = damping,
                maximumForce = 1e+06f
            };
            _joint.targetRotation = Quaternion.identity;
        }

        private void FixedUpdate()
        {
            _previousAngle = CurrentAngle;

            float raw = transform.localEulerAngles.y;
            if (raw > 180f) raw -= 360f;

            if (!unlimited)
                raw = Mathf.Clamp(raw, -maxAngle, maxAngle);

            CurrentAngle = raw;

            if (Mathf.Abs(CurrentAngle - _previousAngle) > 0.1f)
            {
                onAngleChanged?.Invoke(CurrentAngle);
                onNormalizedChanged?.Invoke(NormalizedSteering);
                OnAngleEvent?.Invoke(CurrentAngle);
            }
        }

        public void SetMaxAngle(float angle) => maxAngle = angle;
        public void SetSpringReturn(float force) => springReturn = force;
    }
}
