using UnityEngine;
using VRCore.Hand;

namespace VRCore.Locomotion
{
    public class GorillaMoveProvider : MonoBehaviour, ILocomotionProvider
    {
        [Header("References")]
        [SerializeField] private VRHand leftHand;
        [SerializeField] private VRHand rightHand;

        [Header("Detection")]
        [SerializeField] private LayerMask pushSurfaceMask = ~0;
        [SerializeField] private float surfaceDetectRadius = 0.06f;
        [SerializeField] private float surfaceDetectDistance = 0.15f;

        [Header("Force")]
        [SerializeField] private float pushForce = 12f;
        [SerializeField] private float maxPushSpeed = 8f;
        [SerializeField] private float verticalMultiplier = 0.6f;

        [Header("Activation")]
        [SerializeField] private bool disableWhileHolding = true;

        public bool IsActive => _isActive;
        public LocomotionState ProvidedState => LocomotionState.GorillaMoving;
        public int Priority => 20;

        private Rigidbody _playerRb;
        private Vector3 _prevLeftPos;
        private Vector3 _prevRightPos;
        private bool _isActive;
        private bool _isLocomotionOwner;

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

            _isActive = false;

            if (leftHand != null)
                ProcessHand(leftHand, ref _prevLeftPos);
            if (rightHand != null)
                ProcessHand(rightHand, ref _prevRightPos);
        }

        private void ProcessHand(VRHand hand, ref Vector3 prevPos)
        {
            if (disableWhileHolding && hand.GrabHandler != null && hand.GrabHandler.IsHolding)
            {
                prevPos = hand.transform.position;
                return;
            }

            Vector3 handPos = hand.transform.position;
            Vector3 handDown = -hand.transform.up;

            bool nearSurface = Physics.SphereCast(handPos, surfaceDetectRadius, handDown,
                out _, surfaceDetectDistance, pushSurfaceMask, QueryTriggerInteraction.Ignore);

            if (!nearSurface)
            {
                bool nearAny = Physics.SphereCast(handPos, surfaceDetectRadius, Vector3.down,
                    out _, surfaceDetectDistance, pushSurfaceMask, QueryTriggerInteraction.Ignore);

                if (!nearAny)
                {
                    prevPos = handPos;
                    return;
                }
            }

            Vector3 handDelta = prevPos - handPos;
            prevPos = handPos;

            if (handDelta.sqrMagnitude < 0.00001f) return;

            handDelta.y *= verticalMultiplier;
            handDelta.y = Mathf.Max(handDelta.y, 0f);

            if (_playerRb.linearVelocity.magnitude < maxPushSpeed)
            {
                _playerRb.AddForce(handDelta * pushForce, ForceMode.VelocityChange);
                _isActive = true;
            }
        }

        public void SetLocomotionActive(bool active)
        {
            _isLocomotionOwner = active;
        }

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
