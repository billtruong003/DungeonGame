// File: Core/Combat/StateMachine/CombatStates.cs
// Tất cả concrete state cho combat state machine
using UnityEngine;

namespace RPGModular
{
    // =========================================================================
    //  IDLE STATE - Ngoài chiến đấu, di chuyển tự do
    // =========================================================================
    public class CombatIdleState : CombatState
    {
        public CombatIdleState(CombatStateMachine sm) : base(sm) { }

        public override void Enter()
        {
            AnimController?.SetCombatMode(false);
        }

        public override void Tick(float deltaTime)
        {
            // Nếu attack → vào attacking ngay (không cần lock-on)
            if (Input.AttackInput)
            {
                Input.ConsumeAttackInput();
                stateMachine.SwitchState(
                    new AttackingState(stateMachine, false), CombatStateType.Attacking);
                return;
            }

            // Di chuyển thường handled bởi LocomotionStateMachine bên ngoài
        }
    }

    // =========================================================================
    //  COMBAT ENGAGED - Lock-on target, di chuyển chiến đấu
    // =========================================================================
    public class CombatEngagedState : CombatState
    {
        public CombatEngagedState(CombatStateMachine sm) : base(sm) { }

        public override void Enter()
        {
            AnimController?.SetCombatMode(true);

            // Sync CombatLocomotion với LockOn target
            if (stateMachine.LockOn?.CurrentTargetTransform != null)
            {
                CombatLoco?.SetLockOnTarget(stateMachine.LockOn.CurrentTargetTransform);
            }
        }

        public override void Tick(float deltaTime)
        {
            // Attack
            if (Input.AttackInput)
            {
                Input.ConsumeAttackInput();
                stateMachine.SwitchState(
                    new AttackingState(stateMachine, false), CombatStateType.Attacking);
                return;
            }

            // Heavy Attack
            if (Input.HeavyAttackInput)
            {
                Input.ConsumeHeavyAttackInput();
                stateMachine.SwitchState(
                    new AttackingState(stateMachine, true), CombatStateType.Attacking);
                return;
            }

            // Block
            if (Input.BlockHeld && Health.HasStamina(stateMachine.blockStaminaCost))
            {
                stateMachine.SwitchState(
                    new BlockingState(stateMachine), CombatStateType.Blocking);
                return;
            }

            // Combat movement
            CombatLoco?.HandleCombatMovement(Input.MoveInput, Stats.MoveSpeed);
        }

        public override void Exit()
        {
            // Không clear lock-on ở đây — chỉ clear khi explicit LockOff
        }
    }

    // =========================================================================
    //  ATTACKING STATE - Đang tung đòn (normal hoặc heavy)
    // =========================================================================
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
                // Advance combo
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

            // Trong Recovery phase: check input buffer cho combo chain
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

