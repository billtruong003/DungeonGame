using System;
using UnityEngine;
using VRCore.Input;

namespace VRCore.Locomotion
{
    public class TeleportProvider : MonoBehaviour, ILocomotionProvider
    {
        [Header("Input")]
        [SerializeField] private HandSide teleportHand = HandSide.Right;
        [SerializeField] private TeleportActivation activation = TeleportActivation.JoystickForward;
        [SerializeField] private float joystickThreshold = 0.6f;

        [Header("Arc")]
        [SerializeField] private float arcVelocity = 8f;
        [SerializeField] private float arcGravity = -15f;
        [SerializeField] private int arcSegments = 60;
        [SerializeField] private float maxDistance = 15f;

        [Header("Validation")]
        [SerializeField] private LayerMask validLayers = ~0;
        [SerializeField] private float maxSlopeAngle = 45f;

        [Header("Visual")]
        [SerializeField] private LineRenderer arcLine;
        [SerializeField] private GameObject landingIndicator;
        [SerializeField] private Color validColor = new(0.2f, 0.8f, 0.4f);
        [SerializeField] private Color invalidColor = new(0.8f, 0.2f, 0.2f);

        public bool IsActive => _isTeleporting;
        public LocomotionState ProvidedState => LocomotionState.Teleporting;
        public int Priority => 30;
        public bool IsAiming => _isAiming;
        public bool HasValidTarget => _hasValidTarget;
        public Vector3 TargetPosition => _targetPosition;

        public event Action<Vector3> OnTeleported;
        public event Action OnAimStarted;
        public event Action OnAimCancelled;

        private VRPlayerBody _playerBody;
        private bool _isAiming;
        private bool _isTeleporting;
        private bool _hasValidTarget;
        private Vector3 _targetPosition;
        private Vector3[] _arcPoints;

        public enum TeleportActivation { JoystickForward, TriggerHold, PrimaryButton }

        private void Awake()
        {
            _playerBody = GetComponentInParent<VRPlayerBody>();
            _arcPoints = new Vector3[arcSegments];

            if (landingIndicator != null)
                landingIndicator.SetActive(false);
        }

        private void Update()
        {
            if (InputManager.Instance == null) return;

            IVRInput input = InputManager.Instance.Input;
            bool activating = EvaluateActivation(input, true);
            bool releasing = EvaluateActivation(input, false);

            if (activating && !_isAiming)
                StartAim();

            if (_isAiming)
            {
                UpdateArc();
                if (releasing) ExecuteTeleport();
            }
        }

        private bool EvaluateActivation(IVRInput input, bool forPress)
        {
            return activation switch
            {
                TeleportActivation.JoystickForward => forPress
                    ? input.JoystickAxis(teleportHand).y > joystickThreshold
                    : input.JoystickAxis(teleportHand).y < joystickThreshold * 0.5f,
                TeleportActivation.TriggerHold => forPress
                    ? input.TriggerPressed(teleportHand)
                    : input.TriggerReleased(teleportHand),
                TeleportActivation.PrimaryButton => forPress
                    ? input.PrimaryButtonDown(teleportHand)
                    : !input.TriggerHeld(teleportHand),
                _ => false
            };
        }

        public void StartAim()
        {
            _isAiming = true;
            if (arcLine != null) arcLine.enabled = true;
            OnAimStarted?.Invoke();
        }

        public void CancelAim()
        {
            _isAiming = false;
            _hasValidTarget = false;
            if (arcLine != null) arcLine.enabled = false;
            if (landingIndicator != null) landingIndicator.SetActive(false);
            OnAimCancelled?.Invoke();
        }

        public void TeleportTo(Vector3 position)
        {
            if (_playerBody != null)
                _playerBody.Teleport(position);
            else
                transform.root.position = position;

            OnTeleported?.Invoke(position);
        }

        public void TeleportTo(Vector3 position, float yRotation)
        {
            if (_playerBody != null)
                _playerBody.Teleport(position, Quaternion.Euler(0f, yRotation, 0f));
            else
            {
                transform.root.position = position;
                transform.root.rotation = Quaternion.Euler(0f, yRotation, 0f);
            }
            OnTeleported?.Invoke(position);
        }

        public void TeleportTo(Transform target)
        {
            if (target == null) return;
            TeleportTo(target.position, target.eulerAngles.y);
        }

        public bool TryTeleportToAimTarget()
        {
            if (!_hasValidTarget) return false;
            TeleportTo(_targetPosition);
            CancelAim();
            return true;
        }

        private void UpdateArc()
        {
            Transform hand = transform;
            Vector3 startPos = hand.position;
            Vector3 startVel = hand.forward * arcVelocity;

            _hasValidTarget = false;
            int hitIndex = arcSegments;

            for (int i = 0; i < arcSegments; i++)
            {
                float t = (float)i / arcSegments * (maxDistance / arcVelocity);
                _arcPoints[i] = startPos + startVel * t + Vector3.up * (0.5f * arcGravity * t * t);

                if (i > 0)
                {
                    Vector3 dir = _arcPoints[i] - _arcPoints[i - 1];
                    if (Physics.Raycast(_arcPoints[i - 1], dir.normalized, out RaycastHit hit,
                        dir.magnitude, validLayers, QueryTriggerInteraction.Ignore))
                    {
                        _arcPoints[i] = hit.point;
                        hitIndex = i + 1;
                        _hasValidTarget = Vector3.Angle(Vector3.up, hit.normal) <= maxSlopeAngle;
                        _targetPosition = hit.point;
                        break;
                    }
                }
            }

            UpdateVisuals(hitIndex);
        }

        private void UpdateVisuals(int pointCount)
        {
            if (arcLine != null)
            {
                arcLine.positionCount = pointCount;
                for (int i = 0; i < pointCount; i++)
                    arcLine.SetPosition(i, _arcPoints[i]);

                Color c = _hasValidTarget ? validColor : invalidColor;
                arcLine.startColor = c;
                arcLine.endColor = c;
            }

            if (landingIndicator != null)
            {
                landingIndicator.SetActive(_hasValidTarget);
                if (_hasValidTarget)
                    landingIndicator.transform.position = _targetPosition;
            }
        }

        private void ExecuteTeleport()
        {
            _isAiming = false;
            if (arcLine != null) arcLine.enabled = false;
            if (landingIndicator != null) landingIndicator.SetActive(false);

            if (!_hasValidTarget) return;

            _isTeleporting = true;
            TeleportTo(_targetPosition);
            _isTeleporting = false;
        }

        public void SetLocomotionActive(bool active) { }
        public void SetActivationMode(TeleportActivation mode) => activation = mode;
        public void SetMaxDistance(float distance) => maxDistance = distance;
        public void SetValidLayers(LayerMask layers) => validLayers = layers;
        public void SetTeleportHand(HandSide hand) => teleportHand = hand;
    }
}
