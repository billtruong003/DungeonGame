using UnityEngine;

namespace RPGModular
{
    public class ComboReadyState : CombatState
    {
        private float windowDuration;
        private float timer;

        public ComboReadyState(CombatStateMachine sm, float windowDuration) : base(sm)
        {
            this.windowDuration = windowDuration;
        }

        public override void Enter()
        {
            timer = windowDuration;
        }

        public override void Tick(float deltaTime)
        {
            timer -= deltaTime;

            // Check skill input for combo chain
            if (Input != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (Input.GetSkillInput(i))
                    {
                        Input.ConsumeSkillInput(i);
                        Game.Skill?.Cast(i);
                        return;
                    }
                }
            }

            // Dodge cancel
            if ((Input.JumpInput || Input.DoubleTapDodge) && stateMachine.CanDodge())
            {
                Input.ConsumeJumpInput();
                Input.ConsumeDoubleTapDodge();
                Game.Combo?.EndCombo();
                Vector2 dodgeDir = Input.MoveInput.magnitude > 0.1f ? Input.MoveInput : Vector2.down;
                stateMachine.SwitchState(
                    new DodgeState(stateMachine, dodgeDir), CombatStateType.Dodge);
                return;
            }

            // Window expired → return to engaged (auto-attack resumes)
            if (timer <= 0f)
            {
                stateMachine.ReturnToNeutral();
            }
        }
    }
}
