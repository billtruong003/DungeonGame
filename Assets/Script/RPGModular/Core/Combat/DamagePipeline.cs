// File: Core/Combat/DamagePipeline.cs
// Pipeline tính damage: mỗi bước là một processor độc lập
// Dễ thêm/bớt bước (vd: thêm elemental resist, thêm buff giảm dmg)
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGModular
{
    /// <summary>
    /// Một bước trong pipeline tính damage.
    /// Implement interface này để thêm logic tính damage custom.
    /// </summary>
    public interface IDamageProcessor
    {
        int Priority { get; } // Thấp chạy trước
        void Process(DamageInfo input, DamageContext context, ref float currentDamage);
    }

    /// <summary>
    /// Context chứa thông tin của cả attacker và defender.
    /// Pipeline processors đọc context để tính toán.
    /// </summary>
    public class DamageContext
    {
        public IStatProvider AttackerStats;
        public IStatProvider DefenderStats;
        public ECombatState DefenderCombatState;
        public IWeapon AttackerWeapon;
        
        // Flags được set bởi processors trước đó
        public bool WasBlocked;
        public bool WasParried;
        public bool WasDodged;
        public bool WasCrit;
        public float BlockReduction;
        public float KnockbackForce;
        public Vector3 KnockbackDirection;
    }

    /// <summary>
    /// Pipeline tính damage. Chạy qua từng processor theo priority.
    /// Modular: thêm processor mới = thêm tính năng mới mà không sửa code cũ.
    /// </summary>
    public class DamagePipeline
    {
        private readonly List<IDamageProcessor> processors = new List<IDamageProcessor>();

        public DamagePipeline()
        {
            // Default processors - có thể thêm/bớt
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

        /// <summary>
        /// Chạy pipeline: DamageInfo + Context → DamageResult
        /// </summary>
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

    #region Default Processors

    /// <summary>
    /// Bước 1 (Priority 10): Tính crit
    /// </summary>
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

    /// <summary>
    /// Bước 2 (Priority 20): Dodge check
    /// Nếu dodge thành công → damage = 0, skip hết
    /// </summary>
    public class DodgeProcessor : IDamageProcessor
    {
        public int Priority => 20;

        public void Process(DamageInfo input, DamageContext context, ref float currentDamage)
        {
            if (context.DefenderStats == null) return;
            if (input.IsUnblockable) return; // Unblockable cũng undodgeable

            float dodgeChance = context.DefenderStats.DodgeChance;
            if (UnityEngine.Random.value < dodgeChance)
            {
                context.WasDodged = true;
                currentDamage = 0f;
            }
        }
    }

    /// <summary>
    /// Bước 3 (Priority 30): Block check
    /// Nếu đang block: giảm damage, nếu heavy attack → knockback + vẫn dính ít dmg
    /// </summary>
    public class BlockProcessor : IDamageProcessor
    {
        public int Priority => 30;
        
        private const float BlockReductionPercent = 0.7f;       // Block giảm 70% damage
        private const float HeavyBlockReductionPercent = 0.4f;  // Heavy attack chỉ giảm 40%
        private const float HeavyBlockKnockbackForce = 8f;

        public void Process(DamageInfo input, DamageContext context, ref float currentDamage)
        {
            if (context.WasDodged) return; // Đã dodge rồi
            if (context.DefenderCombatState != ECombatState.Blocking) return;
            if (input.IsUnblockable) return;

            context.WasBlocked = true;

            if (input.IsHeavyAttack)
            {
                // Heavy attack: vẫn block được nhưng bị knockback + dính ít dmg
                context.BlockReduction = HeavyBlockReductionPercent;
                currentDamage *= (1f - HeavyBlockReductionPercent);
                context.KnockbackForce = HeavyBlockKnockbackForce;
                context.KnockbackDirection = input.HitDirection;
            }
            else
            {
                // Block thường: giảm 70%
                context.BlockReduction = BlockReductionPercent;
                currentDamage *= (1f - BlockReductionPercent);
            }
        }
    }

    /// <summary>
    /// Bước 4 (Priority 40): Defense reduction
    /// Physical → PhysicalDefense, Magic → MagicDefense
    /// </summary>
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

            // Công thức: finalDmg = rawDmg × (100 / (100 + defense))
            // Defense 100 → giảm 50%, Defense 200 → giảm 67%
            float reductionMultiplier = 100f / (100f + defense);
            currentDamage *= reductionMultiplier;
        }
    }

    /// <summary>
    /// Bước 5 (Priority 100): Đảm bảo damage tối thiểu = 1
    /// </summary>
    public class MinDamageProcessor : IDamageProcessor
    {
        public int Priority => 100;

        public void Process(DamageInfo input, DamageContext context, ref float currentDamage)
        {
            if (context.WasDodged) return;
            currentDamage = Mathf.Max(currentDamage, 1f);
        }
    }

    #endregion
}
