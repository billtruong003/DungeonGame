// File: Core/Combat/PlayerCombatController.cs
// OBSOLETE — Use CombatStateMachine instead.
// This was the old monolithic combat controller. CombatStateMachine provides:
// - State pattern (cleaner), dodge, parry, guard break, riposte
// - Proper DamagePipeline integration
// - Uses HealthSystem instead of duplicate HP tracking
// - Uses PlayerInputHandler (superset of CombatInputHandler)
using System;
using UnityEngine;

namespace RPGModular
{
    [Obsolete("Use CombatStateMachine instead. This monolithic controller is kept for reference only.")]
    public class PlayerCombatController : MonoBehaviour, IDamageDealer, IDamageable
    {
        [Header("Dependencies")]
        [SerializeField] private CharacterStats stats;
        [SerializeField] private AnimationController animController;
        [SerializeField] private CombatLocomotion combatLocomotion;
        [SerializeField] private WeaponHandler weaponHandler;
        [SerializeField] private CombatInputHandler inputHandler;

        [Header("Health")]
        [SerializeField] private float currentHP;
        [SerializeField] private float currentStamina;

        [Header("Combo")]
        [SerializeField] private float comboResetTime = 1.0f;  // Reset combo sau bao lâu không đánh

        [Header("Block")]
        [SerializeField] private float blockStaminaCost = 10f;      // Stamina mỗi lần block
        [SerializeField] private float blockHeavyStaminaCost = 30f;  // Stamina block heavy attack

        // State
        private ECombatState combatState = ECombatState.Idle;
        private int currentComboIndex = 0;
        private float comboTimer;
        private DamagePipeline damagePipeline;

        // IDamageable
        public float CurrentHP => currentHP;
        public float MaxHP => stats.MaxHP;
        public bool IsAlive => currentHP > 0;
        public ECombatState CurrentCombatState => combatState;

        // Events
        public event Action<DamageResult> OnDamageTaken;
        public event Action OnDeath;
        public event Action<IDamageable, DamageResult> OnDamageDealt;

        private void Awake()
        {
            // Auto-find dependencies
            if (stats == null) stats = GetComponent<CharacterStats>();
            if (animController == null) animController = GetComponentInChildren<AnimationController>();
            if (combatLocomotion == null) combatLocomotion = GetComponent<CombatLocomotion>();
            if (weaponHandler == null) weaponHandler = GetComponent<WeaponHandler>();
            if (inputHandler == null) inputHandler = GetComponent<CombatInputHandler>();

            damagePipeline = new DamagePipeline();
        }

        private void Start()
        {
            currentHP = stats.MaxHP;
            currentStamina = stats.MaxStamina;

            // Listen animation events
            animController.OnPhaseChanged += OnAnimationPhaseChanged;
            animController.OnActionComplete += OnAnimationActionComplete;
        }

        private void OnDestroy()
        {
            if (animController != null)
            {
                animController.OnPhaseChanged -= OnAnimationPhaseChanged;
                animController.OnActionComplete -= OnAnimationActionComplete;
            }
        }

        private void Update()
        {
            if (!IsAlive) return;

            // Stamina regen
            RegenerateStamina();

            // Combo timer
            if (currentComboIndex > 0)
            {
                comboTimer -= Time.deltaTime;
                if (comboTimer <= 0f)
                    ResetCombo();
            }

            // Process input based on current state
            switch (combatState)
            {
                case ECombatState.Idle:
                case ECombatState.Combat:
                    HandleCombatInput();
                    break;
                case ECombatState.Attacking:
                    HandleAttackingState();
                    break;
                case ECombatState.Blocking:
                    HandleBlockingState();
                    break;
                case ECombatState.HitStun:
                case ECombatState.Knockback:
                    // Chờ animation xong
                    break;
            }

            // Lock-on toggle
            if (inputHandler.LockOnToggle)
            {
                inputHandler.ConsumeLockOnInput();
                ToggleLockOn();
            }

            // Movement (nếu không đang attacking/hitstun)
            if (combatState == ECombatState.Combat || combatState == ECombatState.Idle)
            {
                if (combatLocomotion.IsLockedOn)
                {
                    combatLocomotion.HandleCombatMovement(inputHandler.MoveInput,
                        stats.MoveSpeed);
                }
            }
        }

