#if VRCORE_HAS_RAGDOLL
using FIMSpace.FProceduralAnimation;
#endif
using System;
using UnityEngine;
using BillVRCore.Weapons;

namespace BillVRCore.Ragdoll
{
    public class RagdollBridge : MonoBehaviour, IDamageable
    {
        [Header("Health")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float fallThreshold = 40f;

        [Header("Impact")]
        [SerializeField] private float impactForceMultiplier = 1f;
        [SerializeField] private float maxImpactForce = 50f;

        [Header("Recovery")]
        [SerializeField] private bool canGetUp = true;
        [SerializeField] private float getUpDelay = 3f;

        [Header("Fallback")]
        [SerializeField] private bool useFallbackRagdoll = true;

        public float CurrentHealth { get; private set; }
        public float MaxHealth => maxHealth;
        public float HealthPercent => maxHealth > 0 ? CurrentHealth / maxHealth : 0f;
        public bool IsAlive => CurrentHealth > 0f;
        public bool IsFallen { get; private set; }
        public bool IsDead => CurrentHealth <= 0f;

        public Action<DamageEvent> OnDamageReceived;
        public Action OnFall;
        public Action OnGetUp;
        public Action OnDeath;
        public Action<float, float> OnHealthChanged;

        private Animator _animator;
        private float _fallTimer;

#if VRCORE_HAS_RAGDOLL
        private RagdollAnimator2 _ra2;
#endif

        private void Awake()
        {
            CurrentHealth = maxHealth;
            _animator = GetComponentInChildren<Animator>();

#if VRCORE_HAS_RAGDOLL
            _ra2 = GetComponent<RagdollAnimator2>();
#endif
        }

        private void Update()
        {
            if (!IsFallen || !canGetUp || !IsAlive) return;
            _fallTimer -= Time.deltaTime;
            if (_fallTimer <= 0f) GetUp();
        }

        public void TakeDamage(DamageEvent damage)
        {
            float prev = CurrentHealth;
            CurrentHealth = Mathf.Max(CurrentHealth - damage.amount, 0f);
            OnHealthChanged?.Invoke(prev, CurrentHealth);

            ApplyPhysicalImpact(damage);
            OnDamageReceived?.Invoke(damage);

            if (CurrentHealth <= 0f) { Die(damage); return; }
            if (damage.amount >= fallThreshold && !IsFallen) Fall(damage);
        }

        public void TakeDamage(float amount, Vector3 direction)
        {
            TakeDamage(DamageEvent.Create(amount, DamageType.Melee, transform.position, direction, amount));
        }

        public void TakeDamage(float amount, Vector3 point, Vector3 direction, float force)
        {
            TakeDamage(DamageEvent.Create(amount, DamageType.Melee, point, direction, force));
        }

        public void Heal(float amount)
        {
            float prev = CurrentHealth;
            CurrentHealth = Mathf.Min(CurrentHealth + amount, maxHealth);
            OnHealthChanged?.Invoke(prev, CurrentHealth);
        }

        public void FullHeal()
        {
            float prev = CurrentHealth;
            CurrentHealth = maxHealth;
            IsFallen = false;
            OnHealthChanged?.Invoke(prev, CurrentHealth);
        }

        public void Fall() => Fall(default);

        public void Fall(DamageEvent cause)
        {
            IsFallen = true;
            _fallTimer = getUpDelay;

#if VRCORE_HAS_RAGDOLL
            if (_ra2 != null)
            {
                _ra2.RA2Event_SwitchToFall();
                OnFall?.Invoke();
                return;
            }
#endif
            ActivateFallbackRagdoll();
            OnFall?.Invoke();
        }

        public void GetUp()
        {
            IsFallen = false;

#if VRCORE_HAS_RAGDOLL
            if (_ra2 != null)
            {
                _ra2.RA2Event_TransitionStand(0.5f);
                OnGetUp?.Invoke();
                return;
            }
#endif
            DeactivateFallbackRagdoll();
            OnGetUp?.Invoke();
        }

        public void Die() => Die(default);

        public void ForceFall(float duration)
        {
            IsFallen = true;
            _fallTimer = duration;
            Fall(default);
        }

        public void SetCanGetUp(bool value) => canGetUp = value;
        public void SetMaxHealth(float value) { maxHealth = value; CurrentHealth = Mathf.Min(CurrentHealth, maxHealth); }
        public void SetFallThreshold(float value) => fallThreshold = value;
        public void SetGetUpDelay(float delay) => getUpDelay = delay;

        public void ResetHealth()
        {
            CurrentHealth = maxHealth;
            IsFallen = false;
        }

        private void Die(DamageEvent cause)
        {
            IsFallen = true;

#if VRCORE_HAS_RAGDOLL
            if (_ra2 != null)
            {
                _ra2.RA2Event_SwitchToSleep();
                OnDeath?.Invoke();
                return;
            }
#endif
            ActivateFallbackRagdoll();
            OnDeath?.Invoke();
        }

        private void ApplyPhysicalImpact(DamageEvent damage)
        {
            float force = Mathf.Min(damage.force * impactForceMultiplier, maxImpactForce);
            Vector3 impulse = damage.direction * force;

#if VRCORE_HAS_RAGDOLL
            if (_ra2 != null)
            {
                _ra2.RA2Event_AddFullImpact(impulse);
                return;
            }
#endif

            if (damage.hitCollider != null)
            {
                var rb = damage.hitCollider.GetComponentInParent<Rigidbody>();
                if (rb != null && !rb.isKinematic)
                    rb.AddForce(impulse, ForceMode.Impulse);
            }
        }

        private void ActivateFallbackRagdoll()
        {
            if (!useFallbackRagdoll) return;
            if (_animator != null) _animator.enabled = false;

            foreach (var rb in GetComponentsInChildren<Rigidbody>())
            {
                if (rb.gameObject == gameObject) continue;
                rb.isKinematic = false;
                rb.useGravity = true;
            }
        }

        private void DeactivateFallbackRagdoll()
        {
            if (!useFallbackRagdoll) return;
            if (_animator != null) _animator.enabled = true;

            foreach (var rb in GetComponentsInChildren<Rigidbody>())
            {
                if (rb.gameObject == gameObject) continue;
                rb.isKinematic = true;
            }
        }
    }
}