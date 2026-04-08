using System;
using UnityEngine;

namespace RPGModular
{

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
        public float blockChance = 0f;

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

    public class EnemyBase : MonoBehaviour, IDamageable, ITargetLockable, IDamageDealer
    {
        [Header("Config")]
        [SerializeField] protected EnemyData data;

        [Header("Components")]
        [SerializeField] protected AnimationController animController;
        [SerializeField] protected Transform lockOnPoint;

        protected float currentHP;
        protected ECombatState currentCombatState = ECombatState.Idle;
        protected DamagePipeline damagePipeline;
        protected float attackCooldownTimer;

        public float CurrentHP => currentHP;
        public float MaxHP => data != null ? data.baseHP : 100f;
        public bool IsAlive => currentHP > 0;
        public ECombatState CurrentCombatState => ConvertState();

        public Transform LockOnPoint => lockOnPoint ?? transform;
        public bool CanBeLocked => IsAlive;

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

        public DamageResult TakeDamage(DamageInfo damageInfo)
        {
            if (!IsAlive) return new DamageResult { FinalDamage = 0 };

            var context = new DamageContext
            {
                AttackerStats = null,
                DefenderStats = null,
                DefenderCombatState = CurrentCombatState,
                AttackerWeapon = null,

            };

            float damage = damageInfo.RawDamage;

            if (data != null && UnityEngine.Random.value < data.dodgeChance)
            {
                PlayDodgeAnimation();
                var dodgeResult = new DamageResult { WasDodged = true, FinalDamage = 0 };
                OnDamageTaken?.Invoke(dodgeResult);
                return dodgeResult;
            }

            if (data != null && data.canBlock && currentCombatState == ECombatState.Blocking)
            {
                damage *= 0.3f;
                PlayBlockHitAnimation();
            }

            float defense = damageInfo.Type == DamageType.Slash
                         || damageInfo.Type == DamageType.Pierce
                         || damageInfo.Type == DamageType.Strike
                ? (data?.physicalDefense ?? 0f)
                : (data?.magicDefense ?? 0f);

            damage *= 100f / (100f + defense);
            damage = Mathf.Max(damage, 1f);

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

            if (damage > 0)
                PlayHitReaction(damageInfo.IsHeavyAttack);

            if (currentHP <= 0)
            {
                currentHP = 0;
                HandleDeath();
            }

            return result;
        }

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

            var collider = GetComponent<Collider>();
            if (collider != null) collider.enabled = false;

            Destroy(gameObject, 5f);
        }

        private ECombatState ConvertState()
        {
            return currentCombatState;
        }

        protected bool CanAttack()
        {
            return attackCooldownTimer <= 0f && IsAlive;
        }

        protected void PerformAttack(IDamageable target, bool heavy = false)
        {
            if (!CanAttack()) return;

            PlayAttackAnimation(heavy ? "Enemy_Atk_Heavy" : "Enemy_Atk1");
            attackCooldownTimer = data?.attackCooldown ?? 1.5f;

        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (data == null) return;

            Gizmos.color = new Color(1f, 1f, 0f, 0.1f);
            Gizmos.DrawWireSphere(transform.position, data.detectionRange);

            Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, data.attackRange);

            if (lockOnPoint != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(lockOnPoint.position, 0.2f);
            }
        }
#endif
    }
}
