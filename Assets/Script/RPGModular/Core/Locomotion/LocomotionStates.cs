using UnityEngine;

namespace RPGModular
{
    public class ExplorationIdleState : LocomotionState
    {
        public ExplorationIdleState(LocomotionStateMachine sm) : base(sm) { }

        public override void Enter()
        {
            AnimController?.PlayAnimation("Locomotion", AnimationPriority.Locomotion);
            AnimController?.UpdateLocomotion(0f);
            stateMachine.StopHorizontalMovement();
        }

        public override void Tick(float deltaTime)
        {
            // Cập nhật blend tree — giữ MoveSpeed = 0
            AnimController?.UpdateLocomotion(0f);

            if (Input.JumpInput && stateMachine.IsGrounded)
            {
                Input.ConsumeJumpInput();
                stateMachine.SwitchState(new ExplorationJumpState(stateMachine), LocomotionStateType.Jump);
                return;
            }

            if (Input.DashInput && stateMachine.CanDash())
            {
                Input.ConsumeDashInput();
                stateMachine.SwitchState(new ExplorationDashState(stateMachine), LocomotionStateType.Dash);
                return;
            }

            if (Input.MoveInput.magnitude > 0.1f)
            {
                if (Input.SprintHeld)
                {
                    stateMachine.SwitchState(new ExplorationSprintState(stateMachine), LocomotionStateType.Sprint);
                    return;
                }
                stateMachine.SwitchState(new ExplorationMoveState(stateMachine), LocomotionStateType.Move);
                return;
            }

            if (!stateMachine.IsGrounded && stateMachine.TimeSinceGrounded > stateMachine.coyoteTime)
            {
                stateMachine.SwitchState(new ExplorationFallState(stateMachine), LocomotionStateType.Fall);
                return;
            }

            stateMachine.StopHorizontalMovement();
        }
    }

    public class ExplorationMoveState : LocomotionState
    {
        public ExplorationMoveState(LocomotionStateMachine sm) : base(sm) { }

        public override void Enter()
        {
            // BUG FIX: Phải đảm bảo Animator đang ở Blend Tree "Locomotion"
            // Nếu state trước là Dash/Land/Jump → Animator đang ở state khác
            // → SetFloat("MoveSpeed") sẽ vô nghĩa nếu không CrossFade về Locomotion
            AnimController?.PlayAnimation("Locomotion", AnimationPriority.Locomotion);
        }

        public override void Tick(float deltaTime)
        {
            if (Input.JumpInput && stateMachine.IsGrounded)
            {
                Input.ConsumeJumpInput();
                stateMachine.SwitchState(new ExplorationJumpState(stateMachine), LocomotionStateType.Jump);
                return;
            }

            if (Input.DashInput && stateMachine.CanDash())
            {
                Input.ConsumeDashInput();
                stateMachine.SwitchState(new ExplorationDashState(stateMachine), LocomotionStateType.Dash);
                return;
            }

            if (Input.MoveInput.magnitude < 0.1f)
            {
                stateMachine.SwitchState(new ExplorationIdleState(stateMachine), LocomotionStateType.Idle);
                return;
            }

            if (Input.SprintHeld && Health.HasStamina(1f))
            {
                stateMachine.SwitchState(new ExplorationSprintState(stateMachine), LocomotionStateType.Sprint);
                return;
            }

            if (!stateMachine.IsGrounded && stateMachine.TimeSinceGrounded > stateMachine.coyoteTime)
            {
                stateMachine.SwitchState(new ExplorationFallState(stateMachine), LocomotionStateType.Fall);
                return;
            }

            float speed = Mathf.Lerp(stateMachine.walkSpeed, stateMachine.runSpeed, Input.MoveInput.magnitude);
            float statSpeed = Stats != null ? Stats.MoveSpeed : speed;
            stateMachine.ApplyMovement(Input.MoveInput, Mathf.Min(speed, statSpeed), deltaTime);
        }
    }

    public class ExplorationSprintState : LocomotionState
    {
        public ExplorationSprintState(LocomotionStateMachine sm) : base(sm) { }

        public override void Enter()
        {
            // BUG FIX: Đảm bảo Animator ở Blend Tree "Locomotion"
            AnimController?.PlayAnimation("Locomotion", AnimationPriority.Locomotion);
        }

