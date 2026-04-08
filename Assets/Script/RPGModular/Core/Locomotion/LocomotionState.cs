namespace RPGModular
{
    public enum LocomotionStateType
    {
        Idle,
        Move,
        Sprint,
        Jump,
        DoubleJump,
        Fall,
        Land,
        Dash
    }

    public abstract class LocomotionState
    {
        protected readonly LocomotionStateMachine stateMachine;

        protected AnimationController AnimController => stateMachine.AnimController;
        protected CharacterStats Stats => stateMachine.Stats;
        protected HealthSystem Health => stateMachine.Health;
        protected PlayerInputHandler Input => stateMachine.Input;

        public LocomotionState(LocomotionStateMachine stateMachine)
        {
            this.stateMachine = stateMachine;
        }

        public virtual void Enter() { }
        public virtual void Tick(float deltaTime) { }
        public virtual void Exit() { }
    }
}
