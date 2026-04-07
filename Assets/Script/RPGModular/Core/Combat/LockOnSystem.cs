// File: Core/Combat/LockOnSystem.cs
// Hệ thống Lock-On Target tách riêng
// Tìm target, switch target, auto-lose, camera hint
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGModular
{
    public class LockOnSystem : MonoBehaviour, ILockOnSystem
    {
        [Header("Settings")]
        [SerializeField] private float searchRadius = 15f;
        [SerializeField] private float maxLockDistance = 20f;
        [SerializeField] private float lockLostDelay = 0.5f;    // Delay trước khi mất lock (tránh flicker)
        [SerializeField] private LayerMask targetLayer;         // Layer của enemy

        [Header("Angle Filter")]
        [SerializeField] private float searchAngle = 60f;       // Chỉ lock target trong 60° phía trước
        [SerializeField] private bool useAngleFilter = true;

        // Dependencies
        private Transform cameraTransform;

        // State
        private ITargetLockable currentTarget;
        private Transform currentTargetTransform;
        private float lockLostTimer;
        private List<ITargetLockable> nearbyTargets = new List<ITargetLockable>();

        // ILockOnSystem
        public ITargetLockable CurrentTarget => currentTarget;
        public bool IsLockedOn => currentTarget != null;

        // Extra info
        public Transform CurrentTargetTransform => currentTargetTransform;
        public float DistanceToTarget => currentTargetTransform != null 
            ? Vector3.Distance(transform.position, currentTargetTransform.position) 
            : float.MaxValue;

        // Events
        public event Action<ITargetLockable> OnTargetLocked;
        public event Action OnTargetLost;

        private void Awake()
        {
            cameraTransform = Camera.main?.transform;
        }

        private void Update()
        {
            if (!IsLockedOn) return;

            // Kiểm tra target còn valid không
            ValidateCurrentTarget();
        }

        #region Lock / Unlock

        /// <summary>
        /// Lock vào target cụ thể
        /// </summary>
        public void LockOn(ITargetLockable target)
        {
            if (target == null || !target.CanBeLocked) return;

            currentTarget = target;
            currentTargetTransform = target.LockOnPoint;
            lockLostTimer = 0f;

            OnTargetLocked?.Invoke(target);
        }

        /// <summary>
        /// Tự động tìm và lock target gần nhất phía trước
        /// </summary>
        public void AutoLockNearest()
        {
            RefreshNearbyTargets();

            if (nearbyTargets.Count == 0) return;

            // Tìm target gần nhất trong vùng nhìn
            ITargetLockable best = null;
            float bestScore = float.MaxValue;

            foreach (var target in nearbyTargets)
            {
                if (target.LockOnPoint == null) continue;

                float dist = Vector3.Distance(transform.position, target.LockOnPoint.position);
                float angle = GetAngleToTarget(target.LockOnPoint.position);

                // Score = distance + angle penalty (ưu tiên gần + ở giữa màn hình)
                float score = dist + angle * 0.1f;

                if (score < bestScore)
                {
                    bestScore = score;
                    best = target;
                }
            }

            if (best != null)
                LockOn(best);
        }

        /// <summary>
        /// Bỏ lock
        /// </summary>
        public void LockOff()
        {
            if (!IsLockedOn) return;

            currentTarget = null;
            currentTargetTransform = null;
            nearbyTargets.Clear();

            OnTargetLost?.Invoke();
        }

        /// <summary>
        /// Toggle lock-on: đang lock thì bỏ, chưa lock thì tự tìm
        /// </summary>
        public void ToggleLock()
        {
            if (IsLockedOn)
                LockOff();
            else
                AutoLockNearest();
        }

        #endregion

        #region Switch Target

        /// <summary>
        /// Switch sang target bên trái (-1) hoặc phải (1) relative to camera
        /// </summary>
        public void SwitchTarget(int direction)
        {
            if (!IsLockedOn) return;

            RefreshNearbyTargets();
            if (nearbyTargets.Count <= 1) return;

            // Sắp xếp targets theo góc relative to camera right vector
            Vector3 camRight = cameraTransform != null ? cameraTransform.right : transform.right;
            
            ITargetLockable bestNext = null;
            float bestAngleDiff = float.MaxValue;

            Vector3 currentDir = (currentTargetTransform.position - transform.position).normalized;
            float currentAngle = SignedAngle(currentDir, camRight);

            foreach (var target in nearbyTargets)
            {
                if (target == currentTarget) continue;
                if (target.LockOnPoint == null) continue;

                Vector3 dir = (target.LockOnPoint.position - transform.position).normalized;
                float angle = SignedAngle(dir, camRight);
                float diff = angle - currentAngle;

                // direction > 0 = phải (angle tăng), < 0 = trái (angle giảm)
                if (direction > 0 && diff > 5f && diff < bestAngleDiff)
                {
                    bestAngleDiff = diff;
                    bestNext = target;
                }
                else if (direction < 0 && diff < -5f && Mathf.Abs(diff) < bestAngleDiff)
                {
                    bestAngleDiff = Mathf.Abs(diff);
                    bestNext = target;
                }
            }

            if (bestNext != null)
                LockOn(bestNext);
        }

        #endregion

        #region Validation

        /// <summary>
        /// Kiểm tra target hiện tại còn valid không.
        /// Mất lock nếu: chết, quá xa, bị destroy.
        /// </summary>
        private void ValidateCurrentTarget()
        {
            bool shouldLose = false;

            // Target bị destroy
            if (currentTargetTransform == null)
            {
                shouldLose = true;
            }
            // Target chết
            else if (currentTarget is IDamageable damageable && !damageable.IsAlive)
            {
                shouldLose = true;
            }
            // Target quá xa
            else if (DistanceToTarget > maxLockDistance)
            {
                shouldLose = true;
            }
            // Target không thể lock nữa
            else if (!currentTarget.CanBeLocked)
            {
                shouldLose = true;
            }

            if (shouldLose)
            {
                lockLostTimer += Time.deltaTime;
                if (lockLostTimer >= lockLostDelay)
                {
                    // Thử switch sang target khác trước khi mất lock hoàn toàn
                    ITargetLockable fallback = FindFallbackTarget();
                    if (fallback != null)
                    {
                        LockOn(fallback);
                    }
                    else
                    {
                        LockOff();
                    }
                }
            }
            else
            {
                lockLostTimer = 0f;
            }
        }

        private ITargetLockable FindFallbackTarget()
        {
            RefreshNearbyTargets();
            foreach (var target in nearbyTargets)
            {
                if (target == currentTarget) continue;
                if (target.LockOnPoint == null) continue;
                return target;
            }
            return null;
        }

        #endregion

        #region Target Scanning

        /// <summary>
        /// Refresh danh sách target gần đây
        /// </summary>
        private void RefreshNearbyTargets()
        {
            nearbyTargets.Clear();

            Collider[] hits = Physics.OverlapSphere(transform.position, searchRadius, targetLayer);

            foreach (var hit in hits)
            {
                if (hit.transform == transform) continue;

                var lockable = hit.GetComponent<ITargetLockable>();
                if (lockable == null || !lockable.CanBeLocked) continue;

                // Kiểm tra entity còn sống
                var damageable = hit.GetComponent<IDamageable>();
                if (damageable != null && !damageable.IsAlive) continue;

                // Angle filter
                if (useAngleFilter && lockable.LockOnPoint != null)
                {
                    float angle = GetAngleToTarget(lockable.LockOnPoint.position);
                    if (angle > searchAngle) continue;
                }

                nearbyTargets.Add(lockable);
            }
        }

        /// <summary>
        /// Lấy danh sách target gần đây (cho UI hiển thị)
        /// </summary>
        public List<ITargetLockable> GetNearbyTargets()
        {
            RefreshNearbyTargets();
            return new List<ITargetLockable>(nearbyTargets);
        }

        #endregion

        #region Utility

        private float GetAngleToTarget(Vector3 targetPos)
        {
            Vector3 dir = (targetPos - transform.position).normalized;
            Vector3 forward = cameraTransform != null ? cameraTransform.forward : transform.forward;
            forward.y = 0;
            dir.y = 0;
            return Vector3.Angle(forward, dir);
        }

        private float SignedAngle(Vector3 dir, Vector3 reference)
        {
            dir.y = 0;
            reference.y = 0;
            return Vector3.SignedAngle(reference, dir, Vector3.up);
        }

        #endregion

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Search radius
            Gizmos.color = new Color(1f, 1f, 0f, 0.1f);
            Gizmos.DrawWireSphere(transform.position, searchRadius);

            // Max lock distance
            Gizmos.color = new Color(1f, 0f, 0f, 0.08f);
            Gizmos.DrawWireSphere(transform.position, maxLockDistance);

            // Lock-on line
            if (IsLockedOn && currentTargetTransform != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position + Vector3.up, 
                               currentTargetTransform.position + Vector3.up);
                Gizmos.DrawWireSphere(currentTargetTransform.position + Vector3.up, 0.3f);
            }

            // Angle filter cone
            if (useAngleFilter)
            {
                Vector3 forward = Application.isPlaying && cameraTransform != null 
                    ? cameraTransform.forward 
                    : transform.forward;
                forward.y = 0;
                forward.Normalize();

                Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
                Vector3 leftBound = Quaternion.Euler(0, -searchAngle, 0) * forward * searchRadius;
                Vector3 rightBound = Quaternion.Euler(0, searchAngle, 0) * forward * searchRadius;
                Gizmos.DrawLine(transform.position, transform.position + leftBound);
                Gizmos.DrawLine(transform.position, transform.position + rightBound);
            }
        }
#endif
    }
}
