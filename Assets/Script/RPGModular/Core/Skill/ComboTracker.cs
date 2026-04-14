using System;
using UnityEngine;
using BillInspector;

namespace RPGModular
{
    [BillTitle("Combo Tracker", "Track skill combo chains")]
    public class ComboTracker : MonoBehaviour
    {
        [BillBoxGroup("Config")]
        [BillSlider(1f, 5f), BillSuffix("s")]
        [SerializeField] private float comboTimeout = 3f;
        [BillBoxGroup("Config")]
        [BillSlider(0f, 0.5f)]
        [SerializeField] private float comboDamageBonusPerChain = 0.1f;
        [BillBoxGroup("Config")]
        [BillSlider(1f, 2f)]
        [SerializeField] private float comboSpeedBonus = 1.2f;

        [BillReadOnly, BillShowInInspector]
        public int CurrentComboCount { get; private set; }
        private float comboTimer;
        private bool inCombo;

        public event Action OnComboStart;
        public event Action<int> OnComboCountChanged;
        public event Action OnComboEnd;

        private void Update()
        {
            if (inCombo)
            {
                comboTimer -= Time.deltaTime;
                if (comboTimer <= 0f)
                    EndCombo();
            }
        }

        public void OnSkillUsed(SkillData skill)
        {
            if (!inCombo)
            {
                inCombo = true;
                CurrentComboCount = 1;
                OnComboStart?.Invoke();
            }
            else
            {
                CurrentComboCount++;
            }

            comboTimer = comboTimeout;
            OnComboCountChanged?.Invoke(CurrentComboCount);
        }

        public void OnAutoAttackHit()
        {
            // Auto-attacks don't count as combo skills but reset timer
            if (inCombo)
                comboTimer = comboTimeout;
        }

        public float GetComboDamageBonus()
        {
            if (CurrentComboCount <= 2) return 1f;
            return 1f + (CurrentComboCount - 2) * comboDamageBonusPerChain;
        }

        public float GetComboSpeedBonus()
        {
            return inCombo ? comboSpeedBonus : 1f;
        }

        public void EndCombo()
        {
            if (!inCombo) return;
            inCombo = false;
            CurrentComboCount = 0;
            comboTimer = 0;
            OnComboEnd?.Invoke();
        }
    }
}
