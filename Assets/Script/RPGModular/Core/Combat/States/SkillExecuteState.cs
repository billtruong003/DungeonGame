using UnityEngine;

namespace RPGModular
{
    public class SkillExecuteState : CombatState
    {
        private SkillCaster caster;
        private SkillData skill;
        private int level;
        private float timer;
        private bool damageApplied;
        private float damageTime; // when to apply damage (40% of animation)

        public SkillExecuteState(CombatStateMachine sm, SkillCaster caster, SkillData skill, int level) : base(sm)
        {
            this.caster = caster;
            this.skill = skill;
            this.level = level;
        }

        public override void Enter()
        {
            timer = 0f;
            damageApplied = false;
            damageTime = skill.animationDuration * 0.4f;

            // Play skill animation
            if (!string.IsNullOrEmpty(skill.vatAnimClip))
                AnimController?.ForcePlay(skill.vatAnimClip);
            else
                AnimController?.PlayAnimation("Skill_Execute", AnimationPriority.Skill);
        }

        public override void Tick(float deltaTime)
        {
            timer += deltaTime;

            // Apply damage at 40% of animation
            if (!damageApplied && timer >= damageTime)
            {
                damageApplied = true;
                ExecuteSkillHit();
            }

            // Animation done → combo window
            if (timer >= skill.animationDuration)
            {
                caster.NotifyCastComplete(skill);
                Game.Combo?.OnSkillUsed(skill);

                // Chi gain on skill use
                Game.Health?.NotifyCombatActivity();

                stateMachine.SwitchState(
                    new ComboReadyState(stateMachine, skill.comboWindowAfter),
                    CombatStateType.ComboReady);
            }
        }

        public override bool HandleHit(DamageInfo damageInfo)
        {
            if (skill.hasSuperArmor) return false; // take damage, don't interrupt
            if (!skill.canBeInterrupted) return false;

            // Interrupted
            caster.NotifyCastInterrupted(skill);
            return false; // let damage through
        }

        private void ExecuteSkillHit()
        {
            float rawDamage = caster.CalculateSkillDamage(skill, level);
            float perHitDamage = rawDamage / Mathf.Max(1, skill.hitCount);

            switch (skill.targetType)
            {
                case SkillTargetType.SingleTarget:
                    HitSingleTarget(perHitDamage);
                    break;

                case SkillTargetType.AoE_Circle:
                    HitAoECircle(rawDamage);
                    break;

                case SkillTargetType.AoE_Cone:
                    HitAoECone(rawDamage);
                    break;

                case SkillTargetType.AoE_Line:
                    HitAoELine(rawDamage);
                    break;

                case SkillTargetType.Self:
                    ApplySelfBuff();
                    break;

                case SkillTargetType.Projectile:
                    SpawnProjectile(rawDamage);
                    break;
            }

            // Block/Parry special handling
            if (skill.isBlockSkill || skill.isParrySkill)
            {
                // These are handled by HandleHit in this state
                // Block: reduce incoming damage for blockDuration
                // Parry: negate damage during parryWindow
            }
        }

        private void HitSingleTarget(float perHitDamage)
        {
            var lockOn = stateMachine.LockOn;
            if (lockOn == null || lockOn.CurrentTarget == null) return;

            var target = lockOn.CurrentTarget as MonoBehaviour;
            if (target == null) return;

            var damageable = target.GetComponent<IDamageable>();
            if (damageable == null) return;

            for (int i = 0; i < skill.hitCount; i++)
            {
                damageable.TakeDamage(new DamageInfo
                {
                    RawDamage = perHitDamage,
                    Type = skill.scaleType == DamageScaleType.Physical ? DamageType.Slash : DamageType.Fire,
                    HitDirection = (target.transform.position - stateMachine.transform.position).normalized,
                    Source = stateMachine
                });
            }

            ApplyStatusEffect(target.gameObject);
        }

        private void HitAoECircle(float damage)
        {
            var hits = Physics.OverlapSphere(stateMachine.transform.position, skill.aoeRadius);
            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(stateMachine.transform)) continue;
                var damageable = hit.GetComponent<IDamageable>();
                if (damageable == null) damageable = hit.GetComponentInParent<IDamageable>();
                if (damageable == null) continue;

                damageable.TakeDamage(new DamageInfo
                {
                    RawDamage = damage,
                    Type = DamageType.Strike,
                    HitDirection = (hit.transform.position - stateMachine.transform.position).normalized,
                    Source = stateMachine
                });

                ApplyStatusEffect(hit.gameObject);
            }
        }

        private void HitAoECone(float damage)
        {
            var hits = Physics.OverlapSphere(stateMachine.transform.position, skill.range);
            Vector3 forward = stateMachine.transform.forward;
            float halfAngle = skill.coneAngle * 0.5f;

            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(stateMachine.transform)) continue;
                Vector3 toTarget = (hit.transform.position - stateMachine.transform.position).normalized;
                if (Vector3.Angle(forward, toTarget) > halfAngle) continue;

                var damageable = hit.GetComponent<IDamageable>();
                if (damageable == null) damageable = hit.GetComponentInParent<IDamageable>();
                if (damageable == null) continue;

                damageable.TakeDamage(new DamageInfo
                {
                    RawDamage = damage,
                    Type = DamageType.Slash,
                    HitDirection = toTarget,
                    Source = stateMachine
                });

                ApplyStatusEffect(hit.gameObject);
            }
        }

        private void HitAoELine(float damage)
        {
            Vector3 origin = stateMachine.transform.position;
            Vector3 direction = stateMachine.transform.forward;
            var hits = Physics.SphereCastAll(origin, 1f, direction, skill.range);

            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(stateMachine.transform)) continue;
                var damageable = hit.collider.GetComponent<IDamageable>();
                if (damageable == null) damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable == null) continue;

                damageable.TakeDamage(new DamageInfo
                {
                    RawDamage = damage,
                    Type = DamageType.Pierce,
                    HitDirection = direction,
                    Source = stateMachine
                });
            }
        }

        private void ApplySelfBuff()
        {
            if (skill.selfBuff != null)
                Game.Status?.Apply(skill.selfBuff);
        }

        private void SpawnProjectile(float damage)
        {
            // Bill.Pool.Spawn(skill.projectilePrefabId, ...)
            // Projectile.Initialize(damage, direction, skill.appliedEffect)
            // Placeholder: direct hit instead
            HitSingleTarget(damage);
        }

        private void ApplyStatusEffect(GameObject target)
        {
            if (skill.appliedEffect == null) return;
            if (Random.value > skill.effectChance) return;

            // Apply to target's StatusEffectSystem if they have one
            var ses = target.GetComponent<StatusEffectSystem>();
            ses?.Apply(skill.appliedEffect);
        }
    }
}
