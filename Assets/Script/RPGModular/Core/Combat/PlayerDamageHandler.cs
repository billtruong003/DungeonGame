using System;
using UnityEngine;
using BillInspector;

namespace RPGModular
{
    /// <summary>
    /// Bridges CombatStateMachine with damage interfaces.
    /// Implements IDamageDealer + IDamageable + ITargetLockable.
    /// HitboxManager finds this via GetComponentInParent.
    /// Enemies attack this via GetComponent on player.
    /// </summary>
    [BillTitle("Player Damage Handler", "Bridges combat SM with damage system")]
    public class PlayerDamageHandler : MonoBehaviour, IDamageDealer, IDamageable, ITargetLockable
    {
        [BillBoxGroup("Dependencies")]
        [SerializeField] private CombatStateMachine combatSM;
        [SerializeField] private CharacterStats stats;
        [SerializeField] private HealthSystem health;
        [SerializeField] private WeaponHandler weapons;
        [SerializeField] private CombatLocomotion combatLoco;

        [BillBoxGroup("Lock-On")]
        [SerializeField] private Transform lockOnPoint;

        private DamagePipeline _pipeline;

        // ═══════════════════════════════════════════════════════
        // IDamageable
        // ═══════════════════════════════════════════════════════

        public float CurrentHP => health != null ? health.CurrentHP : 0f;
        public float MaxHP => health != null ? health.MaxHP : 100f;
        public bool IsAlive => health != null && health.IsAlive;
        public ECombatState CurrentCombatState => combatSM != null
            ? ConvertState(combatSM.CurrentStateType)
            : ECombatState.Idle;

        public event Action<DamageResult> OnDamageTaken;
        public event Action OnDeath;

        // ═══════════════════════════════════════════════════════
        // IDamageDealer
        // ═══════════════════════════════════════════════════════

        public event Action<IDamageable, DamageResult> OnDamageDealt;

        // ═══════════════════════════════════════════════════════
        // ITargetLockable
        // ═══════════════════════════════════════════════════════

        public Transform LockOnPoint => lockOnPoint != null ? lockOnPoint : transform;
        public bool CanBeLocked => IsAlive;

        // ═══════════════════════════════════════════════════════
        // Lifecycle
        // ═══════════════════════════════════════════════════════

        private void Awake()
        {
            if (combatSM == null) combatSM = GetComponent<CombatStateMachine>();
            if (stats == null) stats = GetComponent<CharacterStats>();
            if (health == null) health = GetComponent<HealthSystem>();
            if (weapons == null) weapons = GetComponent<WeaponHandler>();
            if (combatLoco == null) combatLoco = GetComponent<CombatLocomotion>();

            _pipeline = new DamagePipeline();
        }

        private void OnEnable()
        {
            if (health != null) health.OnDeath += HandleDeath;
        }

        private void OnDisable()
        {
            if (health != null) health.OnDeath -= HandleDeath;
        }

        // ═══════════════════════════════════════════════════════
        // IDamageDealer — calculate outgoing damage
        // ═══════════════════════════════════════════════════════

        public DamageInfo CalculateDamage(bool isHeavyAttack = false)
        {
            var weapon = weapons?.MainHandWeapon;
            float weaponDmg = weapon?.BaseDamage ?? 5f;
            DamageType dmgType = weapon?.PrimaryDamageType ?? DamageType.Strike;

            float rawDamage;
            switch (dmgType)
            {
                case DamageType.Slash:
                case DamageType.Pierce:
                case DamageType.Strike:
                    rawDamage = (stats?.PhysicalAttack ?? 0f) + weaponDmg;
                    break;
                default:
                    rawDamage = (stats?.MagicAttack ?? 0f) + weaponDmg;
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

        /// <summary>Called by HitboxManager after hit confirmed.</summary>
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

            // Let current combat state handle (dodge i-frames, block/parry skills)
            if (combatSM?.CurrentState != null && combatSM.CurrentState.HandleHit(damageInfo))
            {
                var handled = new DamageResult
                {
                    FinalDamage = 0,
                    WasDodged = combatSM.CurrentStateType == CombatStateType.Dodge,
                };
                OnDamageTaken?.Invoke(handled);
                return handled;
            }

            var context = new DamageContext
            {
                AttackerStats = (damageInfo.Source as MonoBehaviour)?.GetComponent<IStatProvider>(),
                DefenderStats = stats,
                DefenderCombatState = CurrentCombatState,
                AttackerWeapon = null
            };

            DamageResult result = _pipeline.Calculate(damageInfo, context);

            if (!result.WasDodged && health != null)
            {
                health.ApplyDamage(result.FinalDamage);

                if (IsAlive && result.FinalDamage > 0)
                {
                    bool heavy = damageInfo.IsHeavyAttack || result.KnockbackForce > 0;
                    combatSM?.SwitchState(new HitStunState(combatSM, heavy),
                        heavy ? CombatStateType.Knockback : CombatStateType.HitStun);

                    if (result.KnockbackForce > 0)
                        combatLoco?.ApplyKnockback(result.KnockbackDirection, result.KnockbackForce);
                }
            }

            OnDamageTaken?.Invoke(result);
            return result;
        }

        // ═══════════════════════════════════════════════════════
        // Simple float overload — for enemy attacks (EnemyBase calls this)
        // ═══════════════════════════════════════════════════════

        public void TakeDamage(float rawAmount)
        {
            TakeDamage(new DamageInfo
            {
                RawDamage = rawAmount,
                Type = DamageType.Strike,
                HitDirection = -transform.forward,
                Source = null,
                CanParry = true
            });
        }

        private void HandleDeath()
        {
            OnDeath?.Invoke();
        }

        private ECombatState ConvertState(CombatStateType type)
        {
            return type switch
            {
                CombatStateType.Idle => ECombatState.Idle,
                CombatStateType.Combat => ECombatState.Combat,
                CombatStateType.Attacking => ECombatState.Attacking,
                CombatStateType.SkillCharge => ECombatState.SkillCharge,
                CombatStateType.SkillExecute => ECombatState.SkillExecute,
                CombatStateType.ComboReady => ECombatState.ComboReady,
                CombatStateType.Dodge => ECombatState.Dodge,
                CombatStateType.HitStun => ECombatState.HitStun,
                CombatStateType.Knockback => ECombatState.Knockback,
                CombatStateType.Dead => ECombatState.Dead,
                _ => ECombatState.Idle,
            };
        }
    }
}
