using UnityEngine;
using VRCore.Hand;
using VRCore.Interaction;

namespace VRCore.Locomotion
{
    public class ClimbProvider : MonoBehaviour, ILocomotionProvider
    {
        [Header("References")]
        [SerializeField] private VRHand leftHand;
        [SerializeField] private VRHand rightHand;

        [Header("Climbing")]
        [SerializeField] private Vector3 climbStrength = new(1f, 1f, 1f);
        [SerializeField] private float climbAcceleration = 30f;
        [SerializeField] private float climbDrag = 15f;

        public bool IsActive => _activeClimbHands > 0;
        public LocomotionState ProvidedState => LocomotionState.Climbing;
        public int Priority => 50;

        private Rigidbody _playerRb;
        private int _activeClimbHands;
        private Vector3 _leftPrevPos;
        private Vector3 _rightPrevPos;
        private bool _leftClimbing;
        private bool _rightClimbing;

        private void Awake()
        {
            _playerRb = GetComponentInParent<Rigidbody>();
        }

        private void Start()
        {
            FindHands();
        }

        private void FixedUpdate()
        {
            if (_playerRb == null) return;

            _activeClimbHands = 0;

            if (leftHand != null)
                ProcessClimbHand(leftHand, ref _leftPrevPos, ref _leftClimbing);
            if (rightHand != null)
                ProcessClimbHand(rightHand, ref _rightPrevPos, ref _rightClimbing);

            if (_activeClimbHands > 0)
            {
                _playerRb.useGravity = false;
                _playerRb.linearDamping = climbDrag;
            }
            else
            {
                _playerRb.useGravity = true;
            }
        }

        private void ProcessClimbHand(VRHand hand, ref Vector3 prevPos, ref bool climbing)
        {
            bool wasClimbing = climbing;
            climbing = false;

            if (hand.GrabHandler == null || !hand.GrabHandler.IsHolding)
            {
                prevPos = hand.FollowTarget != null ? hand.FollowTarget.position : hand.transform.position;
                return;
            }

            var climbable = hand.GrabHandler.HeldObject.GetComponent<Climbable>();
            if (climbable == null)
            {
                prevPos = hand.FollowTarget != null ? hand.FollowTarget.position : hand.transform.position;
                return;
            }

            climbing = true;
            _activeClimbHands++;

            Vector3 currentPos = hand.FollowTarget != null ? hand.FollowTarget.position : hand.transform.position;

            if (!wasClimbing)
            {
                prevPos = currentPos;
                return;
            }

            Vector3 delta = prevPos - currentPos;
            delta = Vector3.Scale(delta, climbStrength * climbable.StrengthMultiplier);

            Vector3 targetVel = delta / Time.fixedDeltaTime;
            _playerRb.linearVelocity = Vector3.Lerp(_playerRb.linearVelocity, targetVel,
                climbAcceleration * Time.fixedDeltaTime);

            prevPos = currentPos;
        }

        public void SetLocomotionActive(bool active) { }

        private void FindHands()
        {
            if (leftHand != null && rightHand != null) return;

            var hands = FindObjectsByType<VRHand>(FindObjectsSortMode.None);
            foreach (var hand in hands)
            {
                if (hand.Side == HandSide.Left && leftHand == null) leftHand = hand;
                if (hand.Side == HandSide.Right && rightHand == null) rightHand = hand;
            }
        }
    }
}
