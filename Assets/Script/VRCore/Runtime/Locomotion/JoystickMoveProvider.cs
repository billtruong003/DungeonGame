using UnityEngine;
using VRCore.Input;

namespace VRCore.Locomotion
{
    public class JoystickMoveProvider : MonoBehaviour, ILocomotionProvider
    {
        [Header("Input")]
        [SerializeField] private HandSide moveHand = HandSide.Left;
        [SerializeField] private float deadzone = 0.15f;

        [Header("Movement")]
        [SerializeField] private float maxSpeed = 3f;
        [SerializeField] private float acceleration = 25f;
        [SerializeField] private float deceleration = 30f;

        [Header("Direction Reference")]
        [SerializeField] private DirectionSource directionSource = DirectionSource.Head;
        [SerializeField] private Transform customDirectionTransform;

        [Header("Gravity")]
        [SerializeField] private float gravityMultiplier = 2f;
        [SerializeField] private bool applyGravity = true;

        public bool IsActive => _inputMagnitude > deadzone;
        public LocomotionState ProvidedState => LocomotionState.JoystickMoving;
        public int Priority => 10;
        public Vector3 CurrentVelocity => _velocity;
        public float InputMagnitude => _inputMagnitude;

        private Rigidbody _playerRb;
        private Transform _headTransform;
        private Vector3 _velocity;
        private float _inputMagnitude;
        private bool _isLocomotionActive;

        public enum DirectionSource { Head, LeftHand, RightHand, Custom }

        private void Awake()
        {
            _playerRb = GetComponentInParent<Rigidbody>();
        }

        private void Start()
        {
            var cam = Camera.main;
            if (cam != null)
                _headTransform = cam.transform;
        }

        private void FixedUpdate()
        {
            if (InputManager.Instance == null || _playerRb == null) return;

            Vector2 axis = InputManager.Instance.Input.JoystickAxis(moveHand);
            _inputMagnitude = axis.magnitude;

            if (_inputMagnitude < deadzone)
            {
                Decelerate();
                return;
            }

            Vector3 moveDir = CalculateMoveDirection(axis);
            Accelerate(moveDir, _inputMagnitude);
        }

        private Vector3 CalculateMoveDirection(Vector2 axis)
        {
            Transform reference = GetDirectionTransform();
            if (reference == null) return Vector3.zero;

            Vector3 forward = Vector3.ProjectOnPlane(reference.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(reference.right, Vector3.up).normalized;

            return (forward * axis.y + right * axis.x).normalized;
        }

        private Transform GetDirectionTransform()
        {
            return directionSource switch
            {
                DirectionSource.Head => _headTransform,
                DirectionSource.Custom => customDirectionTransform,
                _ => _headTransform
            };
        }

        private void Accelerate(Vector3 direction, float inputStrength)
        {
            float targetSpeed = maxSpeed * Mathf.Clamp01(inputStrength);
            Vector3 targetVelocity = direction * targetSpeed;

            Vector3 currentHorizontal = new Vector3(_playerRb.linearVelocity.x, 0f, _playerRb.linearVelocity.z);
            Vector3 newHorizontal = Vector3.MoveTowards(currentHorizontal, targetVelocity,
                acceleration * Time.fixedDeltaTime);

            float verticalVelocity = _playerRb.linearVelocity.y;
            if (applyGravity)
                verticalVelocity += Physics.gravity.y * gravityMultiplier * Time.fixedDeltaTime;

            _playerRb.linearVelocity = new Vector3(newHorizontal.x, verticalVelocity, newHorizontal.z);
            _velocity = _playerRb.linearVelocity;
        }

        private void Decelerate()
        {
            Vector3 currentHorizontal = new Vector3(_playerRb.linearVelocity.x, 0f, _playerRb.linearVelocity.z);
            Vector3 newHorizontal = Vector3.MoveTowards(currentHorizontal, Vector3.zero,
                deceleration * Time.fixedDeltaTime);

            float verticalVelocity = _playerRb.linearVelocity.y;
            if (applyGravity)
                verticalVelocity += Physics.gravity.y * gravityMultiplier * Time.fixedDeltaTime;

            _playerRb.linearVelocity = new Vector3(newHorizontal.x, verticalVelocity, newHorizontal.z);
            _velocity = _playerRb.linearVelocity;
        }

        public void SetLocomotionActive(bool active)
        {
            _isLocomotionActive = active;
        }
    }
}
