using System;
using UnityEngine;
using BillInspector;

namespace RPGModular
{

    [CreateAssetMenu(fileName = "NewEnemy", menuName = "RPG/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        [BillTitle("Enemy Data")]
        [BillBoxGroup("Basic")]
        public string enemyName = "Enemy";
        [BillBoxGroup("Basic")]
        [BillSlider(1, 10000)] public float baseHP = 100f;
        [BillBoxGroup("Basic")]
        [BillSlider(1, 500)] public float baseDamage = 10f;
        [BillBoxGroup("Basic")]
        [BillSlider(0, 15), BillSuffix("m/s")] public float moveSpeed = 3f;
        [BillBoxGroup("Basic")]
        [BillSlider(0.5f, 10f), BillSuffix("m")] public float attackRange = 2f;
        [BillBoxGroup("Basic")]
        [BillSlider(1f, 50f), BillSuffix("m")] public float detectionRange = 10f;

        [BillBoxGroup("Stats")]
        [BillSlider(0, 200)] public float physicalDefense = 10f;
        [BillBoxGroup("Stats")]
        [BillSlider(0, 200)] public float magicDefense = 5f;
        [BillBoxGroup("Stats")]
        [BillSlider(0f, 0.5f)] public float dodgeChance = 0.05f;
        [BillBoxGroup("Stats")]
        [BillSlider(0f, 0.5f)] public float blockChance = 0f;

        [BillBoxGroup("Combat Behavior")]
        [BillSlider(0.3f, 5f), BillSuffix("s")] public float attackCooldown = 1.5f;
        [BillBoxGroup("Combat Behavior")]
        [BillSlider(0.5f, 3f)] public float attackSpeed = 1f;
        [BillBoxGroup("Combat Behavior")]
        public bool canBlock = false;
        [BillBoxGroup("Combat Behavior")]
        [BillShowIf("canBlock")] public bool canParry = false;
        [BillBoxGroup("Combat Behavior")]
        [BillShowIf("canBlock"), BillSlider(0.3f, 3f), BillSuffix("s")] public float blockDuration = 1f;

        [BillBoxGroup("Type")]
        [BillEnumToggleButtons] public EnemyTier tier = EnemyTier.Normal;
        [BillBoxGroup("Type")]
        [BillEnumToggleButtons] public DamageType damageType = DamageType.Strike;

        [BillBoxGroup("Rewards")]
        [BillSlider(0, 10000)] public int expReward = 50;
        [BillBoxGroup("Rewards")]
        [BillSlider(0, 10000)] public int goldReward = 10;
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
        [BillTitle("Enemy Base")]
        [BillBoxGroup("Config")]
        [BillRequired("EnemyData is required"), BillInlineEditor]
        [SerializeField] protected EnemyData data;

        [BillBoxGroup("Components")]
        [SerializeField] protected AnimationController animController;
        [BillBoxGroup("Components")]
        [SerializeField] protected Transform lockOnPoint;

        [BillBoxGroup("Runtime"), BillReadOnly]
        [SerializeField] protected float currentHP;

        protected ECombatState currentCombatState = ECombatState.Idle;
        protected DamagePipeline damagePipeline;
        protected float attackCooldownTimer;

        // Prevent double-death
        private bool isDead;

        public float CurrentHP => currentHP;
        public float MaxHP => data != null ? data.baseHP : 100f;
        public bool IsAlive => currentHP > 0 && !isDead;
        public ECombatState CurrentCombatState => currentCombatState;

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

        // ═══════════════════════════════════════════════════════
        // IDamageable — uses DamagePipeline properly
        // ═══════════════════════════════════════════════════════

        public DamageResult TakeDamage(DamageInfo damageInfo)
        {
            if (!IsAlive) return new DamageResult { FinalDamage = 0 };

            // Build context with actual stats for DamagePipeline
            var context = new DamageContext
            {
                AttackerStats = (damageInfo.Source as MonoBehaviour)?.GetComponent<IStatProvider>(),
                DefenderStats = null, // EnemyBase doesn't use CharacterStats, pipeline handles via inline
                DefenderCombatState = CurrentCombatState,
                AttackerWeapon = null,
            };

            // Override dodge/block via enemy data (since no IStatProvider on enemy)
            // Check dodge
            if (data != null && UnityEngine.Random.value < data.dodgeChance)
            {
                PlayDodgeAnimation();
                var dodgeResult = new DamageResult { WasDodged = true, FinalDamage = 0 };
                OnDamageTaken?.Invoke(dodgeResult);
                return dodgeResult;
            }

            // Check block
            bool isBlocking = data != null && data.canBlock && currentCombatState == ECombatState.Blocking;
            if (isBlocking && !damageInfo.IsUnblockable)
                context.DefenderCombatState = ECombatState.Blocking;

            // Run through pipeline (CritProcessor, BlockProcessor, DefenseProcessor, MinDamageProcessor)
            // DefenseProcessor needs DefenderStats — since enemy uses flat values, apply defense manually
            float damage = damageInfo.RawDamage;

            // Crit (uses attacker stats from pipeline)
            if (context.AttackerStats != null)
            {
                float critChance = context.AttackerStats.CritChance;
                if (UnityEngine.Random.value < critChance)
                {
                    context.WasCrit = true;
                    float critMult = context.AttackerStats.CritDamage;
                    damage *= critMult;
                    damageInfo.IsCrit = true;
                    damageInfo.CritMultiplier = critMult;
                }
            }

            // Block reduction
            if (isBlocking && !damageInfo.IsUnblockable)
            {
                context.WasBlocked = true;
                if (damageInfo.IsHeavyAttack)
                {
                    damage *= 0.6f;
                    context.KnockbackForce = 8f;
                    context.KnockbackDirection = damageInfo.HitDirection;
                }
                else
                {
                    damage *= 0.3f;
                }
                PlayBlockHitAnimation();
            }

            // Defense reduction
            float defense = damageInfo.Type == DamageType.Slash
                         || damageInfo.Type == DamageType.Pierce
                         || damageInfo.Type == DamageType.Strike
                ? (data?.physicalDefense ?? 0f)
                : (data?.magicDefense ?? 0f);
            damage *= 100f / (100f + defense);

            // Min damage
            damage = Mathf.Max(damage, 1f);

            currentHP -= damage;

            var result = new DamageResult
            {
                FinalDamage = damage,
                WasBlocked = context.WasBlocked,
                WasCrit = context.WasCrit,
                DamageReduced = damageInfo.RawDamage - damage,
                KnockbackDirection = damageInfo.HitDirection,
                KnockbackForce = damageInfo.KnockbackForce
            };

            OnDamageTaken?.Invoke(result);

            if (damage > 0 && !context.WasBlocked)
                PlayHitReaction(damageInfo.IsHeavyAttack);

            if (currentHP <= 0)
            {
                currentHP = 0;
                HandleDeath();
            }

            return result;
        }

        // ═══════════════════════════════════════════════════════
        // IDamageDealer
        // ═══════════════════════════════════════════════════════

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

        /// <summary>
        /// Notify that this enemy dealt damage. Called by HitboxManager after hit confirmed.
        /// </summary>
        public void NotifyDamageDealt(IDamageable target, DamageResult result)
        {
            OnDamageDealt?.Invoke(target, result);
        }

        // ═══════════════════════════════════════════════════════
        // Attack — now actually deals damage
        // ═══════════════════════════════════════════════════════

        protected bool CanAttack()
        {
            return attackCooldownTimer <= 0f && IsAlive;
        }

        /// <summary>
        /// Perform attack via hitbox (animation triggers hitbox collider).
        /// If no hitbox system is set up, falls back to direct damage.
        /// </summary>
        protected void PerformAttack(IDamageable target, bool heavy = false)
        {
            if (!CanAttack()) return;

            PlayAttackAnimation(heavy ? "Enemy_Atk_Heavy" : "Enemy_Atk1");
            attackCooldownTimer = data?.attackCooldown ?? 1.5f;

            // Direct damage fallback — if no HitboxManager, apply damage directly
            var hitbox = GetComponentInChildren<HitboxManager>();
            if (hitbox == null && target != null)
            {
                DamageInfo dmgInfo = CalculateDamage(heavy);
                if (target is MonoBehaviour mb)
                    dmgInfo.HitDirection = (mb.transform.position - transform.position).normalized;

                DamageResult result = target.TakeDamage(dmgInfo);
                OnDamageDealt?.Invoke(target, result);
            }
            // If HitboxManager exists, hitbox collision will handle damage via OnTriggerEnter
        }

        // ═══════════════════════════════════════════════════════
        // Animations
        // ═══════════════════════════════════════════════════════

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
            if (isDead) return;
            isDead = true;

            currentCombatState = ECombatState.Dead;
            animController?.ForcePlay("Enemy_Death");
            OnDeath?.Invoke();

            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            Destroy(gameObject, 5f);
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
