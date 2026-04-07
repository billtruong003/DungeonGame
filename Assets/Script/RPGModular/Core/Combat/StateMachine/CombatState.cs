// File: Core/Combat/StateMachine/CombatState.cs
// Base class cho combat state machine
// Cùng pattern với LocomotionState nhưng cho combat
namespace RPGModular
{
    public abstract class CombatState
    {
        protected readonly CombatStateMachine stateMachine;

        // Quick access references
        protected AnimationController AnimController => stateMachine.AnimController;
        protected HealthSystem Health => stateMachine.Health;
        protected WeaponHandler Weapons => stateMachine.Weapons;
        protected CombatInputHandler Input => stateMachine.CombatInput;
        protected CombatLocomotion CombatLoco => stateMachine.CombatLoco;
        protected HitboxManager Hitbox => stateMachine.Hitbox;
        protected CharacterStats Stats => stateMachine.Stats;

        public CombatState(CombatStateMachine stateMachine)
        {
            this.stateMachine = stateMachine;
        }

        public virtual void Enter() { }
        public virtual void Tick(float deltaTime) { }
        public virtual void Exit() { }

        /// <summary>
        /// Xử lý khi bị hit trong state này.
        /// Return true = state đã xử lý, false = để state machine xử lý default.
        /// </summary>
        public virtual bool HandleHit(DamageInfo damageInfo) => false;
    }
}
