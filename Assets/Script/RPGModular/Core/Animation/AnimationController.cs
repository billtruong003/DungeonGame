using System;
using System.Collections;
using UnityEngine;

namespace RPGModular
{
    [RequireComponent(typeof(Animator))]
    public class AnimationController : MonoBehaviour, IAnimationController
    {
        [Header("Dependencies")]
        [SerializeField] private Animator animator;

        [Header("Settings")]
        [SerializeField] private float defaultCrossFade = 0.1f;
        [SerializeField] private float priorityResetDelay = 0.1f;

        private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveYHash = Animator.StringToHash("MoveY");
        private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
        private static readonly int InCombatHash = Animator.StringToHash("InCombat");

        private AnimationPriority currentPriority = AnimationPriority.Locomotion;
        private AnimationPhase currentPhase = AnimationPhase.Done;
        private AnimationActionData currentAction;
        private Coroutine actionCoroutine;
        private Action<AnimationPhase> currentPhaseCallback;

        private WeaponAnimationSet currentWeaponAnimSet;
        private bool isInCombat;

        public event Action OnActionComplete;
        public event Action<AnimationPhase> OnPhaseChanged;

        public AnimationPriority CurrentPriority => currentPriority;
        public AnimationPhase CurrentPhase => currentPhase;
        public bool CanBeInterrupted => currentPhase == AnimationPhase.Startup
                                        && currentAction != null
                                        && currentAction.CanCancelStartup
                                     || currentPhase == AnimationPhase.Recovery
                                        && currentAction != null
                                        && currentAction.CanCancelRecovery
                                     || currentPhase == AnimationPhase.Done;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();
        }

        public void SetWeaponAnimationSet(WeaponAnimationSet animSet)
        {
            currentWeaponAnimSet = animSet;

            if (isInCombat && currentPhase == AnimationPhase.Done
                && currentPriority <= AnimationPriority.CombatIdle)
            {
                ReturnToCombatIdle();
            }
        }

        public void SetCombatMode(bool combat)
        {
            isInCombat = combat;
            animator.SetBool(InCombatHash, combat);

            if (combat && currentWeaponAnimSet != null)
            {
                ReturnToCombatIdle();
            }
        }

        public bool PlayAnimation(string stateName, AnimationPriority priority,
                                  float crossFadeDuration = 0.1f, int layer = 0)
        {

            if (priority < currentPriority && currentPhase != AnimationPhase.Done)
                return false;

            if (actionCoroutine != null)
            {
                StopCoroutine(actionCoroutine);
                actionCoroutine = null;
            }

            currentPriority = priority;
            currentPhase = AnimationPhase.Done;
            currentAction = null;

            animator.CrossFade(stateName, crossFadeDuration, layer);
            return true;
        }

        public bool PlayAction(AnimationActionData actionData, AnimationPriority priority,
                              Action<AnimationPhase> onPhaseChanged = null)
        {

            if (priority < currentPriority && !CanBeInterrupted)
                return false;

            if (actionCoroutine != null)
            {
                StopCoroutine(actionCoroutine);
                actionCoroutine = null;
            }

            currentPriority = priority;
            currentAction = actionData;
            currentPhaseCallback = onPhaseChanged;

            animator.CrossFade(actionData.AnimationStateName, actionData.CrossFadeDuration,
                             actionData.AnimatorLayer);

            actionCoroutine = StartCoroutine(TrackActionPhases(actionData));
            return true;
        }

        public void ForcePlay(string stateName, float crossFadeDuration = 0.1f, int layer = 0)
        {
            if (actionCoroutine != null)
            {
                StopCoroutine(actionCoroutine);
                actionCoroutine = null;
            }

            currentPriority = AnimationPriority.Death;
            currentPhase = AnimationPhase.Done;
            currentAction = null;

            animator.CrossFade(stateName, crossFadeDuration, layer);
        }

        public void SetFloat(string paramName, float value, float dampTime = 0.1f)
        {
            animator.SetFloat(Animator.StringToHash(paramName), value, dampTime, Time.deltaTime);
        }

        public void SetBool(string paramName, bool value)
        {
            animator.SetBool(Animator.StringToHash(paramName), value);
        }

        public void UpdateLocomotion(float speed, float moveX = 0f, float moveY = 0f)
        {
            animator.SetFloat(MoveSpeedHash, speed, 0.1f, Time.deltaTime);
            animator.SetFloat(MoveXHash, moveX, 0.1f, Time.deltaTime);
            animator.SetFloat(MoveYHash, moveY, 0.1f, Time.deltaTime);
        }

        public void SetGrounded(bool grounded)
        {
            animator.SetBool(IsGroundedHash, grounded);
        }

        public float GetNormalizedTime(int layer = 0)
        {
            var stateInfo = animator.GetCurrentAnimatorStateInfo(layer);
            return stateInfo.normalizedTime % 1f;
        }

        private IEnumerator TrackActionPhases(AnimationActionData actionData)
        {

            yield return null;

            SetPhase(AnimationPhase.Startup);
            while (GetNormalizedTime(actionData.AnimatorLayer) < actionData.StartupEnd)
            {
                yield return null;
            }

            SetPhase(AnimationPhase.Active);
            while (GetNormalizedTime(actionData.AnimatorLayer) < actionData.ActiveEnd)
            {
                yield return null;
            }

            SetPhase(AnimationPhase.Recovery);
            while (GetNormalizedTime(actionData.AnimatorLayer) < 0.95f)
            {
                yield return null;
            }

            SetPhase(AnimationPhase.Done);
            currentAction = null;
            currentPhaseCallback = null;
            actionCoroutine = null;

            yield return new WaitForSeconds(priorityResetDelay);

            if (currentPhase == AnimationPhase.Done)
            {
                currentPriority = isInCombat ? AnimationPriority.CombatIdle : AnimationPriority.Locomotion;

                if (isInCombat)
                {
                    ReturnToCombatIdle();
                }

                OnActionComplete?.Invoke();
            }
        }

        private void SetPhase(AnimationPhase phase)
        {
            currentPhase = phase;
            currentPhaseCallback?.Invoke(phase);
            OnPhaseChanged?.Invoke(phase);
        }

        private void ReturnToCombatIdle()
        {
            if (currentWeaponAnimSet != null && !string.IsNullOrEmpty(currentWeaponAnimSet.CombatIdle))
            {
                animator.CrossFade(currentWeaponAnimSet.CombatIdle, defaultCrossFade);
            }
            else
            {

                animator.CrossFade("Unarmed_Idle", defaultCrossFade);
            }
        }

        public void UpdateCombatLocomotion(Vector2 moveDirection, float speed)
        {
            if (currentWeaponAnimSet == null || speed < 0.01f)
            {

                UpdateLocomotion(0f, 0f, 0f);
                return;
            }

            UpdateLocomotion(speed, moveDirection.x, moveDirection.y);
        }

        public bool HasState(string stateName, int layer = 0)
        {
            int hash = Animator.StringToHash(stateName);
            return animator.HasState(layer, hash);
        }

        public Animator GetRawAnimator() => animator;

    }
}
