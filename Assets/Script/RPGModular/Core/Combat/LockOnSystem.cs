using System;
using System.Collections.Generic;
using UnityEngine;
using BillInspector;

namespace RPGModular
{
    public class LockOnSystem : MonoBehaviour, ILockOnSystem
    {
        [BillTitle("Lock-On System")]
        [BillBoxGroup("Settings")]
        [BillSlider(5f, 30f), BillSuffix("m")]
        [SerializeField] private float searchRadius = 15f;
        [BillBoxGroup("Settings")]
        [BillSlider(5f, 40f), BillSuffix("m")]
        [SerializeField] private float maxLockDistance = 20f;
        [BillBoxGroup("Settings")]
        [BillSlider(0f, 2f), BillSuffix("s")]
        [SerializeField] private float lockLostDelay = 0.5f;
        [BillBoxGroup("Settings")]
        [SerializeField] private LayerMask targetLayer;

        [BillBoxGroup("Angle Filter")]
        [SerializeField] private bool useAngleFilter = true;
        [BillBoxGroup("Angle Filter")]
        [BillShowIf("useAngleFilter"), BillSlider(10f, 180f), BillSuffix("°")]
        [SerializeField] private float searchAngle = 60f;

        // Cached camera reference (avoid Camera.main every frame)
        private Transform cameraTransform;

        private ITargetLockable currentTarget;
        private Transform currentTargetTransform;
        private float lockLostTimer;
        private List<ITargetLockable> nearbyTargets = new List<ITargetLockable>();

        // Pre-allocated buffer for OverlapSphereNonAlloc
        private static readonly Collider[] _overlapBuffer = new Collider[32];

        public ITargetLockable CurrentTarget => currentTarget;
        public bool IsLockedOn => currentTarget != null;

        public Transform CurrentTargetTransform => currentTargetTransform;
        public float DistanceToTarget => currentTargetTransform != null
            ? Vector3.Distance(transform.position, currentTargetTransform.position)
            : float.MaxValue;

        public event Action<ITargetLockable> OnTargetLocked;
        public event Action OnTargetLost;

        private void Awake()
        {
            CacheCamera();
        }

        private void CacheCamera()
        {
            var cam = Camera.main;
            if (cam != null)
                cameraTransform = cam.transform;
        }

        private void Update()
        {
            if (cameraTransform == null)
                CacheCamera();

            if (!IsLockedOn) return;

            ValidateCurrentTarget();
        }

        public void LockOn(ITargetLockable target)
        {
            if (target == null || !target.CanBeLocked) return;

            currentTarget = target;
            currentTargetTransform = target.LockOnPoint;
            lockLostTimer = 0f;

            OnTargetLocked?.Invoke(target);
        }

        public void AutoLockNearest()
        {
            RefreshNearbyTargets();

            if (nearbyTargets.Count == 0) return;

            ITargetLockable best = null;
            float bestScore = float.MaxValue;

            foreach (var target in nearbyTargets)
            {
                if (target.LockOnPoint == null) continue;

                float dist = Vector3.Distance(transform.position, target.LockOnPoint.position);
                float angle = GetAngleToTarget(target.LockOnPoint.position);

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

        public void LockOff()
        {
            if (!IsLockedOn) return;

            currentTarget = null;
            currentTargetTransform = null;
            nearbyTargets.Clear();

            OnTargetLost?.Invoke();
        }

        public void ToggleLock()
        {
            if (IsLockedOn)
                LockOff();
            else
                AutoLockNearest();
        }

        public void SwitchTarget(int direction)
        {
            if (!IsLockedOn) return;

            RefreshNearbyTargets();
            if (nearbyTargets.Count <= 1) return;

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

        private void ValidateCurrentTarget()
        {
            bool shouldLose = false;

            if (currentTargetTransform == null)
            {
                shouldLose = true;
            }

            else if (currentTarget is IDamageable damageable && !damageable.IsAlive)
            {
                shouldLose = true;
            }

            else if (DistanceToTarget > maxLockDistance)
            {
                shouldLose = true;
            }

            else if (!currentTarget.CanBeLocked)
            {
                shouldLose = true;
            }

            if (shouldLose)
            {
                lockLostTimer += Time.deltaTime;
                if (lockLostTimer >= lockLostDelay)
                {

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

        private void RefreshNearbyTargets()
        {
            nearbyTargets.Clear();

            int count = Physics.OverlapSphereNonAlloc(transform.position, searchRadius, _overlapBuffer, targetLayer);

            for (int i = 0; i < count; i++)
            {
                var hit = _overlapBuffer[i];
                if (hit.transform == transform) continue;

                var lockable = hit.GetComponent<ITargetLockable>();
                if (lockable == null || !lockable.CanBeLocked) continue;

                var damageable = hit.GetComponent<IDamageable>();
                if (damageable != null && !damageable.IsAlive) continue;

                if (useAngleFilter && lockable.LockOnPoint != null)
                {
                    float angle = GetAngleToTarget(lockable.LockOnPoint.position);
                    if (angle > searchAngle) continue;
                }

                nearbyTargets.Add(lockable);
            }
        }

        public List<ITargetLockable> GetNearbyTargets()
        {
            RefreshNearbyTargets();
            return new List<ITargetLockable>(nearbyTargets);
        }

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

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {

            Gizmos.color = new Color(1f, 1f, 0f, 0.1f);
            Gizmos.DrawWireSphere(transform.position, searchRadius);

            Gizmos.color = new Color(1f, 0f, 0f, 0.08f);
            Gizmos.DrawWireSphere(transform.position, maxLockDistance);

            if (IsLockedOn && currentTargetTransform != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position + Vector3.up,
                               currentTargetTransform.position + Vector3.up);
                Gizmos.DrawWireSphere(currentTargetTransform.position + Vector3.up, 0.3f);
            }

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
