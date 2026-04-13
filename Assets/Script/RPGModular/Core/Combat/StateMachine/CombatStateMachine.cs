using System;
using UnityEngine;
using BillInspector;

namespace RPGModular
{
    [BillTitle("Combat State Machine", "Player combat controller with damage dealing & receiving")]
    public class CombatStateMachine : MonoBehaviour, IDamageDealer, IDamageable
    {
        [BillBoxGroup("Dependencies")]
        [field: BillRequired("AnimController is required")]
        [field: SerializeField] public AnimationController AnimController { get; private set; }

        [BillBoxGroup("Dependencies")]
        [field: BillRequired("Stats is required")]
        [field: SerializeField] public CharacterStats Stats { get; private set; }

        [BillBoxGroup("Dependencies")]
        [field: BillRequired("Health is required")]
        [field: SerializeField] public HealthSystem Health { get; private set; }

        [BillBoxGroup("Dependencies")]
        [field: SerializeField] public WeaponHandler Weapons { get; private set; }

        [BillBoxGroup("Dependencies")]
        [field: SerializeField] public CombatLocomotion CombatLoco { get; private set; }

        [BillBoxGroup("Dependencies")]
        [field: SerializeField] public PlayerInputHandler PlayerInput { get; private set; }

        [BillBoxGroup("Dependencies")]
        [field: SerializeField] public HitboxManager Hitbox { get; private set; }

        [BillBoxGroup("Dependencies")]
        [field: SerializeField] public LockOnSystem LockOn { get; private set; }

        [BillBoxGroup("Dependencies")]
        [field: SerializeField] public AutoAttackSystem AutoAttack { get; private set; }

        [BillFoldoutGroup("Combat Params")]
        [BillSlider(0.3f, 3f)] public float comboResetTime = 1.0f;
        [BillFoldoutGroup("Combat Params")]
        [BillSlider(0f, 50f), BillSuffix("stamina")] public float blockStaminaCost = 10f;
        [BillFoldoutGroup("Combat Params")]
        [BillSlider(0f, 100f), BillSuffix("stamina")] public float blockHeavyStaminaCost = 30f;
        [BillFoldoutGroup("Combat Params")]
        [BillSlider(0f, 50f), BillSuffix("stamina")] public float parryStaminaCost = 15f;

        [BillFoldoutGroup("Dodge")]
        [BillSlider(0f, 50f), BillSuffix("stamina")] public float dodgeStaminaCost = 25f;
        [BillFoldoutGroup("Dodge")]
        [BillSlider(0.1f, 1f), BillSuffix("s")] public float dodgeDuration = 0.4f;
        [BillFoldoutGroup("Dodge")]
        [BillSlider(5f, 25f)] public float dodgeSpeed = 12f;
        [BillFoldoutGroup("Dodge")]
        [BillSlider(0f, 1f), BillSuffix("s")] public float dodgeIFrameStart = 0f;
        [BillFoldoutGroup("Dodge")]
        [BillSlider(0f, 1f), BillSuffix("s")] public float dodgeIFrameEnd = 0.25f;
        [BillFoldoutGroup("Dodge")]
        [BillSlider(0f, 2f), BillSuffix("s")] public float dodgeCooldown = 0.5f;

        [BillFoldoutGroup("Guard Break")]
        [BillSlider(0.5f, 3f), BillSuffix("s")] public float guardBreakDuration = 1.5f;

        // ═══════════════════════════════════════════════════════
        // Runtime state (read-only in inspector)
        // ═══════════════════════════════════════════════════════

        public CombatState CurrentState { get; private set; }
        public CombatStateType CurrentStateType { get; private set; }
        public int CurrentComboIndex { get; set; }
        public float ComboTimer { get; set; }
        public float LastDodgeTime { get; set; }

        public DamagePipeline DamagePipeline { get; private set; }

        // ═══════════════════════════════════════════════════════
        // IDamageable implementation
        // ═══════════════════════════════════════════════════════

        public float CurrentHP => Health != null ? Health.CurrentHP : 0f;
        public float MaxHP => Health != null ? Health.MaxHP : 100f;
        public bool IsAlive => Health != null && Health.IsAlive;
        public ECombatState CurrentCombatState => ConvertStateType(CurrentStateType);

        // ═══════════════════════════════════════════════════════
        // Events
        // ═══════════════════════════════════════════════════════

        public event Action<CombatStateType, CombatStateType> OnStateChanged;
        public event Action<DamageResult> OnDamageTaken;
        public event Action OnDeath;
        public event Action<IDamageable, DamageResult> OnDamageDealt;

        // ═══════════════════════════════════════════════════════
        // Lifecycle
        // ═══════════════════════════════════════════════════════

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

