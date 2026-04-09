using UnityEngine;
using VRCore.Hand;

namespace VRCore.Interaction
{
    public class TwoHandGrabbable : Grabbable
    {
        [Header("Two Hand Settings")]
        [SerializeField] private Transform primaryGripPoint;
        [SerializeField] private Transform secondaryGripPoint;
        [SerializeField] private TwoHandRotation rotationType = TwoHandRotation.LookAtSecondary;
        [SerializeField] [Range(0f, 1f)] private float twoHandRotationBlend = 0.8f;

        public Transform PrimaryGrip => primaryGripPoint;
        public Transform SecondaryGrip => secondaryGripPoint;
        public bool IsTwoHanded => HeldHandCount >= 2;

        private VRHand _primaryHand;
        private VRHand _secondaryHand;
        private Quaternion _initialRelativeRotation;

        public enum TwoHandRotation { LookAtSecondary, AverageBoth }

        public override void OnGrab(VRHand hand)
        {
            if (HeldHandCount == 0)
            {
                _primaryHand = hand;
                base.OnGrab(hand);
                return;
            }

            if (HeldHandCount == 1 && hand != _primaryHand)
            {
                _secondaryHand = hand;
                _initialRelativeRotation = Quaternion.Inverse(
                    Quaternion.LookRotation(
                        _secondaryHand.transform.position - _primaryHand.transform.position))
                    * transform.rotation;
                base.OnGrab(hand);
                return;
            }

            base.OnGrab(hand);
        }

        public override void OnRelease(VRHand hand)
        {
            if (hand == _secondaryHand)
                _secondaryHand = null;
            else if (hand == _primaryHand)
            {
                _primaryHand = _secondaryHand;
                _secondaryHand = null;
            }

            base.OnRelease(hand);
        }

        private void FixedUpdate()
        {
            if (!IsTwoHanded || _primaryHand == null || _secondaryHand == null) return;

            ApplyTwoHandRotation();
        }

        private void ApplyTwoHandRotation()
        {
            Vector3 direction = _secondaryHand.transform.position - _primaryHand.transform.position;
            if (direction.sqrMagnitude < 0.0001f) return;

            Quaternion targetRotation;

            if (rotationType == TwoHandRotation.LookAtSecondary)
            {
                Vector3 upHint = _primaryHand.transform.up;
                targetRotation = Quaternion.LookRotation(direction, upHint) * _initialRelativeRotation;
            }
            else
            {
                targetRotation = Quaternion.Slerp(
                    _primaryHand.transform.rotation,
                    _secondaryHand.transform.rotation,
                    0.5f);
            }

            Quaternion current = Rb.rotation;
            Quaternion blended = Quaternion.Slerp(current, targetRotation, twoHandRotationBlend);

            Quaternion delta = blended * Quaternion.Inverse(current);
            delta.ToAngleAxis(out float angle, out Vector3 axis);

            if (float.IsInfinity(axis.x) || float.IsNaN(axis.x)) return;
            if (angle > 180f) angle -= 360f;

            Rb.angularVelocity = axis * (angle * Mathf.Deg2Rad / Time.fixedDeltaTime) * 0.5f;
        }

        protected override void Awake()
        {
            base.Awake();

            if (primaryGripPoint == null)
            {
                primaryGripPoint = new GameObject("PrimaryGrip").transform;
                primaryGripPoint.SetParent(transform, false);
            }

            if (secondaryGripPoint == null)
            {
                secondaryGripPoint = new GameObject("SecondaryGrip").transform;
                secondaryGripPoint.SetParent(transform, false);
                secondaryGripPoint.localPosition = Vector3.forward * 0.2f;
            }
        }
    }
}