        #region Input Handling

        private void HandleCombatInput()
        {
            // Block (hold)
            if (inputHandler.BlockHeld && currentStamina > blockStaminaCost)
            {
                EnterBlock();
                return;
            }

            // Attack
            if (inputHandler.AttackInput)
            {
                inputHandler.ConsumeAttackInput();
                PerformNormalAttack(false);
                return;
            }

            // Heavy attack
            if (inputHandler.HeavyAttackInput)
            {
                inputHandler.ConsumeHeavyAttackInput();
                PerformNormalAttack(true);
            }
        }

        private void HandleAttackingState()
        {
            // Trong startup phase, có thể cancel bằng dodge
            // Trong recovery phase, check input buffer cho combo
            if (animController.CurrentPhase == AnimationPhase.Recovery 
                && animController.CanBeInterrupted)
            {
                if (inputHandler.AttackInput)
                {
                    inputHandler.ConsumeAttackInput();
                    PerformNormalAttack(false);
                }
            }
        }

        private void HandleBlockingState()
        {
            if (!inputHandler.BlockHeld || currentStamina <= 0)
            {
                ExitBlock();
            }
        }

        #endregion

        #region Combat Actions

        private void PerformNormalAttack(bool isHeavy)
        {
            var actionData = weaponHandler.GetNormalAttackAction(currentComboIndex);
            if (actionData == null) return;

            AnimationPriority priority = isHeavy ? AnimationPriority.Skill : AnimationPriority.NormalAttack;

            bool played = animController.PlayAction(actionData, priority, OnAnimationPhaseChanged);
            if (played)
            {
                combatState = ECombatState.Attacking;
                currentComboIndex = (currentComboIndex + 1) % weaponHandler.MaxComboCount;
                comboTimer = comboResetTime;
            }
        }

        private void EnterBlock()
        {
            if (combatState == ECombatState.Blocking) return;

            var weapon = weaponHandler.MainHandWeapon;
            var animSet = weapon?.AnimationSet ?? WeaponAnimationSet.CreateDefault(WeaponType.Unarmed);

            bool played = animController.PlayAnimation(animSet.BlockIdle, AnimationPriority.Block);
            if (played)
            {
                combatState = ECombatState.Blocking;
            }
        }

        private void ExitBlock()
        {
            combatState = combatLocomotion.IsLockedOn ? ECombatState.Combat : ECombatState.Idle;
            // AnimationController sẽ tự return về combat idle
        }

        private void ResetCombo()
        {
            currentComboIndex = 0;
            comboTimer = 0f;
        }

        #endregion

        #region IDamageDealer

