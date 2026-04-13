using UnityEngine;

namespace BillVRCore.Interaction.Gadgets
{
    public class PhysicsDial : PhysicsGadgetBase
    {
        [Header("Dial")]
        [SerializeField] private RotationAxis rotationAxis = RotationAxis.Y;
        [SerializeField] private float minAngle = -180f;
        [SerializeField] private float maxAngle = 180f;
        [SerializeField] private bool unlimited;

        [Header("Stepping")]
        [SerializeField] private bool useSteps;
        [SerializeField] private int stepCount = 10;
        [SerializeField] private float stepHapticStrength = 0.15f;

        public float NormalizedValue => unlimited ? GetUnlimitedNormalized() : Mathf.InverseLerp(minAngle, maxAngle, _accumulatedAngle);
        public float RawAngle => _accumulatedAngle;
        public int CurrentStep => useSteps ? Mathf.RoundToInt(NormalizedValue * stepCount) : -1;

        public enum RotationAxis { X, Y, Z }

        private float _accumulatedAngle;
        private float _previousFrameAngle;
        private int _lastStep = -1;

        protected override void SetupJoint()
        {
            CreateJoint();
            LockAllMotion();

            if (unlimited)
            {
                switch (rotationAxis)
                {
                    case RotationAxis.X: Joint.angularXMotion = ConfigurableJointMotion.Free; break;
                    case RotationAxis.Y: Joint.angularYMotion = ConfigurableJointMotion.Free; break;
                    case RotationAxis.Z: Joint.angularZMotion = ConfigurableJointMotion.Free; break;
                }
            }
            else
            {
                switch (rotationAxis)
                {
                    case RotationAxis.X:
                        Joint.angularXMotion = ConfigurableJointMotion.Limited;
                        Joint.lowAngularXLimit = new SoftJointLimit { limit = minAngle };
                        Joint.highAngularXLimit = new SoftJointLimit { limit = maxAngle };
                        break;
                    case RotationAxis.Y:
                        Joint.angularYMotion = ConfigurableJointMotion.Limited;
                        Joint.angularYLimit = new SoftJointLimit { limit = Mathf.Max(Mathf.Abs(minAngle), Mathf.Abs(maxAngle)) };
                        break;
                    case RotationAxis.Z:
                        Joint.angularZMotion = ConfigurableJointMotion.Limited;
                        Joint.angularZLimit = new SoftJointLimit { limit = Mathf.Max(Mathf.Abs(minAngle), Mathf.Abs(maxAngle)) };
                        break;
                }
            }
        }

        protected override float CalculateValue()
        {
            float currentAngle = GetLocalAxisAngle();
            float delta = Mathf.DeltaAngle(_previousFrameAngle, currentAngle);
            _accumulatedAngle += delta;
            _previousFrameAngle = currentAngle;

            if (!unlimited)
                _accumulatedAngle = Mathf.Clamp(_accumulatedAngle, minAngle, maxAngle);

            CheckStepDetent();
            return NormalizedValue;
        }

        private void CheckStepDetent()
        {
            if (!useSteps || stepCount <= 0) return;

            int step = CurrentStep;
            if (step != _lastStep)
            {
                _lastStep = step;
                PlayStepHaptic();
            }
        }

        private void PlayStepHaptic()
        {
            if (!IsBeingUsed || stepHapticStrength <= 0f) return;

            var grabbable = GetComponent<Grabbable>();
            if (grabbable == null || grabbable.HoldingHands.Count == 0) return;

            grabbable.HoldingHands[0].Haptics.PlayHaptic(stepHapticStrength, 0.03f);
        }

        private float GetLocalAxisAngle()
        {
            Vector3 euler = transform.localEulerAngles;
            float raw = rotationAxis switch
            {
                RotationAxis.X => euler.x,
                RotationAxis.Y => euler.y,
                _ => euler.z
            };

            if (raw > 180f) raw -= 360f;
            return raw;
        }

        private float GetUnlimitedNormalized()
        {
            return (_accumulatedAngle % 360f) / 360f;
        }
    }
}
