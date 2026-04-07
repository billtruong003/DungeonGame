// File: Core/Combat/CombatLocomotion.cs
// Xử lý di chuyển trong trạng thái chiến đấu
// Lock-on target: luôn nhìn vào quái, tốc độ khác nhau theo hướng
using UnityEngine;

namespace RPGModular
{
    [RequireComponent(typeof(CharacterController))]
    public class CombatLocomotion : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private CharacterController controller;
        [SerializeField] private AnimationController animController;

        [Header("Combat Movement")]
        [SerializeField] private float combatMoveSpeed = 4f;
        [SerializeField] private float approachSpeedMultiplier = 1.0f;     // Chạy lại gần = 100%
        [SerializeField] private float retreatSpeedMultiplier = 0.75f;     // Lùi ra xa = 75%
        [SerializeField] private float strafeSpeedMultiplier = 0.85f;      // Đi ngang = 85%
        [SerializeField] private float rushSpeedMultiplier = 1.3f;         // Buff chạy lại gần = 130%

        [Header("Lock-On")]
        [SerializeField] private float rotationSpeed = 15f;
        [SerializeField] private float lockOnMaxDistance = 20f;

        [Header("Knockback")]
        [SerializeField] private float knockbackDecay = 5f;

        // State
        private Transform lockOnTarget;
        private bool isLockedOn;
        private Vector3 knockbackVelocity;
        private bool isRushing;    // Buff chạy nhanh lại gần

        // Properties
        public bool IsLockedOn => isLockedOn;
        public Transform LockOnTarget => lockOnTarget;

        private void Awake()
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            if (animController == null) animController = GetComponentInChildren<AnimationController>();
        }

        #region Lock-On

        public void SetLockOnTarget(Transform target)
        {
            lockOnTarget = target;
            isLockedOn = target != null;
            
            if (animController != null)
                animController.SetCombatMode(isLockedOn);
        }

        public void ClearLockOn()
        {
            lockOnTarget = null;
            isLockedOn = false;
            
            if (animController != null)
                animController.SetCombatMode(false);
        }

        #endregion

        #region Combat Movement

        /// <summary>
        /// Di chuyển trong combat. Input là world-space direction (đã qua camera transform).
        /// Tốc độ thay đổi theo hướng relative to target:
        /// - Tiến lại gần: 100% (hoặc 130% nếu rush)
        /// - Lùi ra xa: 75%
        /// - Đi ngang: 85%
        /// </summary>
        public void HandleCombatMovement(Vector2 moveInput, float baseSpeed = -1f)
        {
            if (!isLockedOn || lockOnTarget == null) return;

            float speed = baseSpeed > 0 ? baseSpeed : combatMoveSpeed;

            // Luôn nhìn vào target
            RotateTowardsTarget();

            if (moveInput.magnitude < 0.01f)
            {
                // Đứng yên
                animController?.UpdateCombatLocomotion(Vector2.zero, 0f);
                return;
            }

            // Tính hướng di chuyển relative to hướng nhìn vào target
            Vector3 toTarget = (lockOnTarget.position - transform.position).normalized;
            toTarget.y = 0;
            
            Vector3 forward = toTarget;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            // Input thành world direction
            Vector3 moveDir = (forward * moveInput.y + right * moveInput.x).normalized;

            // Tính dot product với hướng tới target để biết đang tiến/lùi/ngang
            float forwardDot = Vector3.Dot(moveDir, forward);
            
            float speedMultiplier;
            if (forwardDot > 0.5f)
            {
                // Đang tiến lại gần
                speedMultiplier = isRushing ? rushSpeedMultiplier : approachSpeedMultiplier;
            }
            else if (forwardDot < -0.3f)
            {
                // Đang lùi
                speedMultiplier = retreatSpeedMultiplier;
            }
            else
            {
                // Đi ngang (strafe)
                speedMultiplier = strafeSpeedMultiplier;
            }

            float finalSpeed = speed * speedMultiplier;

            // Áp dụng di chuyển + knockback
            Vector3 totalMovement = moveDir * finalSpeed + knockbackVelocity;
            controller.Move(totalMovement * Time.deltaTime);

            // Decay knockback
            if (knockbackVelocity.magnitude > 0.1f)
            {
                knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, knockbackDecay * Time.deltaTime);
            }
            else
            {
                knockbackVelocity = Vector3.zero;
            }

            // Update animation - truyền local direction cho blend tree
            Vector2 localMoveDir = new Vector2(
                Vector3.Dot(moveDir, right),    // X: trái/phải
                Vector3.Dot(moveDir, forward)   // Y: tiến/lùi
            );
            animController?.UpdateCombatLocomotion(localMoveDir, finalSpeed / combatMoveSpeed);
        }

        /// <summary>
        /// Xoay nhân vật luôn nhìn vào target
        /// </summary>
        private void RotateTowardsTarget()
        {
            if (lockOnTarget == null) return;

            Vector3 directionToTarget = lockOnTarget.position - transform.position;
            directionToTarget.y = 0;

            if (directionToTarget.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, targetRotation, 
                    Time.deltaTime * rotationSpeed);
            }
        }

        #endregion

        #region Knockback

        /// <summary>
        /// Áp dụng knockback (từ block heavy attack, hoặc bị đánh mạnh)
        /// </summary>
        public void ApplyKnockback(Vector3 direction, float force)
        {
            knockbackVelocity = direction.normalized * force;
        }

        #endregion

        #region Rush (Buff tốc độ chạy lại gần)

        public void SetRushing(bool rushing)
        {
            isRushing = rushing;
        }

        #endregion

        #region Kiểm tra khoảng cách

        /// <summary>
        /// Khoảng cách đến target hiện tại
        /// </summary>
        public float DistanceToTarget()
        {
            if (lockOnTarget == null) return float.MaxValue;
            return Vector3.Distance(transform.position, lockOnTarget.position);
        }

        /// <summary>
        /// Kiểm tra target còn trong range lock-on không
        /// </summary>
        public bool IsTargetInRange()
        {
            return DistanceToTarget() <= lockOnMaxDistance;
        }

        #endregion

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!isLockedOn || lockOnTarget == null) return;

            // Vẽ line tới target
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position + Vector3.up, lockOnTarget.position + Vector3.up);

            // Vẽ vòng lock-on range
            Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, lockOnMaxDistance);

            // Vẽ hướng di chuyển
            if (knockbackVelocity.magnitude > 0.1f)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(transform.position + Vector3.up, knockbackVelocity);
            }
        }
#endif
    }
}
