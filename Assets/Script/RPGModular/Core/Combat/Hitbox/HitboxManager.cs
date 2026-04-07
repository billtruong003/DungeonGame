// File: Core/Combat/Hitbox/HitboxManager.cs
// Quản lý hitbox cho combat
// Hitbox được enable/disable dựa trên AnimationPhase:
//   Startup → hitbox OFF
//   Active → hitbox ON (gây damage)
//   Recovery → hitbox OFF
// Mỗi vũ khí có thể có hitbox riêng (vd: kiếm hitbox dài, dao hitbox ngắn)
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGModular
{
    /// <summary>
    /// Một hitbox collider trên vũ khí hoặc tay chân
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DamageHitbox : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private WeaponSlot attachedSlot = WeaponSlot.MainHand;
        [SerializeField] private float damageMultiplier = 1.0f;  // Vd: tip of spear = 1.2x

        private Collider hitboxCollider;
        private HitboxManager manager;
        private HashSet<int> alreadyHit = new HashSet<int>(); // Tránh hit cùng target nhiều lần

        public WeaponSlot AttachedSlot => attachedSlot;
        public float DamageMultiplier => damageMultiplier;

        private void Awake()
        {
            hitboxCollider = GetComponent<Collider>();
            hitboxCollider.isTrigger = true;
            hitboxCollider.enabled = false;
            manager = GetComponentInParent<HitboxManager>();
        }

        /// <summary>
        /// Enable hitbox (bắt đầu detect collision)
        /// </summary>
        public void Activate()
        {
            alreadyHit.Clear();
            hitboxCollider.enabled = true;
        }

        /// <summary>
        /// Disable hitbox
        /// </summary>
        public void Deactivate()
        {
            hitboxCollider.enabled = false;
            alreadyHit.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!hitboxCollider.enabled) return;

            // Tránh hit cùng target nhiều lần trong cùng attack
            int id = other.gameObject.GetInstanceID();
            if (alreadyHit.Contains(id)) return;

            // Tránh self-hit
            if (other.transform.IsChildOf(transform.root)) return;

            // Tìm IDamageable trên target
            var damageable = other.GetComponent<IDamageable>();
            if (damageable == null)
                damageable = other.GetComponentInParent<IDamageable>();
            
            if (damageable == null) return;

            alreadyHit.Add(id);
            manager?.OnHitboxHit(this, damageable, other);
        }
    }

    /// <summary>
    /// Manager trung tâm quản lý tất cả hitbox trên entity.
    /// Listen AnimationPhase để auto enable/disable.
    /// </summary>
    public class HitboxManager : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private AnimationController animController;

        [Header("Hitbox References")]
        [SerializeField] private DamageHitbox mainHandHitbox;
        [SerializeField] private DamageHitbox offHandHitbox;
        [SerializeField] private DamageHitbox bodyHitbox;    // Cho unarmed, body slam...

        // Damage source - ai đang đánh
        private IDamageDealer damageDealer;
        private bool isHeavyAttack;

        // Events
        public event Action<IDamageable, DamageResult> OnHitConfirmed;
        public event Action<IDamageable> OnHitLanded;  // Trước khi tính damage

        private void Awake()
        {
            if (animController == null)
                animController = GetComponentInChildren<AnimationController>();
            
            damageDealer = GetComponent<IDamageDealer>();
        }

        private void OnEnable()
        {
            if (animController != null)
                animController.OnPhaseChanged += HandlePhaseChange;
        }

        private void OnDisable()
        {
            if (animController != null)
                animController.OnPhaseChanged -= HandlePhaseChange;
            
            DeactivateAllHitboxes();
        }

        /// <summary>
        /// Khi animation phase thay đổi:
        /// Active → enable hitbox phù hợp
        /// Khác → disable hết
        /// </summary>
        private void HandlePhaseChange(AnimationPhase phase)
        {
            switch (phase)
            {
                case AnimationPhase.Active:
                    ActivateRelevantHitboxes();
                    break;

                case AnimationPhase.Startup:
                case AnimationPhase.Recovery:
                case AnimationPhase.Done:
                    DeactivateAllHitboxes();
                    break;
            }
        }

        /// <summary>
        /// Enable hitbox dựa trên vũ khí đang cầm
        /// </summary>
        private void ActivateRelevantHitboxes()
        {
            var weaponUser = GetComponent<IWeaponUser>();
            
            if (weaponUser == null || weaponUser.CurrentWeaponType == WeaponType.Unarmed)
            {
                // Unarmed → body hitbox
                bodyHitbox?.Activate();
            }
            else
            {
                // Có vũ khí → main hand hitbox
                mainHandHitbox?.Activate();

                // Dual wield → cả off hand
                if (weaponUser.OffHandWeapon != null 
                    && weaponUser.OffHandWeapon.Type != WeaponType.Shield)
                {
                    offHandHitbox?.Activate();
                }
            }
        }

        private void DeactivateAllHitboxes()
        {
            mainHandHitbox?.Deactivate();
            offHandHitbox?.Deactivate();
            bodyHitbox?.Deactivate();
        }

        /// <summary>
        /// Callback từ DamageHitbox khi detect collision.
        /// Tính damage và apply lên target.
        /// </summary>
        public void OnHitboxHit(DamageHitbox hitbox, IDamageable target, Collider targetCollider)
        {
            OnHitLanded?.Invoke(target);

            if (damageDealer == null) return;

            // Tính damage
            DamageInfo dmgInfo = damageDealer.CalculateDamage(isHeavyAttack);
            dmgInfo.RawDamage *= hitbox.DamageMultiplier;

            // Hit direction
            Vector3 hitDir = (targetCollider.transform.position - transform.position).normalized;
            dmgInfo.HitDirection = hitDir;

            // Apply damage
            DamageResult result = target.TakeDamage(dmgInfo);

            OnHitConfirmed?.Invoke(target, result);
        }

        /// <summary>
        /// Set flags cho attack tiếp theo (gọi trước khi play attack animation)
        /// </summary>
        public void PrepareAttack(bool heavy = false)
        {
            isHeavyAttack = heavy;
        }
    }
}