        public override void Tick(float deltaTime)
        {
            if (Input.JumpInput && stateMachine.IsGrounded)
            {
                Input.ConsumeJumpInput();
                stateMachine.SwitchState(new ExplorationJumpState(stateMachine), LocomotionStateType.Jump);
                return;
            }

            if (Input.DashInput && stateMachine.CanDash())
            {
                Input.ConsumeDashInput();
                stateMachine.SwitchState(new ExplorationDashState(stateMachine), LocomotionStateType.Dash);
                return;
            }

            bool canSprint = Input.SprintHeld && Input.MoveInput.magnitude > 0.1f && Health.HasStamina(1f);

            if (!canSprint)
            {
                if (Input.MoveInput.magnitude < 0.1f)
                    stateMachine.SwitchState(new ExplorationIdleState(stateMachine), LocomotionStateType.Idle);
                else
                    stateMachine.SwitchState(new ExplorationMoveState(stateMachine), LocomotionStateType.Move);
                return;
            }

            if (!stateMachine.IsGrounded && stateMachine.TimeSinceGrounded > stateMachine.coyoteTime)
            {
                stateMachine.SwitchState(new ExplorationFallState(stateMachine), LocomotionStateType.Fall);
                return;
            }

            Health.ModifyResource(ResourceType.Stamina, -stateMachine.sprintStaminaCost * deltaTime);
            stateMachine.ApplyMovement(Input.MoveInput, stateMachine.sprintSpeed, deltaTime);
        }
    }

    public class ExplorationJumpState : LocomotionState
    {
        public ExplorationJumpState(LocomotionStateMachine sm) : base(sm) { }

        public override void Enter()
        {
            Vector3 vel = stateMachine.Velocity;
            stateMachine.Velocity = new Vector3(vel.x, stateMachine.jumpForce, vel.z);
            stateMachine.HasUsedDoubleJump = false;
            AnimController?.PlayAnimation("Explore_Jump", AnimationPriority.Locomotion);
        }

        public override void Tick(float deltaTime)
        {
            if (stateMachine.Velocity.y <= 0f)
            {
                stateMachine.SwitchState(new ExplorationFallState(stateMachine), LocomotionStateType.Fall);
                return;
            }

            if (Input.JumpInput && stateMachine.hasDoubleJump && !stateMachine.HasUsedDoubleJump)
            {
                Input.ConsumeJumpInput();
                stateMachine.SwitchState(new ExplorationDoubleJumpState(stateMachine), LocomotionStateType.DoubleJump);
                return;
            }

            if (Input.MoveInput.magnitude > 0.1f)
                stateMachine.ApplyMovement(Input.MoveInput, stateMachine.runSpeed * 0.8f, deltaTime);
        }
    }

    public class ExplorationDoubleJumpState : LocomotionState
    {
        public ExplorationDoubleJumpState(LocomotionStateMachine sm) : base(sm) { }

        public override void Enter()
        {
            Vector3 vel = stateMachine.Velocity;
            stateMachine.Velocity = new Vector3(vel.x, stateMachine.doubleJumpForce, vel.z);
            stateMachine.HasUsedDoubleJump = true;
            AnimController?.PlayAnimation("Explore_DoubleJump", AnimationPriority.Locomotion);
        }

        public override void Tick(float deltaTime)
        {
            if (stateMachine.Velocity.y <= 0f)
            {
                stateMachine.SwitchState(new ExplorationFallState(stateMachine), LocomotionStateType.Fall);
                return;
            }

            if (Input.MoveInput.magnitude > 0.1f)
                stateMachine.ApplyMovement(Input.MoveInput, stateMachine.runSpeed * 0.8f, deltaTime);
        }
    }

    public class ExplorationFallState : LocomotionState
    {
        public ExplorationFallState(LocomotionStateMachine sm) : base(sm) { }

        public override void Enter()
        {
            AnimController?.PlayAnimation("Explore_Fall", AnimationPriority.Locomotion);
        }

        public override void Tick(float deltaTime)
        {
            if (Input.JumpInput && stateMachine.hasDoubleJump && !stateMachine.HasUsedDoubleJump)
            {
                Input.ConsumeJumpInput();
                stateMachine.SwitchState(new ExplorationDoubleJumpState(stateMachine), LocomotionStateType.DoubleJump);
                return;
            }

            if (stateMachine.IsGrounded)
            {
                stateMachine.SwitchState(new ExplorationLandState(stateMachine), LocomotionStateType.Land);
                return;
            }

            if (Input.MoveInput.magnitude > 0.1f)
                stateMachine.ApplyMovement(Input.MoveInput, stateMachine.runSpeed * 0.6f, deltaTime);
        }
    }

