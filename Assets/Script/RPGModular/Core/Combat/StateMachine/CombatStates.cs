using UnityEngine;

namespace RPGModular
{
    public class CombatIdleState : CombatState
    {
        public CombatIdleState(CombatStateMachine sm) : base(sm) { }

        public override void Enter()
        {
            AnimController?.SetCombatMode(false);
        }

        public override void Tick(float deltaTime)
        {
            if (Input.AttackInput)
            {
                Input.ConsumeAttackInput();
                stateMachine.SwitchState(
                    new AttackingState(stateMachine, false), CombatStateType.Attacking);
                return;
            }
        }
    }

    public class CombatEngagedState : CombatState
    {
        public CombatEngagedState(CombatStateMachine sm) : base(sm) { }

        public override void Enter()
        {
            AnimController?.SetCombatMode(true);

            if (stateMachine.LockOn?.CurrentTargetTransform != null)
                CombatLoco?.SetLockOnTarget(stateMachine.LockOn.CurrentTargetTransform);
        }

        public override void Tick(float deltaTime)
        {
            if (TryDodge()) return;

            // Skill input check (1-4 slots)
            if (Input != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (Input.GetSkillInput(i))
                    {
                        Input.ConsumeSkillInput(i);
                        AutoAttack?.InterruptAutoAttack();
                        Game.Skill?.Cast(i);
                        return;
                    }
                }
            }

            if (Input.AttackInput)
            {
                Input.ConsumeAttackInput();
                AutoAttack?.InterruptAutoAttack();
                stateMachine.SwitchState(
                    new AttackingState(stateMachine, false), CombatStateType.Attacking);
                return;
            }

            if (Input.HeavyAttackInput)
            {
                Input.ConsumeHeavyAttackInput();
                AutoAttack?.InterruptAutoAttack();
                stateMachine.SwitchState(
                    new AttackingState(stateMachine, true), CombatStateType.Attacking);
                return;
            }

            if (AutoAttack != null && AutoAttack.TryAutoAttack())
            {
                stateMachine.SwitchState(
                    new AttackingState(stateMachine, false), CombatStateType.Attacking);
                return;
            }

            CombatLoco?.HandleCombatMovement(Input.MoveInput, Stats.MoveSpeed);
        }

