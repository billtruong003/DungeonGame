using UnityEngine;
using BillVRCore.Input;

namespace BillVRCore.Locomotion
{
    public class ParkourProvider : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField] private LayerMask wallMask = ~0;
        [SerializeField] private LayerMask vaultMask = ~0;
        [SerializeField] private float wallDetectDistance = 0.5f;
        [SerializeField] private float vaultDetectDistance = 0.8f;

        [Header("Wall Run")]
        [SerializeField] private bool enableWallRun = true;
        [SerializeField] private float wallRunSpeed = 4f;
        [SerializeField] private float wallRunGravity = -3f;
        [SerializeField] private float wallRunMaxDuration = 1.5f;
        [SerializeField] private float wallRunMinSpeed = 2f;

        [Header("Wall Jump")]
        [SerializeField] private float wallJumpForce = 6f;
        [SerializeField] private float wallJumpOutward = 3f;

        [Header("Vault")]
        [SerializeField] private bool enableVault = true;
        [SerializeField] private float vaultMinHeight = 0.3f;
        [SerializeField] private float vaultMaxHeight = 1.5f;
        [SerializeField] private float vaultSpeed = 4f;

        [Header("Slide")]
        [SerializeField] private bool enableSlide = true;
        [SerializeField] private float slideSpeed = 5f;
        [SerializeField] private float slideDuration = 1f;
        [SerializeField] private float slideMinSpeed = 3f;

        public ParkourState CurrentState { get; private set; } = ParkourState.None;
        public bool IsActive => CurrentState != ParkourState.None;

        public enum ParkourState { None, WallRunning, Vaulting, Sliding }

        private Rigidbody _playerRb;
        private VRPlayerBody _playerBody;
        private float _stateTimer;
        private Vector3 _wallNormal;
        private Vector3 _wallRunDirection;
        private Vector3 _vaultTarget;
        private Vector3 _vaultStart;
        private float _vaultProgress;

        private void Awake()
        {
            _playerRb = GetComponentInParent<Rigidbody>();
            _playerBody = GetComponentInParent<VRPlayerBody>();
        }

        private void FixedUpdate()
        {
            switch (CurrentState)
            {
                case ParkourState.None:
                    DetectParkourOpportunity();
                    break;
                case ParkourState.WallRunning:
                    UpdateWallRun();
                    break;
                case ParkourState.Vaulting:
                    UpdateVault();
                    break;
                case ParkourState.Sliding:
                    UpdateSlide();
                    break;
            }
        }

        private void DetectParkourOpportunity()
        {
            if (_playerBody != null && _playerBody.IsGrounded)
            {
                if (enableSlide && DetectSlide()) return;
                if (enableVault && DetectVault()) return;
            }

            if (!_playerBody.IsGrounded && enableWallRun)
                DetectWallRun();
        }

        private bool DetectWallRun()
        {
            float horizontalSpeed = new Vector3(_playerRb.linearVelocity.x, 0, _playerRb.linearVelocity.z).magnitude;
            if (horizontalSpeed < wallRunMinSpeed) return false;

            Vector3 moveDir = _playerRb.linearVelocity.normalized;
            moveDir.y = 0;
            moveDir.Normalize();

            Vector3 rightCheck = Quaternion.Euler(0, 70, 0) * moveDir;
            Vector3 leftCheck = Quaternion.Euler(0, -70, 0) * moveDir;

            if (Physics.Raycast(transform.position, rightCheck, out RaycastHit hitR, wallDetectDistance, wallMask))
            {
                StartWallRun(hitR.normal, Vector3.Cross(hitR.normal, Vector3.up).normalized);
                return true;
            }

            if (Physics.Raycast(transform.position, leftCheck, out RaycastHit hitL, wallDetectDistance, wallMask))
            {
                StartWallRun(hitL.normal, Vector3.Cross(Vector3.up, hitL.normal).normalized);
                return true;
            }

            return false;
        }

        private bool DetectVault()
        {
            Transform head = Camera.main != null ? Camera.main.transform : transform;
            Vector3 forward = head.forward.Flatten().normalized;

            Vector3 rayOrigin = transform.position + Vector3.up * vaultMaxHeight;
            if (!Physics.Raycast(rayOrigin, forward, out RaycastHit fwdHit, vaultDetectDistance, vaultMask))
                return false;

            if (!Physics.Raycast(fwdHit.point + forward * 0.1f + Vector3.up * 0.5f, Vector3.down,
                out RaycastHit topHit, vaultMaxHeight, vaultMask))
                return false;

            float obstacleHeight = topHit.point.y - transform.position.y;
            if (obstacleHeight < vaultMinHeight || obstacleHeight > vaultMaxHeight) return false;

            Vector3 landPos = topHit.point + forward * 0.5f;
            if (!Physics.Raycast(landPos + Vector3.up, Vector3.down, out RaycastHit landHit, 3f, vaultMask))
                return false;

            StartVault(landHit.point + Vector3.up * 0.1f);
            return true;
        }

        private bool DetectSlide()
        {
            float speed = _playerRb.linearVelocity.magnitude;
            if (speed < slideMinSpeed) return false;

            if (InputManager.Instance == null) return false;
            bool crouching = InputManager.Instance.Input.SecondaryButtonDown(HandSide.Left);
            if (!crouching) return false;

            StartSlide();
            return true;
        }

        private void StartWallRun(Vector3 normal, Vector3 direction)
        {
            CurrentState = ParkourState.WallRunning;
            _wallNormal = normal;
            _wallRunDirection = direction;
            _stateTimer = wallRunMaxDuration;
            _playerRb.useGravity = false;
        }

        private void UpdateWallRun()
        {
            _stateTimer -= Time.fixedDeltaTime;
            if (_stateTimer <= 0f || !IsNearWall())
            {
                EndParkour();
                return;
            }

            Vector3 velocity = _wallRunDirection * wallRunSpeed;
            velocity.y = wallRunGravity * (wallRunMaxDuration - _stateTimer);
            _playerRb.linearVelocity = velocity;

            if (InputManager.Instance != null && InputManager.Instance.Input.PrimaryButtonDown(HandSide.Right))
            {
                _playerRb.linearVelocity = _wallNormal * wallJumpOutward + Vector3.up * wallJumpForce
                    + _wallRunDirection * wallRunSpeed * 0.5f;
                EndParkour();
            }
        }

        private void StartVault(Vector3 target)
        {
            CurrentState = ParkourState.Vaulting;
            _vaultStart = transform.position;
            _vaultTarget = target;
            _vaultProgress = 0f;
            _playerRb.useGravity = false;
            _playerRb.linearVelocity = Vector3.zero;
        }

        private void UpdateVault()
        {
            _vaultProgress += Time.fixedDeltaTime * vaultSpeed;

            float t = _vaultProgress * _vaultProgress * (3f - 2f * _vaultProgress);
            float arcHeight = Mathf.Sin(_vaultProgress * Mathf.PI) * 0.5f;

            Vector3 pos = Vector3.Lerp(_vaultStart, _vaultTarget, t);
            pos.y += arcHeight;

            _playerRb.MovePosition(pos);

            if (_vaultProgress >= 1f) EndParkour();
        }

        private void StartSlide()
        {
            CurrentState = ParkourState.Sliding;
            _stateTimer = slideDuration;
            Vector3 slideDir = _playerRb.linearVelocity.normalized;
            _playerRb.linearVelocity = slideDir * slideSpeed;
        }

        private void UpdateSlide()
        {
            _stateTimer -= Time.fixedDeltaTime;
            if (_stateTimer <= 0f)
            {
                EndParkour();
                return;
            }

            float decel = slideSpeed / slideDuration * Time.fixedDeltaTime;
            Vector3 vel = _playerRb.linearVelocity;
            float newSpeed = Mathf.Max(vel.magnitude - decel, 0f);
            _playerRb.linearVelocity = vel.normalized * newSpeed;
        }

        private void EndParkour()
        {
            CurrentState = ParkourState.None;
            _playerRb.useGravity = true;
        }

        private bool IsNearWall()
        {
            return Physics.Raycast(transform.position, -_wallNormal, wallDetectDistance * 1.2f, wallMask);
        }

        public void SetWallRunEnabled(bool enabled) => enableWallRun = enabled;
        public void SetVaultEnabled(bool enabled) => enableVault = enabled;
        public void SetSlideEnabled(bool enabled) => enableSlide = enabled;
    }
}
