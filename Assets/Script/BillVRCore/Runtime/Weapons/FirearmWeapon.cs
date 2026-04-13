using System;
using UnityEngine;
using BillVRCore.Hand;
using BillVRCore.Interaction;

namespace BillVRCore.Weapons
{
    public class FirearmWeapon : WeaponBase
    {
        [Header("Firearm")]
        [SerializeField] private Transform muzzlePoint;
        [SerializeField] private float range = 100f;
        [SerializeField] private float bulletForce = 500f;
        [SerializeField] private float fireRate = 10f;
        [SerializeField] private bool automatic;

        [Header("Ammo")]
        [SerializeField] private int maxAmmo = 30;
        [SerializeField] private PlacePoint magazineSocket;

        [Header("Recoil")]
        [SerializeField] private float recoilForce = 2f;
        [SerializeField] private float recoilTorque = 1f;

        [Header("Effects")]
        [SerializeField] private ParticleSystem muzzleFlash;
        [SerializeField] private AudioSource fireSound;

        public int CurrentAmmo { get; private set; }
        public bool HasAmmo => CurrentAmmo > 0;
        public bool HasMagazine => magazineSocket != null && magazineSocket.HasPlacedObject;
        public event Action OnFired;
        public event Action OnEmptyClick;
        public event Action OnReloaded;

        private float _lastFireTime;
        private bool _triggerHeld;

        protected override void Awake()
        {
            base.Awake();
            CurrentAmmo = maxAmmo;

            if (muzzlePoint == null)
            {
                muzzlePoint = new GameObject("Muzzle").transform;
                muzzlePoint.SetParent(transform, false);
                muzzlePoint.localPosition = Vector3.forward * 0.3f;
            }

            if (magazineSocket != null)
                magazineSocket.OnPlaceEvent += OnMagazineInserted;
        }

        protected override void Update()
        {
            base.Update();
            if (!IsEquipped) return;
            if (!automatic) return;

            var input = PrimaryHand?.GetInput();
            if (input == null) return;

            if (input.TriggerHeld(PrimaryHand.Side))
                TryFire();
        }

        protected override void OnFirePressed(VRHand hand)
        {
            TryFire();
        }

        private void TryFire()
        {
            if (Time.time - _lastFireTime < 1f / fireRate) return;

            if (!HasAmmo)
            {
                OnEmptyClick?.Invoke();
                return;
            }

            CurrentAmmo--;
            _lastFireTime = Time.time;

            PerformRaycast();
            ApplyRecoil();
            PlayFireHaptic();
            PlayEffects();

            OnFired?.Invoke();
        }

        private void PerformRaycast()
        {
            Vector3 origin = muzzlePoint.position;
            Vector3 direction = muzzlePoint.forward;

            if (!Physics.Raycast(origin, direction, out RaycastHit hit, range, HitLayers,
                QueryTriggerInteraction.Ignore))
                return;

            DealDamage(hit.collider, hit.point, direction, BaseDamage, bulletForce, DamageType.Ranged);
        }

        private void ApplyRecoil()
        {
            if (Rb == null) return;

            Vector3 recoilDir = -muzzlePoint.forward * recoilForce;
            Rb.AddForceAtPosition(recoilDir, muzzlePoint.position, ForceMode.Impulse);

            Vector3 torque = muzzlePoint.right * recoilTorque;
            Rb.AddTorque(torque, ForceMode.Impulse);
        }

        private void PlayEffects()
        {
            if (muzzleFlash != null) muzzleFlash.Play();
            if (fireSound != null) fireSound.Play();
        }

        public void Reload(int ammo)
        {
            CurrentAmmo = Mathf.Min(CurrentAmmo + ammo, maxAmmo);
            OnReloaded?.Invoke();
        }

        public void SetAmmo(int ammo)
        {
            CurrentAmmo = Mathf.Clamp(ammo, 0, maxAmmo);
        }

        private void OnMagazineInserted(PlacePoint point, Grabbable mag)
        {
            Reload(maxAmmo);
        }

        private void OnDestroy()
        {
            if (magazineSocket != null)
                magazineSocket.OnPlaceEvent -= OnMagazineInserted;
        }
    }
}
