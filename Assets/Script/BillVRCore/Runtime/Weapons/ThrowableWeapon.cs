using System;
using UnityEngine;
using BillVRCore.Hand;

namespace BillVRCore.Weapons
{
    public class ThrowableWeapon : WeaponBase
    {
        [Header("Throw")]
        [SerializeField] private float minThrowSpeed = 2f;
        [SerializeField] private float throwDamageScale = 5f;

        [Header("Impact Behavior")]
        [SerializeField] private ImpactMode impactMode = ImpactMode.Bounce;
        [SerializeField] private float stickDepth = 0.02f;

        [Header("Explosion (optional)")]
        [SerializeField] private bool explodeOnImpact;
        [SerializeField] private float explosionRadius = 3f;
        [SerializeField] private float explosionForce = 800f;
        [SerializeField] private float explosionDamage = 50f;

        [Header("Effects")]
        [SerializeField] private GameObject impactEffect;
        [SerializeField] private GameObject explosionEffect;
        [SerializeField] private AudioSource impactSound;

        public event Action<ThrowableWeapon, Collision> OnImpact;

        private bool _wasThrown;
        private float _throwSpeed;

        public enum ImpactMode { Bounce, Stick, Shatter, Explode }

        public override void OnRelease(VRHand hand)
        {
            base.OnRelease(hand);

            _throwSpeed = Rb.linearVelocity.magnitude;
            _wasThrown = _throwSpeed >= minThrowSpeed;
        }

        protected override void OnFirePressed(VRHand hand) { }

        private void OnCollisionEnter(Collision collision)
        {
            if (!_wasThrown) return;
            if ((HitLayers & (1 << collision.gameObject.layer)) == 0) return;

            _wasThrown = false;
            ContactPoint contact = collision.GetContact(0);

            float damage = BaseDamage * Mathf.Clamp(_throwSpeed / throwDamageScale, 0.5f, 3f);
            float force = _throwSpeed * Rb.mass;

            DealDamage(collision.collider, contact.point, Rb.linearVelocity.normalized,
                damage, force, DamageType.Throw);

            if (explodeOnImpact || impactMode == ImpactMode.Explode)
                PerformExplosion(contact.point);
            else
                HandleImpactMode(collision, contact);

            SpawnEffect(impactEffect, contact.point, contact.normal);
            if (impactSound != null) impactSound.Play();

            OnImpact?.Invoke(this, collision);
        }

        private void HandleImpactMode(Collision collision, ContactPoint contact)
        {
            switch (impactMode)
            {
                case ImpactMode.Stick:
                    Rb.isKinematic = true;
                    Rb.linearVelocity = Vector3.zero;
                    Rb.angularVelocity = Vector3.zero;
                    transform.position = contact.point + contact.normal * stickDepth;
                    transform.SetParent(collision.transform);
                    enabled = false;
                    break;

                case ImpactMode.Shatter:
                    SpawnEffect(impactEffect, contact.point, contact.normal);
                    Destroy(gameObject, 0.05f);
                    break;

                case ImpactMode.Bounce:
                    break;
            }
        }

        private void PerformExplosion(Vector3 center)
        {
            SpawnEffect(explosionEffect, center, Vector3.up);

            Collider[] hits = Physics.OverlapSphere(center, explosionRadius, HitLayers);
            foreach (var hit in hits)
            {
                float dist = Vector3.Distance(center, hit.transform.position);
                float falloff = 1f - Mathf.Clamp01(dist / explosionRadius);
                Vector3 dir = (hit.transform.position - center).normalized;

                if (dir.sqrMagnitude < 0.001f) dir = Vector3.up;

                DealDamage(hit, center, dir, explosionDamage * falloff,
                    explosionForce * falloff, DamageType.Explosion);

                var rb = hit.GetComponentInParent<Rigidbody>();
                if (rb != null)
                    rb.AddExplosionForce(explosionForce * falloff, center, explosionRadius);
            }

            Destroy(gameObject, 0.1f);
        }

        private void SpawnEffect(GameObject prefab, Vector3 position, Vector3 normal)
        {
            if (prefab == null) return;
            var fx = Instantiate(prefab, position, Quaternion.LookRotation(normal));
            Destroy(fx, 5f);
        }
    }
}
