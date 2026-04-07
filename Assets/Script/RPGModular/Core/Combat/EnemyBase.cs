// File: Core/Combat/EnemyBase.cs
// Base template cho Enemy - implement IDamageable + ITargetLockable
// Enemy cụ thể (melee, ranged, elite, boss) sẽ kế thừa class này
using System;
using UnityEngine;

namespace RPGModular
{
    /// <summary>
    /// Data config cho enemy (ScriptableObject)
    /// </summary>
    [CreateAssetMenu(fileName = "NewEnemy", menuName = "RPG/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        [Header("Basic")]
        public string enemyName = "Enemy";
        public float baseHP = 100f;
        public float baseDamage = 10f;
        public float moveSpeed = 3f;
        public float attackRange = 2f;
        public float detectionRange = 10f;

        [Header("Stats")]
        public float physicalDefense = 10f;
        public float magicDefense = 5f;
        public float dodgeChance = 0.05f;
        public float blockChance = 0f;      // 0 = không biết block

        [Header("Combat Behavior")]
        public float attackCooldown = 1.5f;
        public float attackSpeed = 1f;
        public bool canBlock = false;
        public bool canParry = false;
        public float blockDuration = 1f;
        
        [Header("Type")]
        public EnemyTier tier = EnemyTier.Normal;
        public DamageType damageType = DamageType.Strike;

        [Header("Rewards")]
        public int expReward = 50;
        public int goldReward = 10;
    }

    public enum EnemyTier
    {
        Normal,
        Elite,
        MiniBoss,
        Boss
    }

    /// <summary>
    /// Base class cho tất cả enemy.
    /// Subclass sẽ override behavior (AI pattern, special attacks).
    /// </summary>
    public class EnemyBase : MonoBehaviour, IDamageable, ITargetLockable, IDamageDealer
    {
        [Header("Config")]
        [SerializeField] protected EnemyData data;

        [Header("Components")]
        [SerializeField] protected AnimationController animController;
        [SerializeField] protected Transform lockOnPoint;

        // Runtime
        protected float currentHP;
        protected ECombatState currentCombatState = ECombatState.Idle;
        protected DamagePipeline damagePipeline;
        protected float attackCooldownTimer;

        // IDamageable
        public float CurrentHP => currentHP;
        public float MaxHP => data != null ? data.baseHP : 100f;
        public bool IsAlive => currentHP > 0;
        public ECombatState CurrentCombatState => ConvertState();

        // ITargetLockable
        public Transform LockOnPoint => lockOnPoint ?? transform;
        public bool CanBeLocked => IsAlive;

        // Events
        public event Action<DamageResult> OnDamageTaken;
        public event Action OnDeath;
        public event Action<IDamageable, DamageResult> OnDamageDealt;

        protected virtual void Awake()
        {
            if (animController == null)
                animController = GetComponentInChildren<AnimationController>();
            
            damagePipeline = new DamagePipeline();
        }

        protected virtual void Start()
        {
            if (data != null)
                currentHP = data.baseHP;
        }

        protected virtual void Update()
        {
            if (!IsAlive) return;
            attackCooldownTimer -= Time.deltaTime;
        }

        #region IDamageable

        public DamageResult TakeDamage(DamageInfo damageInfo)
        {
            if (!IsAlive) return new DamageResult { FinalDamage = 0 };

            // Đơn giản hóa context cho enemy (không có full stat system)
            var context = new DamageContext
            {
                AttackerStats = null,
                DefenderStats = null,
                DefenderCombatState = CurrentCombatState,
                AttackerWeapon = null,
                // Manual override defense
            };

            // Tính damage thủ công (enemy không dùng CharacterStats)
            float damage = damageInfo.RawDamage;

            // Dodge check
            if (data != null && UnityEngine.Random.value < data.dodgeChance)
            {
                PlayDodgeAnimation();
                var dodgeResult = new DamageResult { WasDodged = true, FinalDamage = 0 };
                OnDamageTaken?.Invoke(dodgeResult);
                return dodgeResult;
            }

            // Block check
            if (data != null && data.canBlock && currentCombatState == ECombatState.Blocking)
            {
                damage *= 0.3f; // Block giảm 70%
                PlayBlockHitAnimation();
            }

            // Defense reduction
            float defense = damageInfo.Type == DamageType.Slash 
                         || damageInfo.Type == DamageType.Pierce 
                         || damageInfo.Type == DamageType.Strike
                ? (data?.physicalDefense ?? 0f) 
                : (data?.magicDefense ?? 0f);
            
            damage *= 100f / (100f + defense);
            damage = Mathf.Max(damage, 1f);

            // Apply
            currentHP -= damage;

            var result = new DamageResult
            {
                FinalDamage = damage,
                WasCrit = damageInfo.IsCrit,
                DamageReduced = damageInfo.RawDamage - damage,
                KnockbackDirection = damageInfo.HitDirection,
                KnockbackForce = damageInfo.KnockbackForce
            };

            OnDamageTaken?.Invoke(result);

            // Hit reaction animation
            if (damage > 0)
                PlayHitReaction(damageInfo.IsHeavyAttack);

            // Death
            if (currentHP <= 0)
            {
                currentHP = 0;
                HandleDeath();
            }

            return result;
        }

        #endregion

        #region IDamageDealer

        public DamageInfo CalculateDamage(bool isHeavyAttack = false)
        {
            float baseDmg = data?.baseDamage ?? 10f;
            if (isHeavyAttack) baseDmg *= 1.5f;

            return new DamageInfo
            {
                RawDamage = baseDmg,
                Type = data?.damageType ?? DamageType.Strike,
                HitDirection = transform.forward,
                Source = this,
                IsHeavyAttack = isHeavyAttack,
                CanParry = true
            };
        }

        #endregion

        #region Animation Helpers

        protected virtual void PlayHitReaction(bool heavy)
        {
            if (animController == null) return;

            if (heavy)
                animController.PlayAnimation("Enemy_Hit_Heavy", AnimationPriority.Knockback);
            else
                animController.PlayAnimation("Enemy_Hit_Light", AnimationPriority.HitReaction);
        }

        protected virtual void PlayDodgeAnimation()
        {
            animController?.PlayAnimation("Enemy_Dodge", AnimationPriority.Skill);
        }

        protected virtual void PlayBlockHitAnimation()
        {
            animController?.PlayAnimation("Enemy_Block_Hit", AnimationPriority.Block);
        }

        protected virtual void PlayAttackAnimation(string attackName = "Enemy_Atk1")
        {
            var actionData = new AnimationActionData
            {
                AnimationStateName = attackName,
                StartupEnd = 0.2f,
                ActiveEnd = 0.6f,
                CanCancelStartup = false,
                CanCancelRecovery = false
            };
            animController?.PlayAction(actionData, AnimationPriority.NormalAttack);
        }

        protected virtual void HandleDeath()
        {
            animController?.ForcePlay("Enemy_Death");
            OnDeath?.Invoke();

            // Disable collider, etc.
            var collider = GetComponent<Collider>();
            if (collider != null) collider.enabled = false;

            // Destroy sau delay
            Destroy(gameObject, 5f);
        }

        #endregion

        #region State Helpers

        private ECombatState ConvertState()
        {
            return currentCombatState;
        }

        /// <summary>
        /// Check xem có thể attack không (cooldown ready)
        /// </summary>
        protected bool CanAttack()
        {
            return attackCooldownTimer <= 0f && IsAlive;
        }

        /// <summary>
        /// Thực hiện attack và reset cooldown
        /// </summary>
        protected void PerformAttack(IDamageable target, bool heavy = false)
        {
            if (!CanAttack()) return;

            PlayAttackAnimation(heavy ? "Enemy_Atk_Heavy" : "Enemy_Atk1");
            attackCooldownTimer = data?.attackCooldown ?? 1.5f;

            // Damage sẽ được apply qua HitboxManager (nếu có)
            // hoặc override method này trong subclass
        }

        #endregion

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (data == null) return;

            // Detection range
            Gizmos.color = new Color(1f, 1f, 0f, 0.1f);
            Gizmos.DrawWireSphere(transform.position, data.detectionRange);

            // Attack range
            Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, data.attackRange);

            // Lock-on point
            if (lockOnPoint != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(lockOnPoint.position, 0.2f);
            }
        }
#endif
    }
}
