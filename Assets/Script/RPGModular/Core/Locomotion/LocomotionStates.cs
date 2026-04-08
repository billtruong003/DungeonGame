using UnityEngine;

namespace RPGModular
{
    public class ExplorationIdleState : LocomotionState
    {
        public ExplorationIdleState(LocomotionStateMachine sm) : base(sm) { }

        public override void Enter()
        {
            AnimController?.PlayAnimation("Idle", AnimationPriority.Locomotion);
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
            AnimController?.PlayAnimation("Jump", AnimationPriority.Locomotion);
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
            AnimController?.PlayAnimation("DoubleJump", AnimationPriority.Locomotion);
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
            AnimController?.PlayAnimation("Fall", AnimationPriority.Locomotion);
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

            string anim = isHardLand ? "HardLand" : "Land";
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
        private float timer;
        private Vector3 dashDirection;

        public ExplorationDashState(LocomotionStateMachine sm) : base(sm) { }

        public override void Enter()
        {
            timer = stateMachine.dashDuration;
            stateMachine.LastDashTime = Time.time;
            Health?.TryConsumeStamina(stateMachine.dashStaminaCost);

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

            Quaternion targetRot = Quaternion.LookRotation(dashDirection);
            stateMachine.transform.rotation = targetRot;

            AnimController?.PlayAnimation("Dash", AnimationPriority.Skill);
        }

        public override void Tick(float deltaTime)
        {
            timer -= deltaTime;

            if (timer <= 0f)
            {
                if (Input.MoveInput.magnitude > 0.1f)
                    stateMachine.SwitchState(new ExplorationMoveState(stateMachine), LocomotionStateType.Move);
                else
                    stateMachine.SwitchState(new ExplorationIdleState(stateMachine), LocomotionStateType.Idle);
                return;
            }

            stateMachine.Velocity = new Vector3(
                dashDirection.x * stateMachine.dashSpeed,
                stateMachine.Velocity.y,
                dashDirection.z * stateMachine.dashSpeed
            );
        }
    }
}
