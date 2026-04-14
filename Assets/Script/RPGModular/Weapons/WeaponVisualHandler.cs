using System;
using UnityEngine;
using BillInspector;

namespace RPGModular
{
    [BillTitle("Weapon Visual Handler", "Weapon mesh spawn on hand bones")]
    public class WeaponVisualHandler : MonoBehaviour
    {
        [BillBoxGroup("Mount Points")]
        [SerializeField] private Transform mainHandMount;
        [SerializeField] private Transform offHandMount;
        [SerializeField] private Transform mainHandSheath;
        [SerializeField] private Transform offHandSheath;

        [BillBoxGroup("Dependencies")]
        [SerializeField] private WeaponHandler weaponHandler;

        private GameObject mainHandVisual;
        private GameObject offHandVisual;
        private bool isDrawn;

        public bool IsDrawn => isDrawn;
        public event Action OnWeaponDrawn;
        public event Action OnWeaponSheathed;

        private void Awake()
        {
            if (weaponHandler == null)
                weaponHandler = GetComponent<WeaponHandler>();
        }

        private void OnEnable()
        {
            if (weaponHandler != null)
                weaponHandler.OnWeaponChanged += OnWeaponChanged;
        }

        private void OnDisable()
        {
            if (weaponHandler != null)
                weaponHandler.OnWeaponChanged -= OnWeaponChanged;
        }

        private void OnWeaponChanged(IWeapon weapon, WeaponSlot slot)
        {
            RefreshVisuals();
        }

        public void DrawWeapon()
        {
            if (isDrawn) return;
            isDrawn = true;

            // Move from sheath to hand
            if (mainHandVisual != null && mainHandMount != null)
            {
                mainHandVisual.transform.SetParent(mainHandMount, false);
                mainHandVisual.transform.localPosition = Vector3.zero;
                mainHandVisual.transform.localRotation = Quaternion.identity;
            }

            if (offHandVisual != null && offHandMount != null)
            {
                offHandVisual.transform.SetParent(offHandMount, false);
                offHandVisual.transform.localPosition = Vector3.zero;
                offHandVisual.transform.localRotation = Quaternion.identity;
            }

            OnWeaponDrawn?.Invoke();
        }

        public void SheatheWeapon()
        {
            if (!isDrawn) return;
            isDrawn = false;

            if (mainHandVisual != null && mainHandSheath != null)
            {
                mainHandVisual.transform.SetParent(mainHandSheath, false);
                mainHandVisual.transform.localPosition = Vector3.zero;
                mainHandVisual.transform.localRotation = Quaternion.identity;
            }

            if (offHandVisual != null && offHandSheath != null)
            {
                offHandVisual.transform.SetParent(offHandSheath, false);
                offHandVisual.transform.localPosition = Vector3.zero;
                offHandVisual.transform.localRotation = Quaternion.identity;
            }

            OnWeaponSheathed?.Invoke();
        }

        private void RefreshVisuals()
        {
            ClearVisual(ref mainHandVisual);
            ClearVisual(ref offHandVisual);

            var mainWeapon = weaponHandler?.MainHandWeapon;
            if (mainWeapon != null)
            {
                var weaponData = weaponHandler.MainHandWeaponData;
                if (weaponData != null && weaponData.VisualPrefab != null)
                {
                    Transform parent = isDrawn ? mainHandMount : mainHandSheath;
                    if (parent != null)
                    {
                        mainHandVisual = Instantiate(weaponData.VisualPrefab, parent);
                        mainHandVisual.transform.localPosition = Vector3.zero;
                        mainHandVisual.transform.localRotation = Quaternion.identity;
                    }
                }
            }

            var offWeapon = weaponHandler?.OffHandWeapon;
            if (offWeapon != null)
            {
                var weaponData = weaponHandler.OffHandWeaponData;
                if (weaponData != null && weaponData.VisualPrefab != null)
                {
                    Transform parent = isDrawn ? offHandMount : offHandSheath;
                    if (parent != null)
                    {
                        offHandVisual = Instantiate(weaponData.VisualPrefab, parent);
                        offHandVisual.transform.localPosition = Vector3.zero;
                        offHandVisual.transform.localRotation = Quaternion.identity;
                    }
                }
            }
        }

        private void ClearVisual(ref GameObject visual)
        {
            if (visual != null)
            {
                Destroy(visual);
                visual = null;
            }
        }
    }
}
