using System;
using UnityEngine;

namespace RPGModular
{
    public class CombatStateMachine : MonoBehaviour
    {
        [field: Header("Core Dependencies")]
        [field: SerializeField] public AnimationController AnimController { get; private set; }
        [field: SerializeField] public CharacterStats Stats { get; private set; }
        [field: SerializeField] public HealthSystem Health { get; private set; }
        [field: SerializeField] public WeaponHandler Weapons { get; private set; }
        [field: SerializeField] public CombatLocomotion CombatLoco { get; private set; }
        [field: SerializeField] public PlayerInputHandler PlayerInput { get; private set; }
        [field: SerializeField] public HitboxManager Hitbox { get; private set; }
        [field: SerializeField] public LockOnSystem LockOn { get; private set; }
        [field: SerializeField] public AutoAttackSystem AutoAttack { get; private set; }

        [Header("Combat Params")]
        public float comboResetTime = 1.0f;
        public float blockStaminaCost = 10f;
        public float blockHeavyStaminaCost = 30f;
        public float parryStaminaCost = 15f;

        [Header("Dodge")]
        public float dodgeStaminaCost = 25f;
        public float dodgeDuration = 0.4f;
        public float dodgeSpeed = 12f;
        public float dodgeIFrameStart = 0f;
        public float dodgeIFrameEnd = 0.25f;
        public float dodgeCooldown = 0.5f;

        [Header("Guard Break")]
        public float guardBreakDuration = 1.5f;

        public CombatState CurrentState { get; private set; }
        public CombatStateType CurrentStateType { get; private set; }
        public int CurrentComboIndex { get; set; }
        public float ComboTimer { get; set; }
        public float LastDodgeTime { get; set; }

        public DamagePipeline DamagePipeline { get; private set; }

        public event Action<CombatStateType, CombatStateType> OnStateChanged;

        private void Awake()
        {
            AutoFindDependencies();
            DamagePipeline = new DamagePipeline();
        }

        private void Start()
        {
            SwitchState(new CombatIdleState(this), CombatStateType.Idle);

            if (Health != null)
                Health.OnDeath += HandleDeath;
            if (LockOn != null)
            {
                LockOn.OnTargetLocked += _ => OnTargetAcquired();
                LockOn.OnTargetLost += OnTargetLost;
            }
        }

        private void OnDestroy()
        {
            if (Health != null)
                Health.OnDeath -= HandleDeath;
        }

        private void Update()
        {
            CurrentState?.Tick(Time.deltaTime);

            if (CurrentComboIndex > 0)
            {
                ComboTimer -= Time.deltaTime;
                if (ComboTimer <= 0f)
                {
                    CurrentComboIndex = 0;
                    ComboTimer = 0f;
                }
            }

            if (PlayerInput != null && PlayerInput.LockOnToggle)
            {
                PlayerInput.ConsumeLockOnInput();
                LockOn?.ToggleLock();
            }

            if (PlayerInput != null && PlayerInput.SwitchTargetDirection != 0 && LockOn != null)
                LockOn.SwitchTarget(PlayerInput.SwitchTargetDirection);
        }

        public void SwitchState(CombatState newState, CombatStateType type)
        {
            var oldType = CurrentStateType;
            CurrentState?.Exit();
            CurrentState = newState;
            CurrentStateType = type;
            CurrentState?.Enter();
            OnStateChanged?.Invoke(oldType, type);
        }

        public void ReturnToNeutral()
        {
            if (LockOn != null && LockOn.IsLockedOn)
                SwitchState(new CombatEngagedState(this), CombatStateType.Combat);
            else
                SwitchState(new CombatIdleState(this), CombatStateType.Idle);
        }

        public bool CanDodge()
        {
            return Time.time - LastDodgeTime > dodgeCooldown
                && Health != null
                && Health.HasStamina(dodgeStaminaCost);
        }

        private void HandleDeath()
        {
            SwitchState(new DeadState(this), CombatStateType.Dead);
        }

        private void OnTargetAcquired()
        {
            if (CurrentStateType == CombatStateType.Idle)
                SwitchState(new CombatEngagedState(this), CombatStateType.Combat);
            Health?.SetCombatMode(true);
        }

        private void OnTargetLost()
        {
            if (CurrentStateType == CombatStateType.Combat)
                SwitchState(new CombatIdleState(this), CombatStateType.Idle);
            Health?.SetCombatMode(false);
        }

        private void AutoFindDependencies()
        {
            if (AnimController == null) AnimController = GetComponentInChildren<AnimationController>();
            if (Stats == null) Stats = GetComponent<CharacterStats>();
            if (Health == null) Health = GetComponent<HealthSystem>();
            if (Weapons == null) Weapons = GetComponent<WeaponHandler>();
            if (CombatLoco == null) CombatLoco = GetComponent<CombatLocomotion>();
            if (PlayerInput == null) PlayerInput = GetComponent<PlayerInputHandler>();
            if (Hitbox == null) Hitbox = GetComponentInChildren<HitboxManager>();
            if (LockOn == null) LockOn = GetComponent<LockOnSystem>();
            if (AutoAttack == null) AutoAttack = GetComponent<AutoAttackSystem>();
        }

#if UNITY_EDITOR
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 280, 260));
            GUI.color = Color.white;
            var style = new GUIStyle(GUI.skin.label) { richText = true };
            GUILayout.Label($"<b>Combat State:</b> {CurrentStateType}", style);
            if (Health != null)
            {
                GUILayout.Label($"HP: {Health.CurrentHP:F0}/{Health.MaxHP:F0} ({Health.HPPercent:P0})");
                GUILayout.Label($"Mana: {Health.CurrentMana:F0}/{Health.MaxMana:F0}");
                GUILayout.Label($"Stamina: {Health.CurrentStamina:F0}/{Health.MaxStamina:F0}");
            }
            if (Weapons != null)
                GUILayout.Label($"Weapon: {Weapons.CurrentWeaponType}");
            GUILayout.Label($"Combo: {CurrentComboIndex}/{Weapons?.MaxComboCount ?? 0}");
            if (AnimController != null)
            {
                GUILayout.Label($"Anim: {AnimController.CurrentPriority} / {AnimController.CurrentPhase}");
                GUILayout.Label($"CanInterrupt: {AnimController.CanBeInterrupted}");
            }
            if (LockOn != null)
                GUILayout.Label($"Lock-On: {LockOn.IsLockedOn} | Dist: {LockOn.DistanceToTarget:F1}m");
            if (AutoAttack != null)
                GUILayout.Label($"AutoAtk: {AutoAttack.AutoAttackEnabled} | InRange: {AutoAttack.IsInAttackRange()}");
            GUILayout.EndArea();
        }
#endif
    }

    public enum CombatStateType
    {
        Idle,
        Combat,
        Attacking,
        Blocking,
        Parrying,
        HitStun,
        Knockback,
        Dodge,
        GuardBreak,
        Dead
    }
}
