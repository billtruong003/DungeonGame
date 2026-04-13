using System;
using UnityEngine;
using UnityEngine.Events;
using BillVRCore.Hand;

namespace BillVRCore.Interaction.Gadgets
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Grabbable))]
    public abstract class PhysicsGadgetBase : MonoBehaviour
    {
        [Header("Gadget")]
        [SerializeField] protected bool lockOnGrab = true;
        [SerializeField] protected float returnSpeed;
        [SerializeField] protected float hapticOnChange = 0.05f;

        [Header("Value Events")]
        [SerializeField] private UnityEvent<float> onValueChanged;

        public float CurrentValue { get; protected set; }
        public float PreviousValue { get; protected set; }
        public bool IsBeingUsed => _grabbable != null && _grabbable.IsHeld;
        public event Action<float> OnValueEvent;

        protected Rigidbody Rb;
        protected ConfigurableJoint Joint;
        private Grabbable _grabbable;

        protected virtual void Awake()
        {
            Rb = GetComponent<Rigidbody>();
            _grabbable = GetComponent<Grabbable>();

            Rb.useGravity = false;
            Rb.linearDamping = 5f;
            Rb.angularDamping = 5f;

            // Gadgets use their own ConfigurableJoint — disable GrabHandler
            // velocity tracking so it doesn't fight the gadget's joint.
            _grabbable.SetDisableGrabTracking(true);
            _grabbable.SetParentOnGrab(false);

            SetupJoint();
        }

        protected abstract void SetupJoint();
        protected abstract float CalculateValue();

        protected virtual void FixedUpdate()
        {
            PreviousValue = CurrentValue;
            CurrentValue = CalculateValue();

            if (!Mathf.Approximately(CurrentValue, PreviousValue))
            {
                onValueChanged?.Invoke(CurrentValue);
                OnValueEvent?.Invoke(CurrentValue);
                PlayChangeHaptic();
            }

            if (!IsBeingUsed && returnSpeed > 0f)
                ApplyReturnForce();
        }

        protected virtual void ApplyReturnForce() { }

        private void PlayChangeHaptic()
        {
            if (hapticOnChange <= 0f || !IsBeingUsed) return;
            if (_grabbable.HoldingHands.Count == 0) return;

            float delta = Mathf.Abs(CurrentValue - PreviousValue);
            if (delta < 0.01f) return;

            _grabbable.HoldingHands[0].Haptics.PlayHaptic(
                hapticOnChange * Mathf.Clamp01(delta * 10f), 0.02f);
        }

        protected ConfigurableJoint CreateJoint()
        {
            Joint = gameObject.AddComponent<ConfigurableJoint>();
            Joint.autoConfigureConnectedAnchor = true;

            JointDrive lockedDrive = new JointDrive
            {
                positionSpring = 1e+06f,
                positionDamper = 1e+04f,
                maximumForce = 1e+06f
            };

            Joint.xDrive = lockedDrive;
            Joint.yDrive = lockedDrive;
            Joint.zDrive = lockedDrive;
            Joint.angularXDrive = lockedDrive;
            Joint.angularYZDrive = lockedDrive;

            return Joint;
        }

        protected void LockAllMotion()
        {
            Joint.xMotion = ConfigurableJointMotion.Locked;
            Joint.yMotion = ConfigurableJointMotion.Locked;
            Joint.zMotion = ConfigurableJointMotion.Locked;
            Joint.angularXMotion = ConfigurableJointMotion.Locked;
            Joint.angularYMotion = ConfigurableJointMotion.Locked;
            Joint.angularZMotion = ConfigurableJointMotion.Locked;
        }
    }
}
