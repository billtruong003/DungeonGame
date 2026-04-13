using System;
using UnityEngine;
using BillInspector;

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
        [BillTitle("Player Controller", "Bridge between Exploration and Combat modes")]
        [BillBoxGroup("Core References")]
        [BillRequired]
        [SerializeField] private LocomotionStateMachine locomotion;
        [BillBoxGroup("Core References")]
        [BillRequired]
        [SerializeField] private CombatStateMachine combat;
        [BillBoxGroup("Core References")]
        [BillRequired]
        [SerializeField] private PlayerInputHandler input;
        [BillBoxGroup("Core References")]
        [SerializeField] private CameraController cameraController;
        [BillBoxGroup("Core References")]
        [SerializeField] private AnimationController animController;
        [BillBoxGroup("Core References")]
        [SerializeField] private WeaponHandler weaponHandler;
        [BillBoxGroup("Core References")]
        [SerializeField] private LockOnSystem lockOn;
        [BillBoxGroup("Core References")]
        [SerializeField] private HealthSystem health;
        [BillBoxGroup("Core References")]
        [SerializeField] private AutoAttackSystem autoAttack;

        [BillFoldoutGroup("Transition")]
        [BillSlider(0.1f, 2f), BillSuffix("s")]
        [SerializeField] private float equipAnimDuration = 0.6f;
        [BillFoldoutGroup("Transition")]
        [BillSlider(0.1f, 2f), BillSuffix("s")]
        [SerializeField] private float unequipAnimDuration = 0.5f;
        [BillFoldoutGroup("Transition")]
        [BillSlider(1f, 15f), BillSuffix("s")]
        [SerializeField] private float combatExitDelay = 5f;

        [BillFoldoutGroup("Aggro Detection")]
        [BillSlider(5f, 30f), BillSuffix("m")]
        [SerializeField] private float aggroCheckRadius = 12f;
        [BillFoldoutGroup("Aggro Detection")]
        [SerializeField] private LayerMask enemyLayer;

        private PlayerMode currentMode = PlayerMode.Exploration;
        private float transitionTimer;
        private float combatExitTimer;
        private bool transitioningToCombat;

        // Pre-allocated buffer for OverlapSphereNonAlloc
        private static readonly Collider[] _aggroBuffer = new Collider[16];

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

            weaponHandler?.UnsheathWeapons();
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

            weaponHandler?.SheathWeapons();
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
            int count = Physics.OverlapSphereNonAlloc(transform.position, aggroCheckRadius, _aggroBuffer, enemyLayer);
            for (int i = 0; i < count; i++)
            {
                var enemy = _aggroBuffer[i];
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
            int count = Physics.OverlapSphereNonAlloc(transform.position, aggroCheckRadius, _aggroBuffer, enemyLayer);
            Transform nearest = null;
            float nearestDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var hit = _aggroBuffer[i];
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
