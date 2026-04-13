using System;
using UnityEngine;
using UnityEngine.Events;

namespace BillVRCore.Hand
{
    [RequireComponent(typeof(Collider))]
    public class PoseArea : MonoBehaviour
    {
        [Header("Pose")]
        [SerializeField] private HandPoseAsset poseAsset;
        [SerializeField] private HandPose inlinePose;
        [SerializeField] private bool useInlinePose;

        [Header("Transition")]
        [SerializeField] private float transitionTime = 0.2f;
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Events")]
        [SerializeField] private UnityEvent<VRHand> onHandEnter;
        [SerializeField] private UnityEvent<VRHand> onHandExit;

        public float TransitionTime => transitionTime;
        public AnimationCurve TransitionCurve => transitionCurve;

        public event Action<VRHand> OnHandEnterEvent;
        public event Action<VRHand> OnHandExitEvent;

        private void Awake()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            var hand = other.GetComponentInParent<VRHand>();
            if (hand == null || hand.IsHolding) return;

            var animator = hand.Animator;
            if (animator == null) return;

            animator.EnterPoseArea(this);
        }

        private void OnTriggerExit(Collider other)
        {
            var hand = other.GetComponentInParent<VRHand>();
            if (hand == null) return;

            var animator = hand.Animator;
            if (animator == null || animator.CurrentPoseArea != this) return;

            animator.ExitPoseArea();
        }

        public bool HasPose(HandSide side)
        {
            if (useInlinePose) return inlinePose.IsValid;
            return poseAsset != null && poseAsset.HasPose(side);
        }

        public HandPose GetPose(HandSide side)
        {
            if (useInlinePose) return inlinePose;
            if (poseAsset != null && poseAsset.HasPose(side))
                return poseAsset.GetPose(side);
            return HandPose.CreateEmpty();
        }

        public void NotifyHandEnter(VRHand hand)
        {
            onHandEnter?.Invoke(hand);
            OnHandEnterEvent?.Invoke(hand);
        }

        public void NotifyHandExit(VRHand hand)
        {
            onHandExit?.Invoke(hand);
            OnHandExitEvent?.Invoke(hand);
        }

        public void SetPoseAsset(HandPoseAsset asset) => poseAsset = asset;
        public void SetTransitionTime(float time) => transitionTime = time;
    }
}
