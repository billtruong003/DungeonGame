using UnityEngine;

namespace RPGModular
{
    public class SkillChargeState : CombatState
    {
        private SkillCaster caster;
        private SkillData skill;
        private int level;
        private float chargeTimer;

        public SkillChargeState(CombatStateMachine sm, SkillCaster caster, SkillData skill, int level) : base(sm)
        {
            this.caster = caster;
            this.skill = skill;
            this.level = level;
        }

        public override void Enter()
        {
            chargeTimer = skill.castTime;

            if (!string.IsNullOrEmpty(skill.castVFXId))
            {
                // Bill.Pool.Spawn(skill.castVFXId, ...) — future VFX
            }

            AnimController?.PlayAnimation("Skill_Charge", AnimationPriority.Skill);
        }

        public override void Tick(float deltaTime)
        {
            chargeTimer -= deltaTime;

            // Dodge cancel
            if ((Input.JumpInput || Input.DoubleTapDodge) && stateMachine.CanDodge())
            {
                Input.ConsumeJumpInput();
                Input.ConsumeDoubleTapDodge();
                caster.NotifyCastInterrupted(skill);
                Vector2 dodgeDir = Input.MoveInput.magnitude > 0.1f ? Input.MoveInput : Vector2.down;
                stateMachine.SwitchState(
                    new DodgeState(stateMachine, dodgeDir), CombatStateType.Dodge);
                return;
            }

            if (chargeTimer <= 0f)
            {
                // Charge complete → execute
                stateMachine.SwitchState(
                    new SkillExecuteState(stateMachine, caster, skill, level),
                    CombatStateType.SkillExecute);
            }
        }

        public override bool HandleHit(DamageInfo damageInfo)
        {
            if (skill.hasSuperArmor) return false; // take damage but don't interrupt

            // Interrupted by hit
            caster.NotifyCastInterrupted(skill);
            return false; // let damage go through
        }
    }
}
