using UnityEngine;

namespace VRCore.Weapons
{
    public class HitZone : MonoBehaviour
    {
        [SerializeField] private float damageMultiplier = 1f;
        [SerializeField] private int boneChainIndex = -1;
        [SerializeField] private int boneIndex = -1;
        [SerializeField] private string zoneId = "";

        public float DamageMultiplier => damageMultiplier;
        public int BoneChainIndex => boneChainIndex;
        public int BoneIndex => boneIndex;
        public string ZoneId => zoneId;

        public DamageEvent ApplyZone(DamageEvent incoming)
        {
            incoming.amount *= damageMultiplier;
            if (boneChainIndex >= 0) incoming.chainIndex = boneChainIndex;
            if (boneIndex >= 0) incoming.boneIndex = boneIndex;
            return incoming;
        }

        public static HitZone FindOnCollider(Collider col)
        {
            var zone = col.GetComponent<HitZone>();
            if (zone != null) return zone;
            return col.GetComponentInParent<HitZone>();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = damageMultiplier > 1.5f
                ? Color.red
                : (damageMultiplier > 1f ? Color.yellow : Color.green);

            var col = GetComponent<Collider>();
            if (col is BoxCollider box)
                Gizmos.DrawWireCube(box.bounds.center, box.bounds.size);
            else if (col is SphereCollider sphere)
                Gizmos.DrawWireSphere(sphere.bounds.center, sphere.radius);
            else if (col is CapsuleCollider)
                Gizmos.DrawWireSphere(col.bounds.center, col.bounds.extents.magnitude);
        }
    }
}
