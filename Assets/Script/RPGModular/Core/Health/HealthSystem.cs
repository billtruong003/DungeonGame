using System;
using UnityEngine;
using BillInspector;

namespace RPGModular
{

    public enum ResourceType
    {
        HP,
        Mana,
        Stamina,
        Chi
    }

    public class HealthSystem : MonoBehaviour
    {
        [BillTitle("Health System", "HP / Mana / Stamina with regen")]
        [BillBoxGroup("Dependencies")]
        [BillRequired]
        [SerializeField] private CharacterStats stats;

        [BillFoldoutGroup("Regen Rates")]
        [BillSlider(0, 20), BillSuffix("/s")] [SerializeField] private float hpRegenBase = 0f;
        [BillFoldoutGroup("Regen Rates")]
        [BillSlider(0, 20), BillSuffix("/s")] [SerializeField] private float manaRegenBase = 3f;
        [BillFoldoutGroup("Regen Rates")]
        [BillSlider(0, 30), BillSuffix("/s")] [SerializeField] private float staminaRegenBase = 15f;

        [BillFoldoutGroup("Regen Control")]
        [BillSlider(0, 5), BillSuffix("s")] [SerializeField] private float regenDelayAfterUse = 1.5f;
        [BillFoldoutGroup("Regen Control")]
        [BillSlider(0, 1), BillLabelText("Stamina Regen In Combat %")] [SerializeField] private float staminaRegenInCombat = 0.6f;

        [BillFoldoutGroup("Chi Gauge")]
        [BillSlider(0, 10), BillSuffix("/s")] [SerializeField] private float chiDecayRate = 5f;
        [BillFoldoutGroup("Chi Gauge")]
        [BillSlider(0, 10), BillSuffix("s")] [SerializeField] private float chiDecayDelay = 5f;

        private float currentHP;
        private float currentMana;
        private float currentStamina;
        private float currentChi;
        private float lastCombatTime;
        private float chiPerVIT = 5f;

        private float hpRegenDelayTimer;
        private float manaRegenDelayTimer;
        private float staminaRegenDelayTimer;

        private bool hpRegenPaused;
        private bool manaRegenPaused;
        private bool staminaRegenPaused;

        private bool isInCombat;
        private bool isDead;

        public float CurrentHP => currentHP;
        public float CurrentMana => currentMana;
        public float CurrentStamina => currentStamina;
        public float MaxHP => stats != null ? stats.MaxHP : 100f;
        public float MaxMana => stats != null ? stats.MaxMana : 50f;
        public float MaxStamina => stats != null ? stats.MaxStamina : 100f;

        public float CurrentChi => currentChi;
        public float MaxChi => 100f + (stats != null ? stats.GetStat(StatType.VIT) * chiPerVIT : 0f);
        public float ChiPercent => MaxChi > 0 ? currentChi / MaxChi : 0f;

        public float HPPercent => MaxHP > 0 ? currentHP / MaxHP : 0f;
        public float ManaPercent => MaxMana > 0 ? currentMana / MaxMana : 0f;
        public float StaminaPercent => MaxStamina > 0 ? currentStamina / MaxStamina : 0f;

        public bool IsAlive => currentHP > 0 && !isDead;
        public bool HasMana(float amount) => currentMana >= amount;
        public bool HasStamina(float amount) => currentStamina >= amount;
        public bool HasChi(float amount) => currentChi >= amount;

        public event Action<ResourceType, float, float> OnResourceChanged;
        public event Action<float> OnDamageTaken;
        public event Action<float> OnHealReceived;
        public event Action OnDeath;
        public event Action OnRevive;

        private void Awake()
        {
            if (stats == null) stats = GetComponent<CharacterStats>();
        }

        private void Start()
        {
            InitializeToFull();
        }

        public void InitializeToFull()
        {
            currentHP = MaxHP;
            currentMana = MaxMana;
            currentStamina = MaxStamina;
            currentChi = 0f; // Chi starts at 0, built through combat
            isDead = false;
        }

