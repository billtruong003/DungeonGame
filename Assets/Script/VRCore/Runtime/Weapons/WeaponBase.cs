using UnityEngine;
using VRCore.Hand;
using VRCore.Interaction;

namespace VRCore.Weapons
{
    public abstract class WeaponBase : TwoHandGrabbable
    {
        [Header("Weapon")]
        [SerializeField] private float baseDamage = 25f;
        [SerializeField] private LayerMask hitLayers = ~0;

        [Header("Haptics")]
        [SerializeField] private float fireHapticAmplitude = 0.6f;
        [SerializeField] private float fireHapticDuration = 0.08f;

        public float BaseDamage => baseDamage;
        public LayerMask HitLayers => hitLayers;
        public bool IsEquipped => IsHeld;

        protected VRHand PrimaryHand => HoldingHands.Count > 0 ? HoldingHands[0] : null;

        public override void OnSqueeze(VRHand hand)
        {
            base.OnSqueeze(hand);
            OnFirePressed(hand);
        }

        protected abstract void OnFirePressed(VRHand hand);

        protected void DealDamage(Collider hitCollider, Vector3 point, Vector3 direction, float damage, float force, DamageType type)
        {
            var zone = HitZone.FindOnCollider(hitCollider);
            var damageEvent = DamageEvent.Create(damage, type, point, direction, force, gameObject);

            if (zone != null)
                damageEvent = zone.ApplyZone(damageEvent);

            damageEvent.hitCollider = hitCollider;

            var damageable = hitCollider.GetComponentInParent<IDamageable>();
            damageable?.TakeDamage(damageEvent);

            OnDamageDealt(damageEvent);
        }

        protected virtual void OnDamageDealt(DamageEvent damage) { }

        protected void PlayFireHaptic()
        {
            if (PrimaryHand == null) return;
            PrimaryHand.Haptics.PlayHaptic(fireHapticAmplitude, fireHapticDuration);
        }
    }
}
