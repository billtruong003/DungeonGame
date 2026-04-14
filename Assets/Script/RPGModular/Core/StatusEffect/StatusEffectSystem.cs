using System;
using System.Collections.Generic;
using UnityEngine;
using BillInspector;

namespace RPGModular
{
    [BillTitle("Status Effect System", "Buff/Debuff management")]
    public class StatusEffectSystem : MonoBehaviour
    {
        [BillBoxGroup("Dependencies")]
        [SerializeField] private CharacterStats stats;
        [SerializeField] private HealthSystem health;

        [BillReadOnly, BillShowInInspector]
        [BillFoldoutGroup("Active Effects")]
        private List<ActiveStatusEffect> activeEffects = new List<ActiveStatusEffect>();

        public IReadOnlyList<ActiveStatusEffect> ActiveEffects => activeEffects;

        public event Action<ActiveStatusEffect> OnEffectApplied;
        public event Action<ActiveStatusEffect> OnEffectRemoved;
        public event Action<ActiveStatusEffect> OnEffectTick;
        public event Action<ActiveStatusEffect, int> OnStackChanged;

        private void Awake()
        {
            if (stats == null) stats = GetComponent<CharacterStats>();
            if (health == null) health = GetComponent<HealthSystem>();
        }

        private void Update()
        {
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                var effect = activeEffects[i];

                // Duration countdown
                if (!effect.Data.isPermanent)
                {
                    effect.RemainingDuration -= Time.deltaTime;
                    if (effect.RemainingDuration <= 0f)
                    {
                        RemoveEffectAt(i);
                        continue;
                    }
                }

                // Tick (DoT / HoT)
                if (Mathf.Abs(effect.Data.tickValue) > 0.001f)
                {
                    effect.TickTimer -= Time.deltaTime;
                    if (effect.TickTimer <= 0f)
                    {
                        effect.TickTimer = effect.Data.tickInterval;
                        float tickAmount = effect.Data.tickValue * effect.CurrentStacks;

                        if (health != null)
                        {
                            if (tickAmount > 0)
                                health.Heal(tickAmount);
                            else
                                health.ApplyDamage(-tickAmount);
                        }

                        OnEffectTick?.Invoke(effect);
                    }
                }
            }
        }

        public void Apply(StatusEffectData data, int stacks = 1, object source = null)
        {
            if (data == null) return;

            // Check existing
            var existing = activeEffects.Find(e => e.Data == data);
            if (existing != null)
            {
                switch (data.stackBehavior)
                {
                    case StackBehavior.Refresh:
                        existing.RemainingDuration = data.baseDuration;
                        return;

                    case StackBehavior.AddDuration:
                        existing.RemainingDuration += data.baseDuration;
                        return;

                    case StackBehavior.StackIntensity:
                        int newStacks = Mathf.Min(existing.CurrentStacks + stacks, data.maxStacks);
                        if (newStacks != existing.CurrentStacks)
                        {
                            // Remove old modifiers, apply new with multiplied values
                            RemoveStatModifiers(existing);
                            existing.CurrentStacks = newStacks;
                            ApplyStatModifiers(existing);
                            OnStackChanged?.Invoke(existing, newStacks);
                        }
                        existing.RemainingDuration = data.baseDuration;
                        return;

                    case StackBehavior.StackSeparate:
                        break; // fall through to create new
                }
            }

            // Create new effect
            var newEffect = new ActiveStatusEffect
            {
                Data = data,
                RemainingDuration = data.baseDuration,
                CurrentStacks = stacks,
                TickTimer = data.tickInterval,
                Source = source
            };

            activeEffects.Add(newEffect);
            ApplyStatModifiers(newEffect);
            OnEffectApplied?.Invoke(newEffect);
        }

        public void RemoveEffect(StatusEffectData data)
        {
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                if (activeEffects[i].Data == data)
                {
                    RemoveEffectAt(i);
                    return;
                }
            }
        }

        public void RemoveAllDebuffs()
        {
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                if (activeEffects[i].Data.isDebuff)
                    RemoveEffectAt(i);
            }
        }

        public bool HasEffect(StatusEffectData data)
        {
            return activeEffects.Exists(e => e.Data == data);
        }

        public float GetMoveSpeedMultiplier()
        {
            float mult = 1f;
            foreach (var e in activeEffects)
            {
                if (Mathf.Abs(e.Data.moveSpeedMultiplier - 1f) > 0.001f)
                    mult *= e.Data.moveSpeedMultiplier;
            }
            return mult;
        }

        private void ApplyStatModifiers(ActiveStatusEffect effect)
        {
            if (effect.Data.statModifiers == null || stats == null) return;
            foreach (var bonus in effect.Data.statModifiers)
            {
                var mod = new StatModifier(bonus.stat, bonus.modType,
                    bonus.value * effect.CurrentStacks, 0, effect);
                stats.AddModifier(mod);
            }
        }

        private void RemoveStatModifiers(ActiveStatusEffect effect)
        {
            if (stats == null) return;
            stats.RemoveAllModifiersFromSource(effect);
        }

        private void RemoveEffectAt(int index)
        {
            var effect = activeEffects[index];
            RemoveStatModifiers(effect);
            activeEffects.RemoveAt(index);
            OnEffectRemoved?.Invoke(effect);
        }
    }
}
