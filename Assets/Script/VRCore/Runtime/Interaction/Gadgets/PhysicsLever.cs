using UnityEngine;

namespace VRCore.Interaction.Gadgets
{
    public class PhysicsLever : PhysicsGadgetBase
    {
        [Header("Lever")]
        [SerializeField] private RotationAxis rotationAxis = RotationAxis.X;
        [SerializeField] private float minAngle = -45f;
        [SerializeField] private float maxAngle = 45f;
        [SerializeField] private float springTorque = 50f;
        [SerializeField] private float damperTorque = 10f;

        public float NormalizedValue => Mathf.InverseLerp(minAngle, maxAngle, GetCurrentAngle());
        public float CurrentAngle => GetCurrentAngle();

        public enum RotationAxis { X, Y, Z }

        private Quaternion _startRotation;

        protected override void Awake()
        {
            _startRotation = transform.localRotation;
            base.Awake();
        }

        protected override void SetupJoint()
        {
            CreateJoint();
            LockAllMotion();

            var lowLimit = new SoftJointLimit { limit = minAngle };
            var highLimit = new SoftJointLimit { limit = maxAngle };
            var spring = new SoftJointLimitSpring { spring = springTorque, damper = damperTorque };

            switch (rotationAxis)
            {
                case RotationAxis.X:
                    Joint.angularXMotion = ConfigurableJointMotion.Limited;
                    Joint.lowAngularXLimit = lowLimit;
                    Joint.highAngularXLimit = highLimit;
                    Joint.angularXLimitSpring = spring;
                    break;
                case RotationAxis.Y:
                    Joint.angularYMotion = ConfigurableJointMotion.Limited;
                    Joint.angularYLimit = new SoftJointLimit { limit = Mathf.Max(Mathf.Abs(minAngle), Mathf.Abs(maxAngle)) };
                    Joint.angularYZLimitSpring = spring;
                    break;
                case RotationAxis.Z:
                    Joint.angularZMotion = ConfigurableJointMotion.Limited;
                    Joint.angularZLimit = new SoftJointLimit { limit = Mathf.Max(Mathf.Abs(minAngle), Mathf.Abs(maxAngle)) };
                    Joint.angularYZLimitSpring = spring;
                    break;
            }

            Joint.angularXDrive = new JointDrive
            {
                positionSpring = springTorque,
                positionDamper = damperTorque,
                maximumForce = 1e+06f
            };
        }

        protected override float CalculateValue()
        {
            return NormalizedValue;
        }

        protected override void ApplyReturnForce()
        {
            float current = GetCurrentAngle();
            float torque = -current * returnSpeed;

            Vector3 torqueDir = rotationAxis switch
            {
                RotationAxis.X => transform.right * torque,
                RotationAxis.Y => transform.up * torque,
                _ => transform.forward * torque
            };

            Rb.AddTorque(torqueDir, ForceMode.Force);
        }

        private float GetCurrentAngle()
        {
            Quaternion delta = transform.localRotation * Quaternion.Inverse(_startRotation);
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;

            float projected = rotationAxis switch
            {
                RotationAxis.X => axis.x * angle,
                RotationAxis.Y => axis.y * angle,
                _ => axis.z * angle
            };

            return Mathf.Clamp(projected, minAngle, maxAngle);
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 axisDir = rotationAxis switch
            {
                RotationAxis.X => transform.right,
                RotationAxis.Y => transform.up,
                _ => transform.forward
            };

            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, axisDir * 0.15f);
        }
    }
}
