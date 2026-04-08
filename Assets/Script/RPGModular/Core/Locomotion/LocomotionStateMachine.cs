using System;
using UnityEngine;

namespace RPGModular
{
    [RequireComponent(typeof(CharacterController))]
    public class LocomotionStateMachine : MonoBehaviour
    {
        [field: SerializeField] public AnimationController AnimController { get; private set; }
        [field: SerializeField] public CharacterStats Stats { get; private set; }
        [field: SerializeField] public HealthSystem Health { get; private set; }
        [field: SerializeField] public PlayerInputHandler Input { get; private set; }
        [field: SerializeField] public CharacterController Controller { get; private set; }
        [field: SerializeField] public Transform CameraTransform { get; set; }

        [Header("Ground Check")]
        [SerializeField] private float groundCheckRadius = 0.3f;
        [SerializeField] private float groundCheckOffset = 0.1f;
        [SerializeField] private LayerMask groundLayer = ~0;

        [Header("Movement")]
        public float walkSpeed = 4f;
        public float runSpeed = 6f;
        public float sprintSpeed = 8f;
        public float sprintStaminaCost = 10f;
        public float rotationSpeed = 12f;
        public float accelerationTime = 0.15f;

        [Header("Jump")]
        public float jumpForce = 8f;
        public float doubleJumpForce = 6.5f;
        public float gravity = -20f;
        public float fallMultiplier = 1.3f;
        public float coyoteTime = 0.12f;
        public bool hasDoubleJump = false;

        [Header("Dash")]
        public float dashSpeed = 16f;
        public float dashDuration = 0.25f;
        public float dashCooldown = 0.8f;
        public float dashStaminaCost = 20f;

        [Header("Landing")]
        public float hardLandThreshold = -12f;
        public float hardLandDuration = 0.4f;
        public float softLandDuration = 0.15f;

        public LocomotionState CurrentState { get; private set; }
        public LocomotionStateType CurrentStateType { get; private set; }
        public Vector3 Velocity { get; set; }
        public bool IsGrounded { get; private set; }
        public float TimeSinceGrounded { get; private set; }
        public bool HasUsedDoubleJump { get; set; }
        public float LastDashTime { get; set; }
        public float CurrentMoveSpeed { get; private set; }

        public event Action<LocomotionStateType, LocomotionStateType> OnStateChanged;

        private void Awake()
        {
            AutoFindDependencies();
        }

        private void Start()
        {
            SwitchState(new ExplorationIdleState(this), LocomotionStateType.Idle);
        }

        private void Update()
        {
            UpdateGroundCheck();
            CurrentState?.Tick(Time.deltaTime);
            ApplyGravity();
            Controller.Move(Velocity * Time.deltaTime);
            AnimController?.SetGrounded(IsGrounded);
        }

        public void SwitchState(LocomotionState newState, LocomotionStateType type)
        {
            var oldType = CurrentStateType;
            CurrentState?.Exit();
            CurrentState = newState;
            CurrentStateType = type;
            CurrentState?.Enter();
            OnStateChanged?.Invoke(oldType, type);
        }

        public void ApplyMovement(Vector2 moveInput, float speed, float deltaTime)
        {
            if (CameraTransform == null) return;

            Vector3 camForward = CameraTransform.forward;
            Vector3 camRight = CameraTransform.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            Vector3 moveDirection = camForward * moveInput.y + camRight * moveInput.x;

            if (moveDirection.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * deltaTime);
            }

            CurrentMoveSpeed = Mathf.MoveTowards(CurrentMoveSpeed, speed * moveInput.magnitude, speed / accelerationTime * deltaTime);

            Vector3 horizontalVelocity = moveDirection.normalized * CurrentMoveSpeed;
            Velocity = new Vector3(horizontalVelocity.x, Velocity.y, horizontalVelocity.z);

            AnimController?.UpdateLocomotion(CurrentMoveSpeed / sprintSpeed, 0f, 0f);
        }

        public void StopHorizontalMovement()
        {
            CurrentMoveSpeed = Mathf.MoveTowards(CurrentMoveSpeed, 0f, sprintSpeed / accelerationTime * Time.deltaTime);
            Velocity = new Vector3(0f, Velocity.y, 0f);
            AnimController?.UpdateLocomotion(CurrentMoveSpeed / sprintSpeed, 0f, 0f);
        }

        private void ApplyGravity()
        {
            if (IsGrounded && Velocity.y < 0f)
            {
                Velocity = new Vector3(Velocity.x, -2f, Velocity.z);
                return;
            }

            float gravityMultiplier = Velocity.y < 0f ? fallMultiplier : 1f;
            Velocity = new Vector3(Velocity.x, Velocity.y + gravity * gravityMultiplier * Time.deltaTime, Velocity.z);
        }

        private void UpdateGroundCheck()
        {
            Vector3 origin = transform.position + Vector3.up * groundCheckOffset;
            IsGrounded = Physics.CheckSphere(origin, groundCheckRadius, groundLayer, QueryTriggerInteraction.Ignore);

            if (IsGrounded)
                TimeSinceGrounded = 0f;
            else
                TimeSinceGrounded += Time.deltaTime;
        }

        public bool CanDash()
        {
            return Time.time - LastDashTime > dashCooldown
                && Health != null
                && Health.HasStamina(dashStaminaCost);
        }

        private void AutoFindDependencies()
        {
            if (AnimController == null) AnimController = GetComponentInChildren<AnimationController>();
            if (Stats == null) Stats = GetComponent<CharacterStats>();
            if (Health == null) Health = GetComponent<HealthSystem>();
            if (Input == null) Input = GetComponent<PlayerInputHandler>();
            if (Controller == null) Controller = GetComponent<CharacterController>();
            if (CameraTransform == null && Camera.main != null) CameraTransform = Camera.main.transform;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 origin = transform.position + Vector3.up * groundCheckOffset;
            Gizmos.color = IsGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(origin, groundCheckRadius);
        }
#endif
    }
}
