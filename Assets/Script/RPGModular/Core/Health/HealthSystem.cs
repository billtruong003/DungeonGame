// File: Core/Health/HealthSystem.cs
// Quản lý HP, Mana, Stamina tách riêng khỏi combat controller
// Dùng chung cho Player, Enemy, NPC — bất kỳ entity nào có "máu"
// Reusable: attach lên bất kỳ GameObject nào có IStatProvider
using System;
using UnityEngine;

namespace RPGModular
{
    /// <summary>
    /// Loại resource (để dùng chung API cho HP/Mana/Stamina)
    /// </summary>
    public enum ResourceType
    {
        HP,
        Mana,
        Stamina
    }

    public class HealthSystem : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private CharacterStats stats;

        [Header("Regen Rates (per second, base)")]
        [SerializeField] private float hpRegenBase = 0f;         // Thường không tự regen
        [SerializeField] private float manaRegenBase = 3f;
        [SerializeField] private float staminaRegenBase = 15f;

        [Header("Regen Control")]
        [SerializeField] private float regenDelayAfterUse = 1.5f;  // Delay trước khi regen lại
        [SerializeField] private float staminaRegenInCombat = 0.6f; // Multiplier regen stamina khi combat

        // Current values
        private float currentHP;
        private float currentMana;
        private float currentStamina;

        // Regen delay timers
        private float hpRegenDelayTimer;
        private float manaRegenDelayTimer;
        private float staminaRegenDelayTimer;

        // Regen pause flags (vd: block tắt stamina regen)
        private bool hpRegenPaused;
        private bool manaRegenPaused;
        private bool staminaRegenPaused;

        // Combat state reference
        private bool isInCombat;

        // === Properties ===
        public float CurrentHP => currentHP;
        public float CurrentMana => currentMana;
        public float CurrentStamina => currentStamina;
        public float MaxHP => stats != null ? stats.MaxHP : 100f;
        public float MaxMana => stats != null ? stats.MaxMana : 50f;
        public float MaxStamina => stats != null ? stats.MaxStamina : 100f;

        public float HPPercent => MaxHP > 0 ? currentHP / MaxHP : 0f;
        public float ManaPercent => MaxMana > 0 ? currentMana / MaxMana : 0f;
        public float StaminaPercent => MaxStamina > 0 ? currentStamina / MaxStamina : 0f;

        public bool IsAlive => currentHP > 0;
        public bool HasMana(float amount) => currentMana >= amount;
        public bool HasStamina(float amount) => currentStamina >= amount;

        // === Events ===
        public event Action<ResourceType, float, float> OnResourceChanged; // type, old, new
        public event Action<float> OnDamageTaken;   // actual damage amount
        public event Action<float> OnHealReceived;  // heal amount
        public event Action OnDeath;
        public event Action OnRevive;

        #region Initialization

        private void Awake()
        {
            if (stats == null) stats = GetComponent<CharacterStats>();
        }

        private void Start()
        {
            InitializeToFull();
        }

        /// <summary>
        /// Set tất cả resource về max. Gọi khi spawn, revive, rest.
        /// </summary>
        public void InitializeToFull()
        {
            currentHP = MaxHP;
            currentMana = MaxMana;
            currentStamina = MaxStamina;
        }

        #endregion

        #region Update - Regen

        private void Update()
        {
            if (!IsAlive) return;

            // Tick regen delays
            hpRegenDelayTimer -= Time.deltaTime;
            manaRegenDelayTimer -= Time.deltaTime;
            staminaRegenDelayTimer -= Time.deltaTime;

            // HP regen
            if (!hpRegenPaused && hpRegenDelayTimer <= 0f && currentHP < MaxHP)
            {
                float hpRegen = hpRegenBase + stats.GetStat(StatType.VIT) * 0.1f;
                ModifyResource(ResourceType.HP, hpRegen * Time.deltaTime);
            }

            // Mana regen
            if (!manaRegenPaused && manaRegenDelayTimer <= 0f && currentMana < MaxMana)
            {
                float manaRegen = manaRegenBase + stats.GetStat(StatType.INT) * 0.3f;
                ModifyResource(ResourceType.Mana, manaRegen * Time.deltaTime);
            }

            // Stamina regen
            if (!staminaRegenPaused && staminaRegenDelayTimer <= 0f && currentStamina < MaxStamina)
            {
                float staminaRegen = staminaRegenBase + stats.GetStat(StatType.VIT) * 0.5f;
                if (isInCombat) staminaRegen *= staminaRegenInCombat;
                ModifyResource(ResourceType.Stamina, staminaRegen * Time.deltaTime);
            }
        }

