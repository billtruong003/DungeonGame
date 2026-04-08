using System;
using UnityEngine;

namespace RPGModular
{
    public class AutoAttackSystem : MonoBehaviour
    {
        [SerializeField] private WeaponHandler weaponHandler;
        [SerializeField] private LockOnSystem lockOn;
        [SerializeField] private CharacterStats stats;
        [SerializeField] private AnimationController animController;

        [Header("Auto Attack")]
        [SerializeField] private bool autoAttackEnabled = true;
        [SerializeField] private float attackRangeBuffer = 0.5f;

        private float attackCooldownTimer;
        private int currentComboIndex;
        private float comboResetTimer;
        private float comboResetTime = 1.2f;

        public bool AutoAttackEnabled
        {
            get => autoAttackEnabled;
            set => autoAttackEnabled = value;
        }

        public bool IsAutoAttacking { get; private set; }
        public int CurrentComboIndex => currentComboIndex;

        public event Action<AnimationActionData, int> OnAutoAttack;
        public event Action OnComboReset;

        private void Awake()
        {
            if (weaponHandler == null) weaponHandler = GetComponent<WeaponHandler>();
            if (lockOn == null) lockOn = GetComponent<LockOnSystem>();
            if (stats == null) stats = GetComponent<CharacterStats>();
            if (animController == null) animController = GetComponentInChildren<AnimationController>();
        }

        private void Update()
        {
            attackCooldownTimer -= Time.deltaTime;

            if (currentComboIndex > 0)
            {
                comboResetTimer -= Time.deltaTime;
                if (comboResetTimer <= 0f)
                    ResetCombo();
            }
        }

        public bool TryAutoAttack()
        {
            if (!autoAttackEnabled) return false;
            if (!lockOn.IsLockedOn) return false;
            if (attackCooldownTimer > 0f) return false;
            if (animController.CurrentPhase != AnimationPhase.Done) return false;

            float attackRange = weaponHandler.MainHandWeapon?.AttackRange ?? 2f;
            float distance = lockOn.DistanceToTarget;

            if (distance > attackRange + attackRangeBuffer) return false;

            return PerformAutoAttack();
        }

        private bool PerformAutoAttack()
        {
            AnimationActionData actionData = weaponHandler.GetNormalAttackAction(currentComboIndex);
            if (actionData == null) return false;

            bool played = animController.PlayAction(actionData, AnimationPriority.NormalAttack);
            if (!played) return false;

            IsAutoAttacking = true;
            float baseSpeed = weaponHandler.MainHandWeapon?.AttackSpeedModifier ?? 1f;
            float atkSpeed = stats != null ? stats.AttackSpeed : 1f;
            float totalSpeed = baseSpeed * atkSpeed;

            attackCooldownTimer = (1f / totalSpeed) * 0.3f;

            int previousIndex = currentComboIndex;
            currentComboIndex = (currentComboIndex + 1) % weaponHandler.MaxComboCount;
            comboResetTimer = comboResetTime;

            OnAutoAttack?.Invoke(actionData, previousIndex);
            return true;
        }

        public void ResetCombo()
        {
            currentComboIndex = 0;
            comboResetTimer = 0f;
            IsAutoAttacking = false;
            OnComboReset?.Invoke();
        }

        public void InterruptAutoAttack()
        {
            IsAutoAttacking = false;
            attackCooldownTimer = 0.1f;
        }

        public bool IsInAttackRange()
        {
            if (!lockOn.IsLockedOn) return false;
            float range = weaponHandler.MainHandWeapon?.AttackRange ?? 2f;
            return lockOn.DistanceToTarget <= range + attackRangeBuffer;
        }
    }
}