        public DamageInfo CalculateDamage(bool isHeavyAttack = false)
        {
            var weapon = weaponHandler.MainHandWeapon;
            float weaponDmg = weapon?.BaseDamage ?? 5f; // Tay không = 5 base
            DamageType dmgType = weapon?.PrimaryDamageType ?? DamageType.Strike;

            float rawDamage;
            switch (dmgType)
            {
                case DamageType.Slash:
                case DamageType.Pierce:
                case DamageType.Strike:
                    rawDamage = stats.PhysicalAttack + weaponDmg;
                    break;
                default: // Magic
                    rawDamage = stats.MagicAttack + weaponDmg;
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

        #endregion

        #region IDamageable

        public DamageResult TakeDamage(DamageInfo damageInfo)
        {
            if (!IsAlive) return new DamageResult { FinalDamage = 0 };

            // Build context
            var context = new DamageContext
            {
                AttackerStats = (damageInfo.Source as MonoBehaviour)?.GetComponent<IStatProvider>(),
                DefenderStats = stats,
                DefenderCombatState = combatState,
                AttackerWeapon = null
            };

            // Run through pipeline
            DamageResult result = damagePipeline.Calculate(damageInfo, context);

            // Apply damage
            if (!result.WasDodged)
            {
                currentHP -= result.FinalDamage;

                // Block stamina cost
                if (result.WasBlocked)
                {
                    float staminaCost = damageInfo.IsHeavyAttack ? blockHeavyStaminaCost : blockStaminaCost;
                    currentStamina -= staminaCost;
                }

                // Knockback
                if (result.KnockbackForce > 0)
                {
                    combatLocomotion.ApplyKnockback(result.KnockbackDirection, result.KnockbackForce);
                    PlayHitReaction(true);
                }
                else if (!result.WasBlocked)
                {
                    PlayHitReaction(false);
                }

                // Death check
                if (currentHP <= 0)
                {
                    currentHP = 0;
                    Die();
                }
            }

            OnDamageTaken?.Invoke(result);
            return result;
        }

        private void PlayHitReaction(bool isHeavy)
        {
            var weapon = weaponHandler.MainHandWeapon;
            var animSet = weapon?.AnimationSet ?? WeaponAnimationSet.CreateDefault(WeaponType.Unarmed);

            if (isHeavy)
            {
                combatState = ECombatState.Knockback;
                animController.PlayAnimation(animSet.Knockback, AnimationPriority.Knockback);
            }
            else
            {
                combatState = ECombatState.HitStun;
                animController.PlayAnimation(animSet.HitLight, AnimationPriority.HitReaction);
            }
        }

        private void Die()
        {
            combatState = ECombatState.Dead;
            animController.ForcePlay("Death");
            OnDeath?.Invoke();
        }

        #endregion

        #region Animation Callbacks

        private void OnAnimationPhaseChanged(AnimationPhase phase)
        {
            // Khi vào Active phase → có thể enable hitbox ở đây
            // Khi ra khỏi Active phase → disable hitbox
        }

        private void OnAnimationActionComplete()
        {
            // Animation action xong → return về combat/idle
            if (combatState == ECombatState.Attacking || combatState == ECombatState.HitStun
                || combatState == ECombatState.Knockback)
            {
                combatState = combatLocomotion.IsLockedOn ? ECombatState.Combat : ECombatState.Idle;
            }
        }

        #endregion

        #region Stamina

        private void RegenerateStamina()
        {
            if (combatState == ECombatState.Blocking) return; // Không regen khi block
            
            float regenRate = 15f + stats.GetStat(StatType.VIT) * 0.5f;
            currentStamina = Mathf.Min(currentStamina + regenRate * Time.deltaTime, stats.MaxStamina);
        }

        #endregion

        #region Lock-On

        private void ToggleLockOn()
        {
            if (combatLocomotion.IsLockedOn)
            {
                combatLocomotion.ClearLockOn();
                combatState = ECombatState.Idle;
            }
            else
            {
                // Tìm target gần nhất
                Transform nearestTarget = FindNearestTarget();
                if (nearestTarget != null)
                {
                    combatLocomotion.SetLockOnTarget(nearestTarget);
                    combatState = ECombatState.Combat;
                }
            }
        }

        private Transform FindNearestTarget()
        {
            // Tìm tất cả IDamageable trong range
            float searchRadius = 15f;
            Collider[] hits = Physics.OverlapSphere(transform.position, searchRadius);
            
            Transform nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var hit in hits)
            {
                if (hit.transform == transform) continue;
                
                var damageable = hit.GetComponent<IDamageable>();
                if (damageable == null || !damageable.IsAlive) continue;

                var lockable = hit.GetComponent<ITargetLockable>();
                if (lockable == null || !lockable.CanBeLocked) continue;

                float dist = Vector3.Distance(transform.position, hit.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = lockable.LockOnPoint ?? hit.transform;
                }
            }

            return nearest;
        }

        #endregion

#if UNITY_EDITOR
        private void OnGUI()
        {
            // Debug HUD
            GUILayout.BeginArea(new Rect(10, 10, 250, 200));
            GUILayout.Label($"State: {combatState}");
            GUILayout.Label($"HP: {currentHP:F0}/{stats.MaxHP:F0}");
            GUILayout.Label($"Stamina: {currentStamina:F0}/{stats.MaxStamina:F0}");
            GUILayout.Label($"Weapon: {weaponHandler.CurrentWeaponType}");
            GUILayout.Label($"Combo: {currentComboIndex}/{weaponHandler.MaxComboCount}");
            GUILayout.Label($"Anim Priority: {animController.CurrentPriority}");
            GUILayout.Label($"Anim Phase: {animController.CurrentPhase}");
            GUILayout.Label($"Lock-On: {combatLocomotion.IsLockedOn}");
            GUILayout.EndArea();
        }
#endif
    }
}