        private bool TryDodge()
        {
            bool wantsDodge = (Input.JumpInput || Input.DoubleTapDodge) && stateMachine.CanDodge();
            if (!wantsDodge) return false;

            Input.ConsumeJumpInput();
            Input.ConsumeDoubleTapDodge();
            AutoAttack?.InterruptAutoAttack();

            Vector2 dodgeDir = Input.MoveInput.magnitude > 0.1f ? Input.MoveInput : Vector2.down;
            stateMachine.SwitchState(
                new DodgeState(stateMachine, dodgeDir), CombatStateType.Dodge);
            return true;
        }
    }

    public class AttackingState : CombatState
    {
        private bool isHeavy;
        private bool actionPlayed;
        private bool waitingForRecoveryInput;

        public AttackingState(CombatStateMachine sm, bool heavy) : base(sm)
        {
            isHeavy = heavy;
        }

        public override void Enter()
        {
            var actionData = Weapons.GetNormalAttackAction(stateMachine.CurrentComboIndex);
            if (actionData == null)
            {
                stateMachine.ReturnToNeutral();
                return;
            }

            Hitbox?.PrepareAttack(isHeavy);

            AnimationPriority priority = isHeavy
                ? AnimationPriority.Skill
                : AnimationPriority.NormalAttack;

            actionPlayed = AnimController.PlayAction(actionData, priority, OnPhaseChanged);

            if (actionPlayed)
            {
                stateMachine.CurrentComboIndex =
                    (stateMachine.CurrentComboIndex + 1) % Weapons.MaxComboCount;
                stateMachine.ComboTimer = stateMachine.comboResetTime;
            }
            else
            {
                stateMachine.ReturnToNeutral();
            }
        }

        public override void Tick(float deltaTime)
        {
            if (!actionPlayed) return;

            if (TryDodgeCancel()) return;

            if (waitingForRecoveryInput && AnimController.CanBeInterrupted)
            {
                if (Input.AttackInput)
                {
                    Input.ConsumeAttackInput();
                    stateMachine.SwitchState(
                        new AttackingState(stateMachine, false), CombatStateType.Attacking);
                    return;
                }

                if (Input.HeavyAttackInput)
                {
                    Input.ConsumeHeavyAttackInput();
                    stateMachine.SwitchState(
                        new AttackingState(stateMachine, true), CombatStateType.Attacking);
                    return;
                }
            }

            if (AnimController.CurrentPhase == AnimationPhase.Done)
                stateMachine.ReturnToNeutral();
        }

        private bool TryDodgeCancel()
        {
            if (AnimController.CurrentPhase != AnimationPhase.Startup) return false;
            if (!AnimController.CanBeInterrupted) return false;

            bool wantsDodge = (Input.JumpInput || Input.DoubleTapDodge) && stateMachine.CanDodge();
            if (!wantsDodge) return false;

            Input.ConsumeJumpInput();
            Input.ConsumeDoubleTapDodge();

            Vector2 dodgeDir = Input.MoveInput.magnitude > 0.1f ? Input.MoveInput : Vector2.down;
            stateMachine.SwitchState(
                new DodgeState(stateMachine, dodgeDir), CombatStateType.Dodge);
            return true;
        }

        private void OnPhaseChanged(AnimationPhase phase)
        {
            if (phase == AnimationPhase.Recovery)
                waitingForRecoveryInput = true;
        }
    }

    public class DodgeState : CombatState
    {
        private Vector2 dodgeInput;
        private Vector3 dodgeDirection;
        private float timer;
        private bool isInvincible;

        public DodgeState(CombatStateMachine sm, Vector2 direction) : base(sm)
        {
            dodgeInput = direction;
        }

        public override void Enter()
        {
            timer = 0f;
            stateMachine.LastDodgeTime = Time.time;
            Health?.TryConsumeStamina(stateMachine.dodgeStaminaCost);

            if (CombatLoco != null && CombatLoco.IsLockedOn && CombatLoco.LockOnTarget != null)
            {
                Vector3 toTarget = (CombatLoco.LockOnTarget.position - stateMachine.transform.position).normalized;
                toTarget.y = 0f;
                Vector3 forward = toTarget;
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                dodgeDirection = (forward * dodgeInput.y + right * dodgeInput.x).normalized;
            }
            else
            {
                Vector3 fwd = stateMachine.transform.forward;
                Vector3 rgt = stateMachine.transform.right;
                dodgeDirection = (fwd * dodgeInput.y + rgt * dodgeInput.x).normalized;
            }

            if (dodgeDirection.sqrMagnitude < 0.01f)
                dodgeDirection = -stateMachine.transform.forward;

            Quaternion targetRot = Quaternion.LookRotation(dodgeDirection);
            stateMachine.transform.rotation = targetRot;

            string animName = GetDodgeAnimName();
            AnimController?.PlayAnimation(animName, AnimationPriority.Skill, 0.05f);
        }

        public override void Tick(float deltaTime)
        {
            timer += deltaTime;

            isInvincible = timer >= stateMachine.dodgeIFrameStart
                && timer <= stateMachine.dodgeIFrameEnd;

            if (timer < stateMachine.dodgeDuration)
            {
                float speedCurve = 1f - (timer / stateMachine.dodgeDuration);
                CombatLoco?.ApplyKnockback(dodgeDirection, stateMachine.dodgeSpeed * speedCurve * deltaTime * 60f);
            }

            if (timer >= stateMachine.dodgeDuration)
                stateMachine.ReturnToNeutral();
        }

        public override bool HandleHit(DamageInfo damageInfo)
        {
            return isInvincible;
        }

        private string GetDodgeAnimName()
        {
            float forward = dodgeInput.y;
            float right = dodgeInput.x;

            if (Mathf.Abs(forward) >= Mathf.Abs(right))
                return forward >= 0f ? "Dodge_Fwd" : "Dodge_Back";

            return right >= 0f ? "Dodge_Right" : "Dodge_Left";
        }
    }

    public class HitStunState : CombatState
    {
        private float duration;
        private float timer;
        private bool isHeavyHit;

        public HitStunState(CombatStateMachine sm, bool heavy, float stunDuration = 0.4f) : base(sm)
        {
            isHeavyHit = heavy;
            duration = heavy ? stunDuration * 1.5f : stunDuration;
        }

        public override void Enter()
        {
            timer = duration;

            var animSet = Weapons.MainHandWeapon?.AnimationSet
                ?? WeaponAnimationSet.CreateDefault(WeaponType.Unarmed);

            if (isHeavyHit)
                AnimController?.PlayAnimation(animSet.Knockback, AnimationPriority.Knockback);
            else
                AnimController?.PlayAnimation(animSet.HitLight, AnimationPriority.HitReaction);
        }

        public override void Tick(float deltaTime)
        {
            timer -= deltaTime;
            if (timer <= 0f)
                stateMachine.ReturnToNeutral();
        }
    }

    public class DeadState : CombatState
    {
        public DeadState(CombatStateMachine sm) : base(sm) { }

        public override void Enter()
        {
            AnimController?.ForcePlay("Death");
            CombatLoco?.ClearLockOn();
        }

        public override void Tick(float deltaTime) { }
    }
}