        #endregion

        #region Resource Modification

        /// <summary>
        /// Modify resource trực tiếp (positive = thêm, negative = trừ).
        /// Dùng cho regen, buff, hoặc cost.
        /// </summary>
        public void ModifyResource(ResourceType type, float amount)
        {
            float old, max;
            switch (type)
            {
                case ResourceType.HP:
                    old = currentHP;
                    max = MaxHP;
                    currentHP = Mathf.Clamp(currentHP + amount, 0f, max);
                    if (Math.Abs(old - currentHP) > 0.01f)
                        OnResourceChanged?.Invoke(type, old, currentHP);
                    break;

                case ResourceType.Mana:
                    old = currentMana;
                    max = MaxMana;
                    currentMana = Mathf.Clamp(currentMana + amount, 0f, max);
                    if (amount < 0) manaRegenDelayTimer = regenDelayAfterUse;
                    if (Math.Abs(old - currentMana) > 0.01f)
                        OnResourceChanged?.Invoke(type, old, currentMana);
                    break;

                case ResourceType.Stamina:
                    old = currentStamina;
                    max = MaxStamina;
                    currentStamina = Mathf.Clamp(currentStamina + amount, 0f, max);
                    if (amount < 0) staminaRegenDelayTimer = regenDelayAfterUse;
                    if (Math.Abs(old - currentStamina) > 0.01f)
                        OnResourceChanged?.Invoke(type, old, currentStamina);
                    break;
            }
        }

        /// <summary>
        /// Consume stamina. Return true nếu đủ.
        /// </summary>
        public bool TryConsumeStamina(float amount)
        {
            if (currentStamina < amount) return false;
            ModifyResource(ResourceType.Stamina, -amount);
            return true;
        }

        /// <summary>
        /// Consume mana. Return true nếu đủ.
        /// </summary>
        public bool TryConsumeMana(float amount)
        {
            if (currentMana < amount) return false;
            ModifyResource(ResourceType.Mana, -amount);
            return true;
        }

        #endregion

        #region Damage & Heal

        /// <summary>
        /// Nhận damage (đã tính toán xong từ DamagePipeline).
        /// Return actual damage dealt.
        /// </summary>
        public float ApplyDamage(float finalDamage)
        {
            if (!IsAlive || finalDamage <= 0) return 0f;

            float actualDamage = Mathf.Min(finalDamage, currentHP);
            ModifyResource(ResourceType.HP, -actualDamage);
            hpRegenDelayTimer = regenDelayAfterUse * 2f; // Longer delay after taking damage

            OnDamageTaken?.Invoke(actualDamage);

            if (currentHP <= 0)
            {
                OnDeath?.Invoke();
            }

            return actualDamage;
        }

        /// <summary>
        /// Heal HP.
        /// </summary>
        public float Heal(float amount)
        {
            if (!IsAlive || amount <= 0) return 0f;

            float actualHeal = Mathf.Min(amount, MaxHP - currentHP);
            ModifyResource(ResourceType.HP, actualHeal);
            OnHealReceived?.Invoke(actualHeal);
            return actualHeal;
        }

        /// <summary>
        /// Revive với % HP.
        /// </summary>
        public void Revive(float hpPercent = 0.3f)
        {
            if (IsAlive) return;
            currentHP = MaxHP * Mathf.Clamp01(hpPercent);
            currentMana = MaxMana * 0.5f;
            currentStamina = MaxStamina;
            OnRevive?.Invoke();
        }

        #endregion

        #region Regen Control

        public void SetCombatMode(bool combat) => isInCombat = combat;

        public void PauseRegen(ResourceType type, bool pause)
        {
            switch (type)
            {
                case ResourceType.HP: hpRegenPaused = pause; break;
                case ResourceType.Mana: manaRegenPaused = pause; break;
                case ResourceType.Stamina: staminaRegenPaused = pause; break;
            }
        }

        #endregion
    }
}
