using UnityEngine;
using UnityEngine.Events;

namespace VRCore.Interaction.Gadgets
{
    public class PhysicsHinge : PhysicsGadgetBase
    {
        [Header("Hinge")]
        [SerializeField] private float minAngle = 0f;
        [SerializeField] private float maxAngle = 120f;
        [SerializeField] private float springTorque = 20f;
        [SerializeField] private float damperTorque = 5f;

        [Header("State Events")]
        [SerializeField] private float openThreshold = 0.8f;
        [SerializeField] private float closeThreshold = 0.1f;
        [SerializeField] private UnityEvent onOpened;
        [SerializeField] private UnityEvent onClosed;

        public float NormalizedAngle => Mathf.InverseLerp(minAngle, maxAngle, GetHingeAngle());
        public bool IsOpen { get; private set; }

        private float _startAngle;
        private bool _wasOpen;

        protected override void Awake()
        {
            _startAngle = transform.localEulerAngles.x;
            if (_startAngle > 180f) _startAngle -= 360f;
            base.Awake();
        }

        protected override void SetupJoint()
        {
            CreateJoint();
            LockAllMotion();

            Joint.angularXMotion = ConfigurableJointMotion.Limited;
            Joint.lowAngularXLimit = new SoftJointLimit { limit = minAngle };
            Joint.highAngularXLimit = new SoftJointLimit { limit = maxAngle };
            Joint.angularXLimitSpring = new SoftJointLimitSpring
            {
                spring = springTorque,
                damper = damperTorque
            };
        }

        protected override float CalculateValue()
        {
            float normalized = NormalizedAngle;

            _wasOpen = IsOpen;
            IsOpen = normalized >= openThreshold;

            if (IsOpen && !_wasOpen) onOpened?.Invoke();
            if (!IsOpen && _wasOpen && normalized <= closeThreshold) onClosed?.Invoke();

            return normalized;
        }

        protected override void ApplyReturnForce()
        {
            float angle = GetHingeAngle();
            Rb.AddTorque(transform.right * (-angle * returnSpeed), ForceMode.Force);
        }

        private float GetHingeAngle()
        {
            float current = transform.localEulerAngles.x;
            if (current > 180f) current -= 360f;
            return Mathf.Clamp(current - _startAngle, minAngle, maxAngle);
        }
    }
}
