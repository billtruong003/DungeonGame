// File: Core/Animation/AnimationController.cs
// CORE DESIGN: Animator được điều khiển 100% bằng code
// Không nối state machine trong Unity Editor
// Mọi transition đều qua CrossFade với priority check
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
        [SerializeField] private float priorityResetDelay = 0.1f; // Delay trước khi reset priority

        // Animator parameter hashes (cache để tối ưu)
        private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveYHash = Animator.StringToHash("MoveY");
        private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
        private static readonly int InCombatHash = Animator.StringToHash("InCombat");

        // State tracking
        private AnimationPriority currentPriority = AnimationPriority.Locomotion;
        private AnimationPhase currentPhase = AnimationPhase.Done;
        private AnimationActionData currentAction;
        private Coroutine actionCoroutine;
        private Action<AnimationPhase> currentPhaseCallback;

        // Weapon state - quyết định idle nào return về
        private WeaponAnimationSet currentWeaponAnimSet;
        private bool isInCombat;

        // Events
        public event Action OnActionComplete;
        public event Action<AnimationPhase> OnPhaseChanged;

        // Properties
        public AnimationPriority CurrentPriority => currentPriority;
        public AnimationPhase CurrentPhase => currentPhase;
        public bool CanBeInterrupted => currentPhase == AnimationPhase.Startup 
                                        && currentAction != null 
                                        && currentAction.CanCancelStartup
                                     || currentPhase == AnimationPhase.Recovery 
                                        && currentAction != null 
                                        && currentAction.CanCancelRecovery
                                     || currentPhase == AnimationPhase.Done;

        #region Initialization

        private void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();
        }

        /// <summary>
        /// Set bộ animation cho vũ khí hiện tại.
        /// Gọi khi equip/unequip vũ khí.
        /// </summary>
        public void SetWeaponAnimationSet(WeaponAnimationSet animSet)
        {
            currentWeaponAnimSet = animSet;
            
            // Nếu đang ở combat idle, switch sang idle mới ngay
            if (isInCombat && currentPhase == AnimationPhase.Done 
                && currentPriority <= AnimationPriority.CombatIdle)
            {
                ReturnToCombatIdle();
            }
        }

        /// <summary>
        /// Bật/tắt trạng thái chiến đấu.
        /// Combat = true → dùng combat idle/walk từ weapon anim set.
        /// </summary>
        public void SetCombatMode(bool combat)
        {
            isInCombat = combat;
            animator.SetBool(InCombatHash, combat);
            
            if (combat && currentWeaponAnimSet != null)
            {
                ReturnToCombatIdle();
            }
        }

        #endregion

        #region IAnimationController - Play Animation

        public bool PlayAnimation(string stateName, AnimationPriority priority, 
                                  float crossFadeDuration = 0.1f, int layer = 0)
        {
            // Priority check: chỉ play nếu >= current priority
            // Hoặc nếu current action đã Done
            if (priority < currentPriority && currentPhase != AnimationPhase.Done)
                return false;

            // Cancel action hiện tại nếu có
            if (actionCoroutine != null)
            {
                StopCoroutine(actionCoroutine);
                actionCoroutine = null;
            }

            currentPriority = priority;
            currentPhase = AnimationPhase.Done; // Simple animation, no phases
            currentAction = null;

            animator.CrossFade(stateName, crossFadeDuration, layer);
            return true;
        }

        public bool PlayAction(AnimationActionData actionData, AnimationPriority priority,
                              Action<AnimationPhase> onPhaseChanged = null)
        {
            // Priority check
            if (priority < currentPriority && !CanBeInterrupted)
                return false;

            // Cancel previous action
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

            // Start phase tracking coroutine
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

            currentPriority = AnimationPriority.Death; // Max priority
            currentPhase = AnimationPhase.Done;
            currentAction = null;

            animator.CrossFade(stateName, crossFadeDuration, layer);
        }

        #endregion

        #region Parameters

        public void SetFloat(string paramName, float value, float dampTime = 0.1f)
        {
            animator.SetFloat(Animator.StringToHash(paramName), value, dampTime, Time.deltaTime);
        }

        public void SetBool(string paramName, bool value)
        {
            animator.SetBool(Animator.StringToHash(paramName), value);
        }

        /// <summary>
        /// Update locomotion blend tree parameters.
        /// Tách riêng vì gọi rất thường xuyên (mỗi frame).
        /// </summary>
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
            return stateInfo.normalizedTime % 1f; // Loop-safe
        }

        #endregion

        #region Phase Tracking

        /// <summary>
        /// Coroutine theo dõi animation phases: Startup → Active → Recovery → Done.
        /// Đây là trái tim của combat animation timing.
        /// </summary>
        private IEnumerator TrackActionPhases(AnimationActionData actionData)
        {
            // Wait 1 frame cho CrossFade bắt đầu
            yield return null;

            // === STARTUP PHASE ===
            SetPhase(AnimationPhase.Startup);
            while (GetNormalizedTime(actionData.AnimatorLayer) < actionData.StartupEnd)
            {
                yield return null;
            }

            // === ACTIVE PHASE === (hitbox ON)
            SetPhase(AnimationPhase.Active);
            while (GetNormalizedTime(actionData.AnimatorLayer) < actionData.ActiveEnd)
            {
                yield return null;
            }

            // === RECOVERY PHASE ===
            SetPhase(AnimationPhase.Recovery);
            while (GetNormalizedTime(actionData.AnimatorLayer) < 0.95f) // Gần cuối animation
            {
                yield return null;
            }

            // === DONE ===
            SetPhase(AnimationPhase.Done);
            currentAction = null;
            currentPhaseCallback = null;
            actionCoroutine = null;

            // Reset priority sau delay nhỏ (cho phép chain combo input)
            yield return new WaitForSeconds(priorityResetDelay);
            
            if (currentPhase == AnimationPhase.Done) // Vẫn Done, chưa bị interrupt
            {
                currentPriority = isInCombat ? AnimationPriority.CombatIdle : AnimationPriority.Locomotion;
                
                // Return về idle phù hợp
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

        /// <summary>
        /// Return về combat idle dựa trên vũ khí đang cầm.
        /// Đây là behavior bạn mô tả: "skill xong, cầm vũ khí nào thì về idle đó"
        /// </summary>
        private void ReturnToCombatIdle()
        {
            if (currentWeaponAnimSet != null && !string.IsNullOrEmpty(currentWeaponAnimSet.CombatIdle))
            {
                animator.CrossFade(currentWeaponAnimSet.CombatIdle, defaultCrossFade);
            }
            else
            {
                // Fallback: Unarmed combat idle
                animator.CrossFade("Unarmed_Idle", defaultCrossFade);
            }
        }

        #endregion

        #region Combat Locomotion Helper

        /// <summary>
        /// Update combat walk animation dựa trên hướng di chuyển relative to target.
        /// Forward/Back/Left/Right sẽ play animation khác nhau.
        /// </summary>
        public void UpdateCombatLocomotion(Vector2 moveDirection, float speed)
        {
            if (currentWeaponAnimSet == null || speed < 0.01f)
            {
                // Đứng yên → combat idle (đã handle ở ReturnToCombatIdle)
                UpdateLocomotion(0f, 0f, 0f);
                return;
            }

            // Truyền direction cho blend tree
            UpdateLocomotion(speed, moveDirection.x, moveDirection.y);
        }

        #endregion

        #region Utility

        /// <summary>
        /// Kiểm tra animator có state này không (debug helper).
        /// </summary>
        public bool HasState(string stateName, int layer = 0)
        {
            int hash = Animator.StringToHash(stateName);
            return animator.HasState(layer, hash);
        }

        /// <summary>
        /// Lấy Animator gốc - chỉ dùng khi thực sự cần (vd: Animation Event).
        /// </summary>
        public Animator GetRawAnimator() => animator;

        #endregion
    }
}
