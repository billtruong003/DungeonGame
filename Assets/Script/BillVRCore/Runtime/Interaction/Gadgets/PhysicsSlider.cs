using UnityEngine;

namespace BillVRCore.Interaction.Gadgets
{
    public class PhysicsSlider : PhysicsGadgetBase
    {
        [Header("Slider")]
        [SerializeField] private SlideAxis axis = SlideAxis.Z;
        [SerializeField] private float minDistance = -0.1f;
        [SerializeField] private float maxDistance = 0.1f;
        [SerializeField] private float springForce = 100f;
        [SerializeField] private float damperForce = 20f;

        [Header("Snap")]
        [SerializeField] private bool snapToEnds;
        [SerializeField] private float snapThreshold = 0.1f;

        public float NormalizedValue => Mathf.InverseLerp(minDistance, maxDistance, GetAxisPosition());

        public enum SlideAxis { X, Y, Z }

        protected override void SetupJoint()
        {
            CreateJoint();
            LockAllMotion();

            var limit = new SoftJointLimit();

            switch (axis)
            {
                case SlideAxis.X:
                    Joint.xMotion = ConfigurableJointMotion.Limited;
                    limit.limit = (maxDistance - minDistance) * 0.5f;
                    Joint.linearLimit = limit;
                    Joint.xDrive = CreateSlideDrive();
                    break;
                case SlideAxis.Y:
                    Joint.yMotion = ConfigurableJointMotion.Limited;
                    limit.limit = (maxDistance - minDistance) * 0.5f;
                    Joint.linearLimit = limit;
                    Joint.yDrive = CreateSlideDrive();
                    break;
                case SlideAxis.Z:
                    Joint.zMotion = ConfigurableJointMotion.Limited;
                    limit.limit = (maxDistance - minDistance) * 0.5f;
                    Joint.linearLimit = limit;
                    Joint.zDrive = CreateSlideDrive();
                    break;
            }
        }

        protected override float CalculateValue()
        {
            return NormalizedValue;
        }

        protected override void ApplyReturnForce()
        {
            float pos = GetAxisPosition();
            float target;

            if (snapToEnds)
            {
                float normalized = Mathf.InverseLerp(minDistance, maxDistance, pos);
                target = normalized >= (1f - snapThreshold) ? maxDistance
                       : normalized <= snapThreshold ? minDistance
                       : (minDistance + maxDistance) * 0.5f;
            }
            else
            {
                target = (minDistance + maxDistance) * 0.5f;
            }

            float force = (target - pos) * returnSpeed;

            Vector3 forceDir = axis switch
            {
                SlideAxis.X => transform.right * force,
                SlideAxis.Y => transform.up * force,
                _ => transform.forward * force
            };

            Rb.AddForce(forceDir, ForceMode.Force);
        }

        private float GetAxisPosition()
        {
            Vector3 localPos = Joint != null
                ? Joint.connectedAnchor - transform.localPosition
                : transform.localPosition;

            float raw = axis switch
            {
                SlideAxis.X => transform.localPosition.x,
                SlideAxis.Y => transform.localPosition.y,
                _ => transform.localPosition.z
            };

            return Mathf.Clamp(raw, minDistance, maxDistance);
        }

        private JointDrive CreateSlideDrive()
        {
            return new JointDrive
            {
                positionSpring = springForce,
                positionDamper = damperForce,
                maximumForce = 1e+06f
            };
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 dir = axis switch
            {
                SlideAxis.X => transform.right,
                SlideAxis.Y => transform.up,
                _ => transform.forward
            };

            Vector3 center = transform.position;
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(center + dir * minDistance, center + dir * maxDistance);
            Gizmos.DrawWireSphere(center + dir * minDistance, 0.01f);
            Gizmos.DrawWireSphere(center + dir * maxDistance, 0.01f);
        }
    }
}
