using UnityEngine;
using BillVRCore.Hand;
using BillVRCore.Interaction;

namespace BillVRCore.Locomotion
{
    public class PushProvider : MonoBehaviour, ILocomotionProvider
    {
        [Header("References")]
        [SerializeField] private VRHand leftHand;
        [SerializeField] private VRHand rightHand;

        [Header("Default Push")]
        [SerializeField] private Vector3 defaultStrength = Vector3.one;
        [SerializeField] private float defaultAcceleration = 20f;
        [SerializeField] private float defaultDrag = 10f;

        public bool IsActive => _activePushes > 0;
        public LocomotionState ProvidedState => LocomotionState.Idle;
        public int Priority => 5;

        private Rigidbody _playerRb;
        private int _activePushes;
        private Vector3 _leftPrevPos;
        private Vector3 _rightPrevPos;

        private void Awake()
        {
            _playerRb = GetComponentInParent<Rigidbody>();
        }

        private void Start()
        {
            FindHands();
            RegisterCollisionHandlers();
        }

        private void RegisterCollisionHandlers()
        {
            if (leftHand != null)
            {
                var handler = leftHand.gameObject.GetOrAddComponent<PushCollisionForwarder>();
                handler.Initialize(this, leftHand);
            }
            if (rightHand != null)
            {
                var handler = rightHand.gameObject.GetOrAddComponent<PushCollisionForwarder>();
                handler.Initialize(this, rightHand);
            }
        }

        public void OnHandCollisionStay(VRHand hand, Collision collision)
        {
            var pushable = collision.gameObject.GetComponent<Pushable>();
            if (pushable == null)
                pushable = collision.gameObject.GetComponentInParent<Pushable>();
            if (pushable == null) return;

            Vector3 followPos = hand.FollowTarget != null ? hand.FollowTarget.position : hand.transform.position;
            Vector3 delta = followPos - hand.transform.position;

            Vector3 strength = pushable.Strength;
            Vector3 pushForce = Vector3.Scale(delta, strength);

            float accel = pushable.Acceleration > 0f ? pushable.Acceleration : defaultAcceleration;
            float drag = pushable.Drag > 0f ? pushable.Drag : defaultDrag;
            _playerRb.linearVelocity = Vector3.Lerp(_playerRb.linearVelocity,
                pushForce / Time.fixedDeltaTime, accel * Time.fixedDeltaTime);
            _playerRb.linearVelocity *= 1f / (1f + drag * Time.fixedDeltaTime);

            _activePushes++;
        }

        private void FixedUpdate()
        {
            _activePushes = 0;
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

    public class PushCollisionForwarder : MonoBehaviour
    {
        private PushProvider _provider;
        private VRHand _hand;

        public void Initialize(PushProvider provider, VRHand hand)
        {
            _provider = provider;
            _hand = hand;
        }

        private void OnCollisionStay(Collision collision)
        {
            if (_provider != null && _hand != null)
                _provider.OnHandCollisionStay(_hand, collision);
        }
    }
}