    public class ExplorationLandState : LocomotionState
    {
        private float timer;
        private bool isHardLand;

        public ExplorationLandState(LocomotionStateMachine sm) : base(sm) { }

        public override void Enter()
        {
            isHardLand = stateMachine.Velocity.y < stateMachine.hardLandThreshold;
            timer = isHardLand ? stateMachine.hardLandDuration : stateMachine.softLandDuration;

            string anim = isHardLand ? "Explore_Land_Hard" : "Explore_Land_Soft";
            AnimController?.PlayAnimation(anim, AnimationPriority.Locomotion);
            stateMachine.HasUsedDoubleJump = false;
        }

        public override void Tick(float deltaTime)
        {
            timer -= deltaTime;

            if (timer <= 0f || (!isHardLand && Input.MoveInput.magnitude > 0.1f))
            {
                if (Input.MoveInput.magnitude > 0.1f)
                    stateMachine.SwitchState(new ExplorationMoveState(stateMachine), LocomotionStateType.Move);
                else
                    stateMachine.SwitchState(new ExplorationIdleState(stateMachine), LocomotionStateType.Idle);
                return;
            }

            stateMachine.StopHorizontalMovement();
        }
    }

    public class ExplorationDashState : LocomotionState
    {
        private Vector3 dashDirection;
        private float elapsed;

        public ExplorationDashState(LocomotionStateMachine sm) : base(sm) { }

        public override void Enter()
        {
            elapsed = 0f;
            stateMachine.LastDashTime = Time.time;
            Health?.TryConsumeStamina(stateMachine.dashStaminaCost);
            Input.ConsumeDashInput();

            if (Input.MoveInput.magnitude > 0.1f)
            {
                Vector3 camForward = stateMachine.CameraTransform.forward;
                Vector3 camRight = stateMachine.CameraTransform.right;
                camForward.y = 0f;
                camRight.y = 0f;
                dashDirection = (camForward.normalized * Input.MoveInput.y + camRight.normalized * Input.MoveInput.x).normalized;
            }
            else
            {
                dashDirection = stateMachine.transform.forward;
            }

            stateMachine.transform.rotation = Quaternion.LookRotation(dashDirection);
            AnimController?.PlayAnimation("Explore_Dash", AnimationPriority.Skill, 0.05f);
        }

        public override void Tick(float deltaTime)
        {
            elapsed += deltaTime;

            float forceDuration = stateMachine.dashDuration;  // 0.25s — lực dash
            float totalDuration = stateMachine.dashAnimDuration; // 0.5s — tổng thời gian ở state này

            if (elapsed < forceDuration)
            {
                // Giai đoạn 1: Lực dash (giảm dần)
                float t = elapsed / forceDuration;
                float curve = 1f - t * t; // ease-out: nhanh → chậm
                stateMachine.Velocity = new Vector3(
                    dashDirection.x * stateMachine.dashSpeed * curve,
                    stateMachine.Velocity.y,
                    dashDirection.z * stateMachine.dashSpeed * curve
                );
            }
            else if (elapsed < totalDuration)
            {
                // Giai đoạn 2: Hết lực, anim vẫn play, player di chuyển bình thường
                if (Input.MoveInput.magnitude > 0.1f)
                {
                    float speed = Mathf.Lerp(stateMachine.walkSpeed, stateMachine.runSpeed,
                        Input.MoveInput.magnitude);
                    stateMachine.ApplyMovement(Input.MoveInput, speed, deltaTime);
                }
                else
                {
                    stateMachine.StopHorizontalMovement();
                }
            }
            else
            {
                // Giai đoạn 3: Hết tổng thời gian → CrossFade về Locomotion + exit
                if (Input.MoveInput.magnitude > 0.1f)
                    stateMachine.SwitchState(new ExplorationMoveState(stateMachine), LocomotionStateType.Move);
                else
                    stateMachine.SwitchState(new ExplorationIdleState(stateMachine), LocomotionStateType.Idle);
            }
        }
    }
}
