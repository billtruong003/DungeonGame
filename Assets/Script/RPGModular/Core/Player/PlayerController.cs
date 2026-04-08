using System;
using UnityEngine;

namespace RPGModular
{
    public enum PlayerMode
    {
        Exploration,
        CombatTransition,
        Combat
    }

    public class PlayerController : MonoBehaviour
    {
        [Header("Core References")]
        [SerializeField] private LocomotionStateMachine locomotion;
        [SerializeField] private CombatStateMachine combat;
        [SerializeField] private PlayerInputHandler input;
        [SerializeField] private CameraController cameraController;
        [SerializeField] private AnimationController animController;
        [SerializeField] private WeaponHandler weaponHandler;
        [SerializeField] private LockOnSystem lockOn;
        [SerializeField] private HealthSystem health;
        [SerializeField] private AutoAttackSystem autoAttack;

        [Header("Transition")]
        [SerializeField] private float equipAnimDuration = 0.6f;
        [SerializeField] private float unequipAnimDuration = 0.5f;
        [SerializeField] private float combatExitDelay = 5f;

        [Header("Aggro Detection")]
        [SerializeField] private float aggroCheckRadius = 12f;
        [SerializeField] private LayerMask enemyLayer;

        private PlayerMode currentMode = PlayerMode.Exploration;
        private float transitionTimer;
        private float combatExitTimer;
        private bool transitioningToCombat;

        public PlayerMode CurrentMode => currentMode;

        public event Action<PlayerMode> OnModeChanged;

        private void Awake()
        {
            AutoFindDependencies();
        }

        private void Start()
        {
            SetMode(PlayerMode.Exploration);

            if (lockOn != null)
            {
                lockOn.OnTargetLocked += HandleTargetLocked;
                lockOn.OnTargetLost += HandleTargetLost;
            }

            if (health != null)
                health.OnDeath += HandleDeath;
        }

        private void OnDestroy()
        {
            if (lockOn != null)
            {
                lockOn.OnTargetLocked -= HandleTargetLocked;
                lockOn.OnTargetLost -= HandleTargetLost;
            }

            if (health != null)
                health.OnDeath -= HandleDeath;
        }

        private void Update()
        {
            switch (currentMode)
            {
                case PlayerMode.Exploration:
                    UpdateExploration();
                    break;
                case PlayerMode.CombatTransition:
                    UpdateTransition();
                    break;
                case PlayerMode.Combat:
                    UpdateCombat();
                    break;
            }
        }

        private void UpdateExploration()
        {
            if (input.LockOnToggle)
            {
                input.ConsumeLockOnInput();
                lockOn?.ToggleLock();
            }

            if (input.AttackInput)
            {
                Transform nearestEnemy = FindNearestEnemy();
                if (nearestEnemy != null)
                {
                    var lockable = nearestEnemy.GetComponent<ITargetLockable>();
                    if (lockable != null)
                        lockOn?.LockOn(lockable);
                }
            }

            if (HasEnemyInAggroRange())
                CheckEnemyAggro();
        }

        private void UpdateTransition()
        {
            transitionTimer -= Time.deltaTime;

            if (transitionTimer <= 0f)
            {
                if (transitioningToCombat)
                    FinishTransitionToCombat();
                else
                    FinishTransitionToExploration();
            }
        }

        private void UpdateCombat()
        {
            if (!lockOn.IsLockedOn && !HasEnemyInAggroRange())
            {
                combatExitTimer -= Time.deltaTime;
                if (combatExitTimer <= 0f)
                {
                    BeginTransitionToExploration();
                    return;
                }
            }
            else
            {
                combatExitTimer = combatExitDelay;
            }
        }

        private void HandleTargetLocked(ITargetLockable target)
        {
            if (currentMode == PlayerMode.Exploration)
                BeginTransitionToCombat();

            if (target is MonoBehaviour mb)
                cameraController?.SetLockOnTarget(mb.transform);
        }

        private void HandleTargetLost()
        {
            cameraController?.ClearLockOnTarget();
        }

        private void HandleDeath()
        {
            locomotion.enabled = false;
            combat.enabled = false;
        }