            // Khi animation hoàn tất
            if (AnimController.CurrentPhase == AnimationPhase.Done)
            {
                stateMachine.ReturnToNeutral();
            }
        }

        private void OnPhaseChanged(AnimationPhase phase)
        {
            if (phase == AnimationPhase.Recovery)
            {
                waitingForRecoveryInput = true;
            }
        }
    }

    // =========================================================================
    //  BLOCKING STATE - Đang giữ block
    // =========================================================================
    public class BlockingState : CombatState
    {
        private float parryWindowTimer;
        private bool inParryWindow;

        public BlockingState(CombatStateMachine sm) : base(sm) { }

        public override void Enter()
        {
            var animSet = Weapons.MainHandWeapon?.AnimationSet 
                         ?? WeaponAnimationSet.CreateDefault(WeaponType.Unarmed);

            AnimController.PlayAnimation(animSet.BlockIdle, AnimationPriority.Block);

            // Parry window: vài frame đầu tiên khi bắt đầu block
            parryWindowTimer = Stats.ParryWindow;
            inParryWindow = true;

            // Pause stamina regen khi block
            Health?.PauseRegen(ResourceType.Stamina, true);
        }

        public override void Tick(float deltaTime)
        {
            // Parry window countdown
            if (inParryWindow)
            {
                parryWindowTimer -= deltaTime;
                if (parryWindowTimer <= 0f)
                    inParryWindow = false;
            }

            // Thả block hoặc hết stamina
            if (!Input.BlockHeld || !Health.HasStamina(1f))
            {
                stateMachine.ReturnToNeutral();
                return;
            }

            // Vẫn có thể di chuyển chậm khi block
            if (CombatLoco != null && CombatLoco.IsLockedOn)
            {
                CombatLoco.HandleCombatMovement(Input.MoveInput, Stats.MoveSpeed * 0.5f);
            }
        }

        public override void Exit()
        {
            Health?.PauseRegen(ResourceType.Stamina, false);
        }

        /// <summary>
        /// Xử lý khi bị hit trong lúc block.
        /// Nếu trong parry window → parry.
        /// Nếu heavy attack → knockback + ít damage.
        /// Nếu thường → block thành công.
        /// </summary>
        public override bool HandleHit(DamageInfo damageInfo)
        {
            if (damageInfo.IsUnblockable) return false; // Để default handle

            var animSet = Weapons.MainHandWeapon?.AnimationSet 
                         ?? WeaponAnimationSet.CreateDefault(WeaponType.Unarmed);

            // === PARRY ===
            if (inParryWindow && damageInfo.CanParry)
            {
                Health?.TryConsumeStamina(stateMachine.parryStaminaCost);

                // Play parry animation
                AnimController.PlayAnimation("Parry_Success", AnimationPriority.Block);

                // TODO: Stun attacker, riposte window
                // Damage = 0 khi parry

                stateMachine.SwitchState(
                    new ParrySuccessState(stateMachine, damageInfo), CombatStateType.Parrying);
                return true;
            }

            // === BLOCK HEAVY → KNOCKBACK ===
            if (damageInfo.IsHeavyAttack)
            {
                Health?.TryConsumeStamina(stateMachine.blockHeavyStaminaCost);
                AnimController.PlayAnimation(animSet.BlockBreak, AnimationPriority.Knockback);
                CombatLoco?.ApplyKnockback(damageInfo.HitDirection, 8f);

                // Vẫn dính ít damage (pipeline sẽ xử lý block reduction)
                return false; // Để pipeline tính damage bình thường với state = Blocking
            }

            // === BLOCK THƯỜNG ===
            Health?.TryConsumeStamina(stateMachine.blockStaminaCost);
            AnimController.PlayAnimation(animSet.BlockHit, AnimationPriority.Block, 0.05f);

            return false; // Pipeline sẽ detect ECombatState.Blocking → giảm damage
        }
    }

    // =========================================================================
    //  PARRY SUCCESS - Vừa parry thành công, mở riposte window
    // =========================================================================
    public class ParrySuccessState : CombatState
    {
        private DamageInfo parryiedAttack;
        private float riposteWindow = 0.6f; // Thời gian có thể counter-attack
        private float timer;

        public ParrySuccessState(CombatStateMachine sm, DamageInfo parried) : base(sm)
        {
            parryiedAttack = parried;
        }

        public override void Enter()
        {
            timer = riposteWindow;
            AnimController?.PlayAnimation("Parry_Success", AnimationPriority.Block);
        }

        public override void Tick(float deltaTime)
        {
            timer -= deltaTime;

            // Trong riposte window, attack sẽ gây bonus damage
            if (Input.AttackInput)
            {
                Input.ConsumeAttackInput();
                // TODO: RiposteAttackState with bonus damage
                stateMachine.SwitchState(
                    new AttackingState(stateMachine, false), CombatStateType.Attacking);
                return;
            }

            if (timer <= 0f)
            {
                stateMachine.ReturnToNeutral();
            }
        }
    }

    // =========================================================================
    //  HIT STUN - Bị đánh trúng, đang choáng nhẹ
    // =========================================================================
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
            {
                AnimController?.PlayAnimation(animSet.Knockback, AnimationPriority.Knockback);
            }
            else
            {
                AnimController?.PlayAnimation(animSet.HitLight, AnimationPriority.HitReaction);
            }
        }

        public override void Tick(float deltaTime)
        {
            timer -= deltaTime;
            if (timer <= 0f)
            {
                stateMachine.ReturnToNeutral();
            }
        }
    }

    // =========================================================================
    //  DEAD STATE
    // =========================================================================
    public class DeadState : CombatState
    {
        public DeadState(CombatStateMachine sm) : base(sm) { }

        public override void Enter()
        {
            AnimController?.ForcePlay("Death");
            CombatLoco?.ClearLockOn();
        }

        public override void Tick(float deltaTime)
        {
            // Chờ revive hoặc game over
        }
    }
}
