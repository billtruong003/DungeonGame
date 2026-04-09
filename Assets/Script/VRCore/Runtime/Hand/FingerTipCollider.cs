using System;
using UnityEngine;

namespace VRCore.Hand
{
    [RequireComponent(typeof(VRHand))]
    public class FingerTipCollider : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField] private float tipRadius = 0.008f;
        [SerializeField] private LayerMask interactLayers = ~0;
        [SerializeField] private float hapticStrength = 0.15f;

        public event Action<FingerType, Collider> OnFingerTouchStart;
        public event Action<FingerType, Collider> OnFingerTouchEnd;
        public event Action<FingerType, Collider, float> OnFingerPoke;

        private VRHand _hand;
        private FingerRig _fingerRig;

        private struct TipState
        {
            public Collider touching;
            public Vector3 prevPosition;
            public float pokeDepth;
        }

        private readonly TipState[] _tipStates = new TipState[5];
        private readonly Collider[] _overlapBuffer = new Collider[4];

        private void Awake()
        {
            _hand = GetComponent<VRHand>();
        }

        private void Start()
        {
            var animator = GetComponent<HandAnimator>();
            _fingerRig = animator != null ? animator.GetFingerRig() : GetComponentInChildren<FingerRig>();
        }

        private void FixedUpdate()
        {
            if (_fingerRig == null) return;

            for (int i = 0; i < 5; i++)
                ProcessFinger((FingerType)i, ref _tipStates[i]);
        }

        private void ProcessFinger(FingerType type, ref TipState state)
        {
            Vector3 tipPos = _fingerRig.GetTipPosition(type);
            int count = Physics.OverlapSphereNonAlloc(tipPos, tipRadius, _overlapBuffer, interactLayers);

            Collider nearest = null;
            float nearestDist = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (_overlapBuffer[i].transform.IsChildOf(transform)) continue;

                float d = Vector3.Distance(tipPos, _overlapBuffer[i].ClosestPoint(tipPos));
                if (d < nearestDist)
                {
                    nearestDist = d;
                    nearest = _overlapBuffer[i];
                }
            }

            if (nearest != null && state.touching == null)
            {
                state.touching = nearest;
                state.prevPosition = tipPos;
                state.pokeDepth = 0f;
                OnFingerTouchStart?.Invoke(type, nearest);
                _hand.Haptics.PlayHaptic(hapticStrength, 0.02f);
            }
            else if (nearest == null && state.touching != null)
            {
                OnFingerTouchEnd?.Invoke(type, state.touching);
                state.touching = null;
                state.pokeDepth = 0f;
            }

            if (state.touching != null)
            {
                Vector3 delta = state.prevPosition - tipPos;
                Vector3 normal = (tipPos - state.touching.ClosestPoint(tipPos)).normalized;
                float depthDelta = Vector3.Dot(delta, -normal);
                state.pokeDepth += depthDelta;
                state.pokeDepth = Mathf.Max(state.pokeDepth, 0f);
                state.prevPosition = tipPos;

                if (depthDelta > 0.001f)
                    OnFingerPoke?.Invoke(type, state.touching, state.pokeDepth);
            }
        }

        public bool IsFingerTouching(FingerType finger) => _tipStates[(int)finger].touching != null;
        public Collider GetTouchingCollider(FingerType finger) => _tipStates[(int)finger].touching;
        public float GetPokeDepth(FingerType finger) => _tipStates[(int)finger].pokeDepth;
        public void SetTipRadius(float radius) => tipRadius = radius;
        public void SetHapticStrength(float strength) => hapticStrength = strength;
    }
}
