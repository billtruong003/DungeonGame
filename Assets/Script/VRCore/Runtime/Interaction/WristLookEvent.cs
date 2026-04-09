using System;
using UnityEngine;
using UnityEngine.Events;

namespace VRCore.Interaction
{
    public class WristLookEvent : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField] private HandSide wristHand = HandSide.Left;
        [SerializeField] private float lookAngleThreshold = 40f;
        [SerializeField] private float palmFacingThreshold = 0.5f;
        [SerializeField] private float minActivationTime = 0.3f;

        [Header("Events")]
        [SerializeField] private UnityEvent onLookAtWrist;
        [SerializeField] private UnityEvent onLookAwayFromWrist;

        public bool IsLookingAtWrist { get; private set; }
        public float LookDuration { get; private set; }
        public event Action OnLookStarted;
        public event Action OnLookEnded;

        private Transform _headTransform;
        private Transform _wristTransform;
        private float _lookStartTime;
        private bool _wasLooking;
        private bool _activated;

        private void Start()
        {
            var cam = Camera.main;
            if (cam != null) _headTransform = cam.transform;

            var hands = FindObjectsByType<Hand.VRHand>(FindObjectsSortMode.None);
            foreach (var hand in hands)
            {
                if (hand.Side == wristHand)
                {
                    _wristTransform = hand.transform;
                    break;
                }
            }
        }

        private void Update()
        {
            if (_headTransform == null || _wristTransform == null) return;

            _wasLooking = IsLookingAtWrist;
            IsLookingAtWrist = EvaluateLookCondition();

            if (IsLookingAtWrist && !_wasLooking)
                _lookStartTime = Time.time;

            if (IsLookingAtWrist)
                LookDuration = Time.time - _lookStartTime;
            else
                LookDuration = 0f;

            bool shouldActivate = IsLookingAtWrist && LookDuration >= minActivationTime;

            if (shouldActivate && !_activated)
            {
                _activated = true;
                onLookAtWrist?.Invoke();
                OnLookStarted?.Invoke();
            }
            else if (!shouldActivate && _activated)
            {
                _activated = false;
                onLookAwayFromWrist?.Invoke();
                OnLookEnded?.Invoke();
            }
        }

        private bool EvaluateLookCondition()
        {
            Vector3 headToWrist = (_wristTransform.position - _headTransform.position).normalized;
            float lookAngle = Vector3.Angle(_headTransform.forward, headToWrist);
            if (lookAngle > lookAngleThreshold) return false;

            Vector3 palmUp = _wristTransform.up;
            float palmDot = Vector3.Dot(palmUp, (_headTransform.position - _wristTransform.position).normalized);
            if (palmDot < palmFacingThreshold) return false;

            return true;
        }

        public void SetWristHand(HandSide side) => wristHand = side;
        public void SetLookAngle(float angle) => lookAngleThreshold = angle;
        public void SetMinActivationTime(float time) => minActivationTime = time;
    }
}
