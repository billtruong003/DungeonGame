using System;
using UnityEngine;
using BillInspector;

namespace RPGModular
{
    public class AutoAttackSystem : MonoBehaviour
    {
        [BillTitle("Auto Attack System")]
        [BillBoxGroup("Dependencies")]
        [SerializeField] private WeaponHandler weaponHandler;
        [BillBoxGroup("Dependencies")]
        [SerializeField] private LockOnSystem lockOn;
        [BillBoxGroup("Dependencies")]
        [SerializeField] private CharacterStats stats;
        [BillBoxGroup("Dependencies")]
        [SerializeField] private AnimationController animController;

        [BillBoxGroup("Auto Attack")]
        [SerializeField] private bool autoAttackEnabled = true;
        [BillBoxGroup("Auto Attack")]
        [BillSlider(0f, 3f), BillSuffix("m")]
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

            // MP recovery per auto-attack hit
            if (Game.Health != null)
            {
                float mpRecovery = 50f + (Game.Stats?.GetStat(StatType.LUK) ?? 0f) * 2f;
                Game.Health.ModifyResource(ResourceType.Mana, mpRecovery);
            }

            // Chi gain per hit (+5)
            Game.Health?.ModifyChi(5f);
            Game.Health?.NotifyCombatActivity();

            // Focus gain (Katana)
            Game.Focus?.OnAutoAttackHit();

            // Combo tracker
            Game.Combo?.OnAutoAttackHit();

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
