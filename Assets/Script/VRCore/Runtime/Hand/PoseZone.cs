using UnityEngine;

namespace VRCore.Hand
{
    [RequireComponent(typeof(Collider))]
    public class PoseZone : MonoBehaviour
    {
        [Header("Pose")]
        [SerializeField] private HandPoseData targetPose;
        [SerializeField] private float blendSpeed = 8f;
        [SerializeField] [Range(0f, 1f)] private float maxBlend = 1f;

        [Header("Filter")]
        [SerializeField] private HandSide handFilter = HandSide.Left;
        [SerializeField] private bool filterByHand;

        public bool IsHandInZone { get; private set; }

        private HandAnimator _activeAnimator;
        private float _currentBlend;

        private void Awake()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            var hand = other.GetComponentInParent<VRHand>();
            if (hand == null) return;
            if (filterByHand && hand.Side != handFilter) return;
            if (hand.GrabHandler.IsHolding) return;

            _activeAnimator = hand.Animator;
            IsHandInZone = true;
        }

        private void OnTriggerExit(Collider other)
        {
            var hand = other.GetComponentInParent<VRHand>();
            if (hand == null || hand.Animator != _activeAnimator) return;

            IsHandInZone = false;
        }

        private void Update()
        {
            float target = IsHandInZone ? maxBlend : 0f;
            _currentBlend = Mathf.MoveTowards(_currentBlend, target, Time.deltaTime * blendSpeed);

            if (_activeAnimator == null) return;

            if (_currentBlend > 0.001f)
                _activeAnimator.SetOverridePose(targetPose, _currentBlend);
            else
                _activeAnimator.ClearOverridePose();
        }

        public void SetPose(HandPoseData pose) => targetPose = pose;
        public void SetBlendSpeed(float speed) => blendSpeed = speed;
    }
}
