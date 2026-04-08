namespace RPGModular
{
    public abstract class CombatState
    {
        protected readonly CombatStateMachine stateMachine;

        protected AnimationController AnimController => stateMachine.AnimController;
        protected HealthSystem Health => stateMachine.Health;
        protected WeaponHandler Weapons => stateMachine.Weapons;
        protected PlayerInputHandler Input => stateMachine.PlayerInput;
        protected CombatLocomotion CombatLoco => stateMachine.CombatLoco;
        protected HitboxManager Hitbox => stateMachine.Hitbox;
        protected CharacterStats Stats => stateMachine.Stats;
        protected AutoAttackSystem AutoAttack => stateMachine.AutoAttack;

        public CombatState(CombatStateMachine stateMachine)
        {
            this.stateMachine = stateMachine;
        }

        public virtual void Enter() { }
        public virtual void Tick(float deltaTime) { }
        public virtual void Exit() { }
        public virtual bool HandleHit(DamageInfo damageInfo) => false;
    }
}
