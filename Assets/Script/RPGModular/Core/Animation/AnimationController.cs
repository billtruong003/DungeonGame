using System;
using System.Collections;
using UnityEngine;
using BillInspector;

namespace RPGModular
{
    [RequireComponent(typeof(Animator))]
    public class AnimationController : MonoBehaviour, IAnimationController
    {
        [BillTitle("Animation Controller")]
        [BillBoxGroup("Dependencies")]
        [BillRequired]
        [SerializeField] private Animator animator;

        [BillBoxGroup("Settings")]
        [BillSlider(0.01f, 0.5f), BillSuffix("s")]
        [SerializeField] private float defaultCrossFade = 0.1f;
        [BillBoxGroup("Settings")]
        [BillSlider(0.01f, 0.5f), BillSuffix("s")]
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

            // Set phase ngay lập tức — không đợi coroutine frame sau
            // Để các state check CurrentPhase trong cùng frame không bị Done sớm
            SetPhase(AnimationPhase.Startup);

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

        /// <summary>
        /// Track animation phases bằng TIMER, không dùng normalizedTime.
        /// normalizedTime bị sai khi CrossFade chưa hoàn tất (đọc state cũ).
        /// Timer = tổng duration tính từ clip length hoặc ActionData thresholds.
        /// </summary>
        private IEnumerator TrackActionPhases(AnimationActionData actionData)
        {
            // Lấy clip duration — ước tính từ thresholds
            // StartupEnd/ActiveEnd là normalized (0-1), nhân với thời gian thực
            // Nếu không biết clip length, dùng thresholds trực tiếp (giả định clip ~1s)
            float clipLength = GetClipLength(actionData.AnimationStateName, actionData.AnimatorLayer);
            if (clipLength <= 0f) clipLength = 1f; // fallback

            float startupDuration = actionData.StartupEnd * clipLength;
            float activeDuration = (actionData.ActiveEnd - actionData.StartupEnd) * clipLength;
            float recoveryDuration = (0.95f - actionData.ActiveEnd) * clipLength;

            // Phase: Startup
            // (SetPhase(Startup) đã gọi trong PlayAction rồi)
            float timer = 0f;
            while (timer < startupDuration)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            // Phase: Active (hitbox window)
            SetPhase(AnimationPhase.Active);
            timer = 0f;
            while (timer < activeDuration)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            // Phase: Recovery
            SetPhase(AnimationPhase.Recovery);
            timer = 0f;
            while (timer < recoveryDuration)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            // Phase: Done
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

        /// <summary>
        /// Lấy clip length từ Animator. Fallback 1s nếu không tìm được.
        /// </summary>
        private float GetClipLength(string stateName, int layer = 0)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                return 1f;

            // Tìm clip theo tên trong tất cả clips của controller
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name == stateName)
                    return clip.length;
            }

            return 1f; // fallback
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

                animator.CrossFade("CombatStrafe", defaultCrossFade);
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
