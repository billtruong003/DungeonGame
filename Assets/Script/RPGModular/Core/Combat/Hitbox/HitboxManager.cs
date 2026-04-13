using System;
using System.Collections.Generic;
using UnityEngine;
using BillInspector;

namespace RPGModular
{

    [RequireComponent(typeof(Collider))]
    public class DamageHitbox : MonoBehaviour
    {
        [BillEnumToggleButtons]
        [SerializeField] private WeaponSlot attachedSlot = WeaponSlot.MainHand;
        [BillSlider(0.1f, 3f)]
        [SerializeField] private float damageMultiplier = 1.0f;

        private Collider hitboxCollider;
        private HitboxManager manager;
        private HashSet<int> alreadyHit = new HashSet<int>();

        public WeaponSlot AttachedSlot => attachedSlot;
        public float DamageMultiplier => damageMultiplier;

        private void Awake()
        {
            hitboxCollider = GetComponent<Collider>();
            hitboxCollider.isTrigger = true;
            hitboxCollider.enabled = false;
            manager = GetComponentInParent<HitboxManager>();
        }

        public void Activate()
        {
            alreadyHit.Clear();
            hitboxCollider.enabled = true;
        }

        public void Deactivate()
        {
            hitboxCollider.enabled = false;
            alreadyHit.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!hitboxCollider.enabled) return;

            int id = other.gameObject.GetInstanceID();
            if (alreadyHit.Contains(id)) return;

            if (other.transform.IsChildOf(transform.root)) return;

            var damageable = other.GetComponent<IDamageable>();
            if (damageable == null)
                damageable = other.GetComponentInParent<IDamageable>();

            if (damageable == null) return;

            alreadyHit.Add(id);
            manager?.OnHitboxHit(this, damageable, other);
        }
    }

    public class HitboxManager : MonoBehaviour
    {
        [BillBoxGroup("Dependencies")]
        [SerializeField] private AnimationController animController;

        [BillBoxGroup("Hitbox References")]
        [SerializeField] private DamageHitbox mainHandHitbox;
        [BillBoxGroup("Hitbox References")]
        [SerializeField] private DamageHitbox offHandHitbox;
        [BillBoxGroup("Hitbox References")]
        [SerializeField] private DamageHitbox bodyHitbox;

        private IDamageDealer damageDealer;
        private bool isHeavyAttack;

        public event Action<IDamageable, DamageResult> OnHitConfirmed;
        public event Action<IDamageable> OnHitLanded;

        private void Awake()
        {
            if (animController == null)
                animController = GetComponentInChildren<AnimationController>();

            // Search component and parents for IDamageDealer
            damageDealer = GetComponent<IDamageDealer>();
            if (damageDealer == null)
                damageDealer = GetComponentInParent<IDamageDealer>();
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

        private void ActivateRelevantHitboxes()
        {
            var weaponUser = GetComponent<IWeaponUser>();
            if (weaponUser == null)
                weaponUser = GetComponentInParent<IWeaponUser>();

            if (weaponUser == null || weaponUser.CurrentWeaponType == WeaponType.Unarmed)
            {
                bodyHitbox?.Activate();
            }
            else
            {
                mainHandHitbox?.Activate();

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

        public void OnHitboxHit(DamageHitbox hitbox, IDamageable target, Collider targetCollider)
        {
            OnHitLanded?.Invoke(target);

            if (damageDealer == null) return;

            DamageInfo dmgInfo = damageDealer.CalculateDamage(isHeavyAttack);
            dmgInfo.RawDamage *= hitbox.DamageMultiplier;

            Vector3 hitDir = (targetCollider.transform.position - transform.position).normalized;
            dmgInfo.HitDirection = hitDir;

            DamageResult result = target.TakeDamage(dmgInfo);

            // Fire OnDamageDealt on the dealer
            if (damageDealer is CombatStateMachine csm)
                csm.NotifyDamageDealt(target, result);
            else if (damageDealer is EnemyBase enemy)
                enemy.NotifyDamageDealt(target, result);

            OnHitConfirmed?.Invoke(target, result);
        }

        public void PrepareAttack(bool heavy = false)
        {
            isHeavyAttack = heavy;
        }
    }
}