        private void BeginTransitionToCombat()
        {
            SetMode(PlayerMode.CombatTransition);
            transitioningToCombat = true;
            transitionTimer = equipAnimDuration;
            combatExitTimer = combatExitDelay;

            locomotion.enabled = false;

            var animSet = weaponHandler.MainHandWeapon?.AnimationSet
                ?? WeaponAnimationSet.CreateDefault(WeaponType.Unarmed);

            if (!string.IsNullOrEmpty(animSet.Equip))
                animController?.PlayAnimation(animSet.Equip, AnimationPriority.Skill, 0.1f);

            cameraController?.SetMode(CameraMode.Combat);
        }

        private void FinishTransitionToCombat()
        {
            SetMode(PlayerMode.Combat);
            combat.enabled = true;
            combat.ReturnToNeutral();
        }

        private void BeginTransitionToExploration()
        {
            SetMode(PlayerMode.CombatTransition);
            transitioningToCombat = false;
            transitionTimer = unequipAnimDuration;

            combat.enabled = false;
            autoAttack?.ResetCombo();

            var animSet = weaponHandler.MainHandWeapon?.AnimationSet
                ?? WeaponAnimationSet.CreateDefault(WeaponType.Unarmed);

            if (!string.IsNullOrEmpty(animSet.Unequip))
                animController?.PlayAnimation(animSet.Unequip, AnimationPriority.Skill, 0.1f);

            cameraController?.SetMode(CameraMode.FreeLook);
            cameraController?.ClearLockOnTarget();
        }

        private void FinishTransitionToExploration()
        {
            SetMode(PlayerMode.Exploration);
            locomotion.enabled = true;
            animController?.SetCombatMode(false);
        }

        public void ForceCombatMode()
        {
            if (currentMode == PlayerMode.Combat) return;

            locomotion.enabled = false;
            SetMode(PlayerMode.Combat);
            combat.enabled = true;
            combat.ReturnToNeutral();
            combatExitTimer = combatExitDelay;
            cameraController?.SetMode(CameraMode.Combat);
        }

        public void ForceExplorationMode()
        {
            if (currentMode == PlayerMode.Exploration) return;

            combat.enabled = false;
            autoAttack?.ResetCombo();
            SetMode(PlayerMode.Exploration);
            locomotion.enabled = true;
            animController?.SetCombatMode(false);
            cameraController?.SetMode(CameraMode.FreeLook);
            cameraController?.ClearLockOnTarget();
        }

        private void SetMode(PlayerMode mode)
        {
            currentMode = mode;
            OnModeChanged?.Invoke(mode);
        }

        private bool HasEnemyInAggroRange()
        {
            return Physics.CheckSphere(transform.position, aggroCheckRadius, enemyLayer, QueryTriggerInteraction.Ignore);
        }

        private void CheckEnemyAggro()
        {
            Collider[] enemies = Physics.OverlapSphere(transform.position, aggroCheckRadius, enemyLayer);
            foreach (var enemy in enemies)
            {
                var damageable = enemy.GetComponent<IDamageable>();
                if (damageable == null || !damageable.IsAlive) continue;

                if (damageable.CurrentCombatState == ECombatState.Attacking)
                {
                    var lockable = enemy.GetComponent<ITargetLockable>();
                    if (lockable != null && lockable.CanBeLocked)
                    {
                        lockOn?.LockOn(lockable);
                        return;
                    }
                }
            }
        }

        private Transform FindNearestEnemy()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, aggroCheckRadius, enemyLayer);
            Transform nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var hit in hits)
            {
                var damageable = hit.GetComponent<IDamageable>();
                if (damageable == null || !damageable.IsAlive) continue;

                float dist = Vector3.Distance(transform.position, hit.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = hit.transform;
                }
            }

            return nearest;
        }

        private void AutoFindDependencies()
        {
            if (locomotion == null) locomotion = GetComponent<LocomotionStateMachine>();
            if (combat == null) combat = GetComponent<CombatStateMachine>();
            if (input == null) input = GetComponent<PlayerInputHandler>();
            if (cameraController == null) cameraController = FindObjectOfType<CameraController>();
            if (animController == null) animController = GetComponentInChildren<AnimationController>();
            if (weaponHandler == null) weaponHandler = GetComponent<WeaponHandler>();
            if (lockOn == null) lockOn = GetComponent<LockOnSystem>();
            if (health == null) health = GetComponent<HealthSystem>();
            if (autoAttack == null) autoAttack = GetComponent<AutoAttackSystem>();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.1f);
            Gizmos.DrawWireSphere(transform.position, aggroCheckRadius);
        }
#endif
    }
}