        private void Update()
        {
            if (!IsAlive) return;

            hpRegenDelayTimer -= Time.deltaTime;
            manaRegenDelayTimer -= Time.deltaTime;
            staminaRegenDelayTimer -= Time.deltaTime;

            if (!hpRegenPaused && hpRegenDelayTimer <= 0f && currentHP < MaxHP)
            {
                float hpRegen = hpRegenBase + stats.GetStat(StatType.VIT) * 0.1f;
                ModifyResource(ResourceType.HP, hpRegen * Time.deltaTime);
            }

            if (!manaRegenPaused && manaRegenDelayTimer <= 0f && currentMana < MaxMana)
            {
                float manaRegen = manaRegenBase + stats.GetStat(StatType.INT) * 0.3f;
                ModifyResource(ResourceType.Mana, manaRegen * Time.deltaTime);
            }

            if (!staminaRegenPaused && staminaRegenDelayTimer <= 0f && currentStamina < MaxStamina)
            {
                float staminaRegen = staminaRegenBase + stats.GetStat(StatType.VIT) * 0.5f;
                if (isInCombat) staminaRegen *= staminaRegenInCombat;
                ModifyResource(ResourceType.Stamina, staminaRegen * Time.deltaTime);
            }

            // Chi decay when out of combat
            if (currentChi > 0f && Time.time - lastCombatTime > chiDecayDelay)
            {
                ModifyResource(ResourceType.Chi, -chiDecayRate * Time.deltaTime);
            }
        }

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

                case ResourceType.Chi:
                    old = currentChi;
                    max = MaxChi;
                    currentChi = Mathf.Clamp(currentChi + amount, 0f, max);
                    if (amount > 0) lastCombatTime = Time.time; // Chi gain = combat activity
                    if (Math.Abs(old - currentChi) > 0.01f)
                        OnResourceChanged?.Invoke(type, old, currentChi);
                    break;
            }
        }

        public bool TryConsumeStamina(float amount)
        {
            if (currentStamina < amount) return false;
            ModifyResource(ResourceType.Stamina, -amount);
            return true;
        }

        public bool TryConsumeMana(float amount)
        {
            if (currentMana < amount) return false;
            ModifyResource(ResourceType.Mana, -amount);
            return true;
        }

        public bool TryConsumeChi(float amount)
        {
            if (currentChi < amount) return false;
            ModifyResource(ResourceType.Chi, -amount);
            return true;
        }

        public void ModifyChi(float amount)
        {
            ModifyResource(ResourceType.Chi, amount);
        }

        public void NotifyCombatActivity()
        {
            lastCombatTime = Time.time;
        }

        public float ApplyDamage(float finalDamage)
        {
            // Death guard — prevent double-death from simultaneous hits
            if (!IsAlive || finalDamage <= 0) return 0f;

            float actualDamage = Mathf.Min(finalDamage, currentHP);
            ModifyResource(ResourceType.HP, -actualDamage);
            hpRegenDelayTimer = regenDelayAfterUse * 2f;

            OnDamageTaken?.Invoke(actualDamage);

            if (currentHP <= 0 && !isDead)
            {
                isDead = true;
                OnDeath?.Invoke();
            }

            return actualDamage;
        }

        public float Heal(float amount)
        {
            if (!IsAlive || amount <= 0) return 0f;

            float actualHeal = Mathf.Min(amount, MaxHP - currentHP);
            ModifyResource(ResourceType.HP, actualHeal);
            OnHealReceived?.Invoke(actualHeal);
            return actualHeal;
        }

        public void Revive(float hpPercent = 0.3f)
        {
            if (IsAlive) return;
            isDead = false;
            currentHP = MaxHP * Mathf.Clamp01(hpPercent);
            currentMana = MaxMana * 0.5f;
            currentStamina = MaxStamina;
            OnRevive?.Invoke();
        }

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

    }
}