        // ═══════════════════════════════════════════════════════
        // State management
        // ═══════════════════════════════════════════════════════

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

        // ═══════════════════════════════════════════════════════
        // IDamageDealer — calculate outgoing damage
        // ═══════════════════════════════════════════════════════

        public DamageInfo CalculateDamage(bool isHeavyAttack = false)
        {
            var weapon = Weapons?.MainHandWeapon;
            float weaponDmg = weapon?.BaseDamage ?? 5f;
            DamageType dmgType = weapon?.PrimaryDamageType ?? DamageType.Strike;

            float rawDamage;
            switch (dmgType)
            {
                case DamageType.Slash:
                case DamageType.Pierce:
                case DamageType.Strike:
                    rawDamage = (Stats?.PhysicalAttack ?? 0f) + weaponDmg;
                    break;
                default:
                    rawDamage = (Stats?.MagicAttack ?? 0f) + weaponDmg;
                    break;
            }

            if (isHeavyAttack) rawDamage *= 1.5f;

            return new DamageInfo
            {
                RawDamage = rawDamage,
                Type = dmgType,
                CritMultiplier = 1f,
                IsCrit = false,
                KnockbackForce = isHeavyAttack ? 5f : 0f,
                HitDirection = transform.forward,
                Source = this,
                IsHeavyAttack = isHeavyAttack,
                IsUnblockable = false,
                CanParry = true
            };
        }

        /// <summary>
        /// Notify that this dealer dealt damage. Called by HitboxManager after hit confirmed.
        /// </summary>
        public void NotifyDamageDealt(IDamageable target, DamageResult result)
        {
            OnDamageDealt?.Invoke(target, result);
        }

        // ═══════════════════════════════════════════════════════
        // IDamageable — receive incoming damage
        // ═══════════════════════════════════════════════════════

        public DamageResult TakeDamage(DamageInfo damageInfo)
        {
            if (!IsAlive) return new DamageResult { FinalDamage = 0 };

            // Let current state handle first (parry window, dodge i-frames)
            if (CurrentState != null && CurrentState.HandleHit(damageInfo))
            {
                var handledResult = new DamageResult
                {
                    FinalDamage = 0,
                    WasParried = CurrentStateType == CombatStateType.Parrying,
                    WasDodged = CurrentStateType == CombatStateType.Dodge,
                };
                OnDamageTaken?.Invoke(handledResult);
                return handledResult;
            }

            // Build context for damage pipeline
            var context = new DamageContext
            {
                AttackerStats = (damageInfo.Source as MonoBehaviour)?.GetComponent<IStatProvider>(),
                DefenderStats = Stats,
                DefenderCombatState = CurrentCombatState,
                AttackerWeapon = null
            };

            DamageResult result = DamagePipeline.Calculate(damageInfo, context);

            if (!result.WasDodged && Health != null)
            {
                Health.ApplyDamage(result.FinalDamage);

                if (result.WasBlocked)
                {
                    float staminaCost = damageInfo.IsHeavyAttack ? blockHeavyStaminaCost : blockStaminaCost;
                    Health.TryConsumeStamina(staminaCost);
                }

                // Hit reaction (if not blocking and still alive)
                if (!result.WasBlocked && IsAlive)
                {
                    bool heavy = damageInfo.IsHeavyAttack || result.KnockbackForce > 0;
                    SwitchState(new HitStunState(this, heavy),
                        heavy ? CombatStateType.Knockback : CombatStateType.HitStun);

                    if (result.KnockbackForce > 0)
                        CombatLoco?.ApplyKnockback(result.KnockbackDirection, result.KnockbackForce);
                }
            }

            OnDamageTaken?.Invoke(result);
            return result;
        }

        // ═══════════════════════════════════════════════════════
        // Internal
        // ═══════════════════════════════════════════════════════

        private void HandleDeath()
        {
            SwitchState(new DeadState(this), CombatStateType.Dead);
            OnDeath?.Invoke();
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

        private ECombatState ConvertStateType(CombatStateType type)
        {
            switch (type)
            {
                case CombatStateType.Idle: return ECombatState.Idle;
                case CombatStateType.Combat: return ECombatState.Combat;
                case CombatStateType.Attacking: return ECombatState.Attacking;
                case CombatStateType.Blocking: return ECombatState.Blocking;
                case CombatStateType.Parrying: return ECombatState.Parrying;
                case CombatStateType.HitStun: return ECombatState.HitStun;
                case CombatStateType.Knockback: return ECombatState.Knockback;
                case CombatStateType.Dodge: return ECombatState.Dodge;
                case CombatStateType.GuardBreak: return ECombatState.GuardBreak;
                case CombatStateType.Dead: return ECombatState.Dead;
                default: return ECombatState.Idle;
            }
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
