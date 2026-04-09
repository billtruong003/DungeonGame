using System;
using UnityEngine;
using VRCore.Hand;
using VRCore.Input;

namespace VRCore.Locomotion
{
    public class GrappleProvider : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private HandSide grappleHand = HandSide.Right;

        [Header("Grapple")]
        [SerializeField] private float maxDistance = 30f;
        [SerializeField] private LayerMask grappleLayers = ~0;
        [SerializeField] private float pullForce = 15f;
        [SerializeField] private float arriveDistance = 1.5f;
        [SerializeField] private float maxPullSpeed = 12f;

        [Header("Spring")]
        [SerializeField] private float springStrength = 50f;
        [SerializeField] private float springDamper = 5f;

        [Header("Visual")]
        [SerializeField] private LineRenderer ropeLine;

        public GrappleState CurrentState { get; private set; } = GrappleState.Idle;
        public Vector3 AnchorPoint { get; private set; }
        public float DistanceToAnchor => Vector3.Distance(transform.position, AnchorPoint);
        public event Action OnGrappleAttached;
        public event Action OnGrappleDetached;

        public enum GrappleState { Idle, Aiming, Attached, Pulling }

        private Rigidbody _playerRb;
        private SpringJoint _springJoint;
        private VRHand _hand;
        private bool _hasValidTarget;
        private RaycastHit _aimHit;

        private void Awake()
        {
            _playerRb = GetComponentInParent<Rigidbody>();
        }

        private void Start()
        {
            var hands = FindObjectsByType<VRHand>(FindObjectsSortMode.None);
            foreach (var h in hands)
            {
                if (h.Side == grappleHand) { _hand = h; break; }
            }
        }

        private void Update()
        {
            if (InputManager.Instance == null || _hand == null) return;

            IVRInput input = InputManager.Instance.Input;

            switch (CurrentState)
            {
                case GrappleState.Idle:
                    if (input.GrabPressed(grappleHand) && !_hand.GrabHandler.IsHolding)
                        StartAim();
                    break;

                case GrappleState.Aiming:
                    UpdateAim();
                    if (input.TriggerPressed(grappleHand) && _hasValidTarget)
                        Fire();
                    if (input.GrabReleased(grappleHand))
                        CancelGrapple();
                    break;

                case GrappleState.Attached:
                case GrappleState.Pulling:
                    UpdateAttached();
                    if (input.GrabReleased(grappleHand))
                        CancelGrapple();
                    break;
            }

            UpdateVisual();
        }

        private void FixedUpdate()
        {
            if (CurrentState != GrappleState.Pulling) return;

            Vector3 direction = (AnchorPoint - _playerRb.position).normalized;
            _playerRb.AddForce(direction * pullForce, ForceMode.Acceleration);

            if (_playerRb.linearVelocity.magnitude > maxPullSpeed)
                _playerRb.linearVelocity = _playerRb.linearVelocity.normalized * maxPullSpeed;

            if (DistanceToAnchor < arriveDistance)
                CancelGrapple();
        }

        public void StartAim()
        {
            CurrentState = GrappleState.Aiming;
        }

        public void Fire()
        {
            if (!_hasValidTarget) return;

            AnchorPoint = _aimHit.point;
            CurrentState = GrappleState.Attached;

            CreateSpringJoint();
            OnGrappleAttached?.Invoke();

            _hand?.Haptics.PlayHaptic(0.5f, 0.1f);
        }

        public void Fire(Vector3 targetPoint)
        {
            AnchorPoint = targetPoint;
            CurrentState = GrappleState.Attached;
            CreateSpringJoint();
            OnGrappleAttached?.Invoke();
        }

        public void StartPull()
        {
            if (CurrentState != GrappleState.Attached) return;
            CurrentState = GrappleState.Pulling;

            if (_springJoint != null)
                Destroy(_springJoint);
        }

        public void CancelGrapple()
        {
            if (_springJoint != null)
                Destroy(_springJoint);

            CurrentState = GrappleState.Idle;
            _hasValidTarget = false;
            OnGrappleDetached?.Invoke();
        }

        private void UpdateAim()
        {
            Transform origin = _hand != null && _hand.PalmTransform != null
                ? _hand.PalmTransform : transform;

            _hasValidTarget = Physics.Raycast(origin.position, origin.forward,
                out _aimHit, maxDistance, grappleLayers, QueryTriggerInteraction.Ignore);
        }

        private void UpdateAttached()
        {
            if (InputManager.Instance.Input.TriggerHeld(grappleHand)
                && CurrentState == GrappleState.Attached)
                StartPull();
        }

        private void CreateSpringJoint()
        {
            _springJoint = _playerRb.gameObject.AddComponent<SpringJoint>();
            _springJoint.autoConfigureConnectedAnchor = false;
            _springJoint.connectedAnchor = AnchorPoint;
            _springJoint.spring = springStrength;
            _springJoint.damper = springDamper;
            _springJoint.maxDistance = DistanceToAnchor * 0.8f;
            _springJoint.minDistance = 0f;
        }

        private void UpdateVisual()
        {
            if (ropeLine == null) return;

            bool showLine = CurrentState == GrappleState.Attached || CurrentState == GrappleState.Pulling;
            ropeLine.enabled = showLine;

            if (showLine)
            {
                Vector3 handPos = _hand != null ? _hand.transform.position : transform.position;
                ropeLine.positionCount = 2;
                ropeLine.SetPosition(0, handPos);
                ropeLine.SetPosition(1, AnchorPoint);
            }

            if (CurrentState == GrappleState.Aiming && _hasValidTarget)
            {
                ropeLine.enabled = true;
                Vector3 handPos = _hand != null ? _hand.transform.position : transform.position;
                ropeLine.positionCount = 2;
                ropeLine.SetPosition(0, handPos);
                ropeLine.SetPosition(1, _aimHit.point);
            }
        }

        public void SetMaxDistance(float dist) => maxDistance = dist;
        public void SetPullForce(float force) => pullForce = force;
        public void SetGrappleHand(HandSide hand) => grappleHand = hand;
    }
}
