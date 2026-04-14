using System;
using UnityEngine;
using BillInspector;

namespace RPGModular
{
    [BillTitle("Focus Gauge", "Katana unique: patience = power")]
    public class FocusGauge : MonoBehaviour
    {
        [BillBoxGroup("Config")]
        [BillSlider(0f, 10f), BillSuffix("/s")] [SerializeField] private float gainWhileStationary = 3f;
        [BillBoxGroup("Config")]
        [BillSlider(0f, 50f)] [SerializeField] private float gainOnDodge = 15f;
        [BillBoxGroup("Config")]
        [BillSlider(0f, 50f)] [SerializeField] private float gainOnParry = 25f;
        [BillBoxGroup("Config")]
        [BillSlider(0f, 10f)] [SerializeField] private float gainPerAutoAttack = 5f;
        [BillBoxGroup("Config")]
        [BillSlider(0f, 10f), BillSuffix("/s")] [SerializeField] private float decayWhileMoving = 2f;
        [BillBoxGroup("Config")]
        [BillSlider(0f, 50f)] [SerializeField] private float lossOnHit = 30f;
        [BillBoxGroup("Config")]
        [BillSlider(0f, 10f), BillSuffix("/s")] [SerializeField] private float decayOutOfCombat = 5f;
        [BillBoxGroup("Config")]
        [BillSlider(0f, 1f)] [SerializeField] private float maxDamageBonus = 0.5f;

        [BillReadOnly, BillShowInInspector]
        public float Current { get; private set; }

        public float Max => 100f;
        public float Ratio => Current / Max;

        [BillBoxGroup("Dependencies")]
        [SerializeField] private WeaponHandler weaponHandler;
        [SerializeField] private PlayerInputHandler inputHandler;

        public bool IsActive => weaponHandler != null && weaponHandler.CurrentWeaponType == WeaponType.Katana;

        public event Action<float> OnFocusChanged;

        private void Awake()
        {
            if (weaponHandler == null) weaponHandler = GetComponent<WeaponHandler>();
            if (inputHandler == null) inputHandler = GetComponent<PlayerInputHandler>();
        }

        private void Update()
        {
            if (!IsActive)
            {
                if (Current > 0) SetFocus(0);
                return;
            }

            bool isMoving = inputHandler != null && inputHandler.MoveInput.sqrMagnitude > 0.01f;
            bool inCombat = Game.Combat != null &&
                Game.Combat.CurrentStateType != CombatStateType.Idle;

            if (inCombat)
            {
                if (!isMoving)
                    ModifyFocus(gainWhileStationary * Time.deltaTime);
                else
                    ModifyFocus(-decayWhileMoving * Time.deltaTime);
            }
            else
            {
                if (Current > 0)
                    ModifyFocus(-decayOutOfCombat * Time.deltaTime);
            }
        }

        public float GetDamageBonus()
        {
            if (!IsActive) return 1f;
            return 1f + Ratio * maxDamageBonus;
        }

        public void OnSuccessfulDodge() => ModifyFocus(gainOnDodge);
        public void OnSuccessfulParry() => ModifyFocus(gainOnParry);
        public void OnAutoAttackHit() => ModifyFocus(gainPerAutoAttack);
        public void OnHitTaken() => ModifyFocus(-lossOnHit);

        public void ModifyFocus(float amount)
        {
            float old = Current;
            Current = Mathf.Clamp(Current + amount, 0f, Max);
            if (Mathf.Abs(old - Current) > 0.01f)
                OnFocusChanged?.Invoke(Current);
        }

        private void SetFocus(float value)
        {
            Current = Mathf.Clamp(value, 0f, Max);
            OnFocusChanged?.Invoke(Current);
        }
    }
}
