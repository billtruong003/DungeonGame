// File: Core/Combat/StateMachine/CombatStateMachine.cs
// State machine chính cho combat
// Quản lý transition giữa: Idle ↔ Combat ↔ Attacking ↔ Blocking ↔ Parrying ↔ HitStun ↔ Dead
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
        [field: SerializeField] public CombatInputHandler CombatInput { get; private set; }
        [field: SerializeField] public HitboxManager Hitbox { get; private set; }
        [field: SerializeField] public LockOnSystem LockOn { get; private set; }

        [Header("Combat Params")]
        public float comboResetTime = 1.0f;
        public float blockStaminaCost = 10f;
        public float blockHeavyStaminaCost = 30f;
        public float parryStaminaCost = 15f;
        public float dodgeStaminaCost = 20f;

        // State
        public CombatState CurrentState { get; private set; }
        public CombatStateType CurrentStateType { get; private set; }
        public int CurrentComboIndex { get; set; }
        public float ComboTimer { get; set; }

        // Damage pipeline (shared)
        public DamagePipeline DamagePipeline { get; private set; }

        // Events
        public event Action<CombatStateType, CombatStateType> OnStateChanged;

        private void Awake()
        {
            AutoFindDependencies();
            DamagePipeline = new DamagePipeline();
        }

        private void Start()
        {
            SwitchState(new CombatIdleState(this), CombatStateType.Idle);

            // Listen to events
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

            // Combo timer
            if (CurrentComboIndex > 0)
            {
                ComboTimer -= Time.deltaTime;
                if (ComboTimer <= 0f)
                {
                    CurrentComboIndex = 0;
                    ComboTimer = 0f;
                }
            }

            // Lock-on toggle
            if (CombatInput != null && CombatInput.LockOnToggle)
            {
                CombatInput.ConsumeLockOnInput();
                LockOn?.ToggleLock();
            }

            // Switch target
            if (CombatInput != null && CombatInput.SwitchTargetDirection != 0 && LockOn != null)
            {
                LockOn.SwitchTarget(CombatInput.SwitchTargetDirection);
            }
        }

        #region State Switching

        public void SwitchState(CombatState newState, CombatStateType type)
        {
            var oldType = CurrentStateType;
            CurrentState?.Exit();
            CurrentState = newState;
            CurrentStateType = type;
            CurrentState?.Enter();

            OnStateChanged?.Invoke(oldType, type);
        }

        /// <summary>
        /// Switch to combat idle (after action complete, etc.)
        /// Tự chọn Idle hoặc Combat dựa trên lock-on state
        /// </summary>
        public void ReturnToNeutral()
        {
            if (LockOn != null && LockOn.IsLockedOn)
                SwitchState(new CombatEngagedState(this), CombatStateType.Combat);
            else
                SwitchState(new CombatIdleState(this), CombatStateType.Idle);
        }

        #endregion

        #region Event Handlers

        private void HandleDeath()
        {
            SwitchState(new DeadState(this), CombatStateType.Dead);
        }

        private void OnTargetAcquired()
        {
            if (CurrentStateType == CombatStateType.Idle)
            {
                SwitchState(new CombatEngagedState(this), CombatStateType.Combat);
            }
            Health?.SetCombatMode(true);
            Health?.PauseRegen(ResourceType.Stamina, false);
        }

        private void OnTargetLost()
        {
            if (CurrentStateType == CombatStateType.Combat)
            {
                SwitchState(new CombatIdleState(this), CombatStateType.Idle);
            }
            Health?.SetCombatMode(false);
        }

        #endregion

        #region Helpers

        private void AutoFindDependencies()
        {
            if (AnimController == null) AnimController = GetComponentInChildren<AnimationController>();
            if (Stats == null) Stats = GetComponent<CharacterStats>();
            if (Health == null) Health = GetComponent<HealthSystem>();
            if (Weapons == null) Weapons = GetComponent<WeaponHandler>();
            if (CombatLoco == null) CombatLoco = GetComponent<CombatLocomotion>();
            if (CombatInput == null) CombatInput = GetComponent<CombatInputHandler>();
            if (Hitbox == null) Hitbox = GetComponentInChildren<HitboxManager>();
            if (LockOn == null) LockOn = GetComponent<LockOnSystem>();
        }

        #endregion

#if UNITY_EDITOR
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 280, 240));
            GUI.color = Color.white;
            GUILayout.Label($"<b>Combat State:</b> {CurrentStateType}", new GUIStyle(GUI.skin.label) { richText = true });
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
            GUILayout.EndArea();
        }
#endif
    }

    /// <summary>
    /// Enum cho UI và debug. Không dùng cho logic (dùng polymorphism thay thế).
    /// </summary>
    public enum CombatStateType
    {
        Idle,
        Combat,     // Lock-on, di chuyển chiến đấu
        Attacking,
        Blocking,
        Parrying,
        HitStun,
        Knockback,
        Dead
    }
}
