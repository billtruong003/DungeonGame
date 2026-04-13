using System;
using System.Collections.Generic;
using UnityEngine;
using BillVRCore.Hand;

namespace BillVRCore.Weapons
{
    public class MeleeWeapon : WeaponBase
    {
        [Header("Melee")]
        [SerializeField] private float minSwingSpeed = 1.5f;
        [SerializeField] private float damageVelocityScale = 10f;
        [SerializeField] private float maxDamageMultiplier = 3f;
        [SerializeField] private float hitCooldown = 0.3f;

        [Header("Blade Zone")]
        [SerializeField] private Collider bladeCollider;
        [SerializeField] private bool useTriggerZone = true;

        [Header("Impact")]
        [SerializeField] private float impactHapticAmplitude = 0.8f;
        [SerializeField] private float impactHapticDuration = 0.12f;
        [SerializeField] private AudioSource impactSound;

        public event Action<DamageEvent> OnMeleeHit;
        public float SwingSpeed => _swingSpeed;

        private struct HitRecord
        {
            public int colliderInstanceId;
            public float hitTime;
        }

        private readonly List<HitRecord> _recentHits = new(16);
        private Vector3 _prevPosition;
        private float _swingSpeed;

        protected override void Awake()
        {
            base.Awake();
            _prevPosition = transform.position;
            if (bladeCollider != null && useTriggerZone) bladeCollider.isTrigger = true;
        }

        private void FixedUpdate()
        {
            Vector3 currentPos = bladeCollider != null ? bladeCollider.transform.position : transform.position;
            _swingSpeed = (currentPos - _prevPosition).magnitude / Time.fixedDeltaTime;
            _prevPosition = currentPos;

            float expireTime = Time.time - hitCooldown * 2f;
            for (int i = _recentHits.Count - 1; i >= 0; i--)
            {
                if (_recentHits[i].hitTime < expireTime)
                    _recentHits.RemoveAt(i);
            }
        }

        protected override void OnFirePressed(VRHand hand) { }

        private void OnTriggerEnter(Collider other)
        {
            if (useTriggerZone) ProcessHit(other, _swingSpeed, other.ClosestPoint(transform.position));
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!useTriggerZone)
                ProcessHit(collision.collider, collision.relativeVelocity.magnitude,
                    collision.contactCount > 0 ? collision.GetContact(0).point : collision.collider.ClosestPoint(transform.position));
        }

        private void ProcessHit(Collider hitCol, float speed, Vector3 hitPoint)
        {
            if (!IsEquipped) return;
            if ((HitLayers & (1 << hitCol.gameObject.layer)) == 0) return;
            if (speed < minSwingSpeed) return;

            int colId = hitCol.GetInstanceID();
            for (int i = 0; i < _recentHits.Count; i++)
            {
                if (_recentHits[i].colliderInstanceId == colId && Time.time - _recentHits[i].hitTime < hitCooldown)
                    return;
            }

            _recentHits.Add(new HitRecord { colliderInstanceId = colId, hitTime = Time.time });

            float velocityMultiplier = Mathf.Clamp(speed / damageVelocityScale, 0.5f, maxDamageMultiplier);
            Vector3 hitDir = (hitPoint - transform.position).normalized;
            DealDamage(hitCol, hitPoint, hitDir, BaseDamage * velocityMultiplier, speed * Rb.mass, DamageType.Melee);

            if (PrimaryHand != null)
            {
                float normalizedSpeed = Mathf.InverseLerp(minSwingSpeed, minSwingSpeed * 4f, speed);
                PrimaryHand.Haptics.PlayHaptic(
                    Mathf.Lerp(impactHapticAmplitude * 0.5f, impactHapticAmplitude, normalizedSpeed),
                    impactHapticDuration);
            }

            if (impactSound != null) impactSound.Play();
        }

        protected override void OnDamageDealt(DamageEvent damage)
        {
            OnMeleeHit?.Invoke(damage);
        }

        public void SetMinSwingSpeed(float speed) => minSwingSpeed = speed;
        public void SetHitCooldown(float cd) => hitCooldown = cd;
    }
}
