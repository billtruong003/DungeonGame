using UnityEngine;

namespace BillVRCore.Weapons
{
    public interface IDamageable
    {
        void TakeDamage(DamageEvent damageEvent);
        bool IsAlive { get; }
    }

    public enum DamageType { Melee, Ranged, Explosion, Stab, Throw }

    [System.Serializable]
    public struct DamageEvent
    {
        public float amount;
        public DamageType type;
        public Vector3 point;
        public Vector3 direction;
        public float force;
        public int boneIndex;
        public int chainIndex;
        public GameObject source;
        public Collider hitCollider;

        public static DamageEvent Create(float amount, DamageType type, Vector3 point,
            Vector3 direction, float force, GameObject source = null)
        {
            return new DamageEvent
            {
                amount = amount,
                type = type,
                point = point,
                direction = direction,
                force = force,
                boneIndex = -1,
                chainIndex = -1,
                source = source
            };
        }
    }

    [System.Serializable]
    public struct HitResult
    {
        public bool didHit;
        public IDamageable target;
        public DamageEvent damage;
        public Vector3 hitPoint;
        public Vector3 hitNormal;
    }
}
