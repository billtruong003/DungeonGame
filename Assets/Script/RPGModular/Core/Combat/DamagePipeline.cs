using System;
using System.Collections.Generic;
using UnityEngine;
using BillInspector;

namespace RPGModular
{

    public interface IDamageProcessor
    {
        int Priority { get; }
        void Process(DamageInfo input, DamageContext context, ref float currentDamage);
    }

    public class DamageContext
    {
        public IStatProvider AttackerStats;
        public IStatProvider DefenderStats;
        public ECombatState DefenderCombatState;
        public IWeapon AttackerWeapon;

        public bool WasBlocked;
        public bool WasParried;
        public bool WasDodged;
        public bool WasCrit;
        public float BlockReduction;
        public float KnockbackForce;
        public Vector3 KnockbackDirection;
    }

    public class DamagePipeline
    {
        private readonly List<IDamageProcessor> processors = new List<IDamageProcessor>();

        public DamagePipeline()
        {

            RegisterProcessor(new CritProcessor());
            RegisterProcessor(new DodgeProcessor());
            RegisterProcessor(new BlockProcessor());
            RegisterProcessor(new DefenseProcessor());
            RegisterProcessor(new MinDamageProcessor());
        }

        public void RegisterProcessor(IDamageProcessor processor)
        {
            processors.Add(processor);
            processors.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        public void RemoveProcessor<T>() where T : IDamageProcessor
        {
            processors.RemoveAll(p => p is T);
        }

        public DamageResult Calculate(DamageInfo info, DamageContext context)
        {
            float currentDamage = info.RawDamage;

            foreach (var processor in processors)
            {
                processor.Process(info, context, ref currentDamage);
            }

            return new DamageResult
            {
                FinalDamage = currentDamage,
                WasBlocked = context.WasBlocked,
                WasParried = context.WasParried,
                WasDodged = context.WasDodged,
                WasCrit = context.WasCrit,
                DamageReduced = info.RawDamage - currentDamage,
                KnockbackDirection = context.KnockbackDirection,
                KnockbackForce = context.KnockbackForce
            };
        }
    }

    public class CritProcessor : IDamageProcessor
    {
        public int Priority => 10;

        public void Process(DamageInfo input, DamageContext context, ref float currentDamage)
        {
            if (context.AttackerStats == null) return;

            float critChance = context.AttackerStats.CritChance;
            if (UnityEngine.Random.value < critChance)
            {
                context.WasCrit = true;
                float critMult = context.AttackerStats.CritDamage;
                currentDamage *= critMult;
                input.IsCrit = true;
                input.CritMultiplier = critMult;
            }
        }
    }

    public class DodgeProcessor : IDamageProcessor
    {
        public int Priority => 20;

        public void Process(DamageInfo input, DamageContext context, ref float currentDamage)
        {
            if (context.DefenderStats == null) return;
            if (input.IsUnblockable) return;

            float dodgeChance = context.DefenderStats.DodgeChance;
            if (UnityEngine.Random.value < dodgeChance)
            {
                context.WasDodged = true;
                currentDamage = 0f;
            }
        }
    }

    public class BlockProcessor : IDamageProcessor
    {
        public int Priority => 30;

        private const float BlockReductionPercent = 0.7f;
        private const float HeavyBlockReductionPercent = 0.4f;
        private const float HeavyBlockKnockbackForce = 8f;

        public void Process(DamageInfo input, DamageContext context, ref float currentDamage)
        {
            if (context.WasDodged) return;
            if (context.DefenderCombatState != ECombatState.Blocking) return;
            if (input.IsUnblockable) return;

            context.WasBlocked = true;

            if (input.IsHeavyAttack)
            {

                context.BlockReduction = HeavyBlockReductionPercent;
                currentDamage *= (1f - HeavyBlockReductionPercent);
                context.KnockbackForce = HeavyBlockKnockbackForce;
                context.KnockbackDirection = input.HitDirection;
            }
            else
            {

                context.BlockReduction = BlockReductionPercent;
                currentDamage *= (1f - BlockReductionPercent);
            }
        }
    }

    public class DefenseProcessor : IDamageProcessor
    {
        public int Priority => 40;

        public void Process(DamageInfo input, DamageContext context, ref float currentDamage)
        {
            if (context.WasDodged) return;
            if (context.DefenderStats == null) return;

            float defense;
            switch (input.Type)
            {
                case DamageType.Slash:
                case DamageType.Pierce:
                case DamageType.Strike:
                    defense = context.DefenderStats.PhysicalDefense;
                    break;
                default:
                    defense = context.DefenderStats.MagicDefense;
                    break;
            }

            float reductionMultiplier = 100f / (100f + defense);
            currentDamage *= reductionMultiplier;
        }
    }

    public class MinDamageProcessor : IDamageProcessor
    {
        public int Priority => 100;

        public void Process(DamageInfo input, DamageContext context, ref float currentDamage)
        {
            if (context.WasDodged) return;
            currentDamage = Mathf.Max(currentDamage, 1f);
        }
    }

}
