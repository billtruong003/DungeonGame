using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace VRCore.Interaction
{
    [RequireComponent(typeof(Rigidbody))]
    public class Grabbable : MonoBehaviour
    {
        [Header("Grab Settings")]
        [SerializeField] private GrabType grabType = GrabType.Default;
        [SerializeField] private HandRestriction handRestriction = HandRestriction.Both;
        [SerializeField] private bool singleHandOnly;
        [SerializeField] private bool parentOnGrab = true;
        [SerializeField] private bool useGentleGrab;
        [SerializeField] private bool maintainGrabOffset;
        [SerializeField] private float jointBreakForce = Mathf.Infinity;
        [SerializeField] private float ignoreColliderTime = 0.25f;

        [Header("Events")]
        [SerializeField] private UnityEvent<Hand.VRHand> onGrabbed;
        [SerializeField] private UnityEvent<Hand.VRHand> onReleased;
        [SerializeField] private UnityEvent<Hand.VRHand> onSqueezed;

        public GrabType GrabType => grabType;
        public bool SingleHandOnly => singleHandOnly;
        public bool ParentOnGrab => parentOnGrab;
        public bool MaintainGrabOffset => maintainGrabOffset;
        public float JointBreakForce => jointBreakForce;
        public Rigidbody Rb { get; private set; }

        public bool IsHeld => _holdingHands.Count > 0;
        public int HeldHandCount => _holdingHands.Count;
        public IReadOnlyList<Hand.VRHand> HoldingHands => _holdingHands;

        public event Action<Hand.VRHand, Grabbable> OnGrabEvent;
        public event Action<Hand.VRHand, Grabbable> OnReleaseEvent;
        public event Action<Hand.VRHand, Grabbable> OnSqueezeEvent;
        public event Action<Hand.VRHand, Grabbable> OnHighlightEvent;
        public event Action<Hand.VRHand, Grabbable> OnUnhighlightEvent;

        private readonly List<Hand.VRHand> _holdingHands = new(4);
        private float _savedDrag;
        private float _savedAngularDrag;
        private Transform _originalParent;

        private readonly struct IgnoredPair
        {
            public readonly Collider handCol;
            public readonly Collider myCol;
            public readonly float expireTime;
            public IgnoredPair(Collider h, Collider m, float t) { handCol = h; myCol = m; expireTime = t; }
        }

        private readonly List<IgnoredPair> _ignoredPairs = new(16);

        protected virtual void Awake()
        {
            Rb = GetComponent<Rigidbody>();
            _originalParent = transform.parent;
        }

        protected virtual void Update()
        {
            CleanupIgnoredColliders();
        }

        public virtual bool CanBeGrabbedBy(Hand.VRHand hand)
        {
            if (singleHandOnly && _holdingHands.Count > 0) return false;

            return handRestriction switch
            {
                HandRestriction.LeftOnly => hand.Side == HandSide.Left,
                HandRestriction.RightOnly => hand.Side == HandSide.Right,
                _ => true
            };
        }

        public virtual void OnGrab(Hand.VRHand hand)
        {
            if (_holdingHands.Count == 0)
            {
                _savedDrag = Rb.linearDamping;
                _savedAngularDrag = Rb.angularDamping;
            }

            _holdingHands.Add(hand);
            Rb.isKinematic = false;

            onGrabbed?.Invoke(hand);
            OnGrabEvent?.Invoke(hand, this);
        }

        public virtual void OnRelease(Hand.VRHand hand)
        {
            _holdingHands.Remove(hand);

            if (_holdingHands.Count == 0)
            {
                Rb.linearDamping = _savedDrag;
                Rb.angularDamping = _savedAngularDrag;

                if (parentOnGrab)
                    transform.SetParent(_originalParent);

                IgnoreHandColliders(hand, ignoreColliderTime);
            }

            onReleased?.Invoke(hand);
            OnReleaseEvent?.Invoke(hand, this);
        }

        public virtual void OnSqueeze(Hand.VRHand hand)
        {
            onSqueezed?.Invoke(hand);
            OnSqueezeEvent?.Invoke(hand, this);
        }

        public void NotifyHighlight(Hand.VRHand hand) => OnHighlightEvent?.Invoke(hand, this);
        public void NotifyUnhighlight(Hand.VRHand hand) => OnUnhighlightEvent?.Invoke(hand, this);

        public void ApplyThrowVelocity(Vector3 velocity, Vector3 angularVelocity)
        {
            Rb.linearVelocity = velocity;
            Rb.angularVelocity = angularVelocity;
        }

        public void ForceRelease()
        {
            for (int i = _holdingHands.Count - 1; i >= 0; i--)
                _holdingHands[i].GrabHandler.ForceRelease();
        }

        public void SetParentOnGrab(bool value) => parentOnGrab = value;
        public void SetSingleHandOnly(bool value) => singleHandOnly = value;
        public void SetHandRestriction(HandRestriction restriction) => handRestriction = restriction;
        public void SetJointBreakForce(float force) => jointBreakForce = force;

        private void IgnoreHandColliders(Hand.VRHand hand, float duration)
        {
            float expireTime = Time.time + duration;
            var handColliders = hand.GetComponentsInChildren<Collider>();
            var myColliders = GetComponentsInChildren<Collider>();

            foreach (var hc in handColliders)
            {
                foreach (var mc in myColliders)
                {
                    Physics.IgnoreCollision(hc, mc, true);
                    _ignoredPairs.Add(new IgnoredPair(hc, mc, expireTime));
                }
            }
        }

        private void CleanupIgnoredColliders()
        {
            float now = Time.time;
            for (int i = _ignoredPairs.Count - 1; i >= 0; i--)
            {
                if (now < _ignoredPairs[i].expireTime) continue;

                var pair = _ignoredPairs[i];
                if (pair.handCol != null && pair.myCol != null)
                    Physics.IgnoreCollision(pair.handCol, pair.myCol, false);

                _ignoredPairs.RemoveAt(i);
            }
        }
    }
}
