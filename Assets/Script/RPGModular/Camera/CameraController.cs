using UnityEngine;
using BillInspector;

namespace RPGModular
{
    public enum CameraMode
    {
        FreeLook,
        Combat
    }

    public class CameraController : MonoBehaviour
    {
        [BillTitle("Camera Controller")]
        [BillBoxGroup("Target")]
        [BillRequired]
        [SerializeField] private Transform target;
        [BillBoxGroup("Target")]
        [SerializeField] private Vector3 shoulderOffset = new Vector3(0.5f, 1.6f, 0f);

        [BillFoldoutGroup("Free Look")]
        [BillSlider(1f, 15f), BillSuffix("m")]
        [SerializeField] private float freeDistance = 5f;
        [BillFoldoutGroup("Free Look")]
        [BillSlider(0.5f, 10f)]
        [SerializeField] private float freeSensitivity = 3f;
        [BillFoldoutGroup("Free Look")]
        [BillSlider(-90f, 0f), BillSuffix("°")]
        [SerializeField] private float freeMinPitch = -30f;
        [BillFoldoutGroup("Free Look")]
        [BillSlider(0f, 90f), BillSuffix("°")]
        [SerializeField] private float freeMaxPitch = 60f;

        [BillFoldoutGroup("Combat Camera")]
        [BillSlider(1f, 10f), BillSuffix("m")]
        [SerializeField] private float combatDistance = 4f;
        [BillFoldoutGroup("Combat Camera")]
        [SerializeField] private Vector3 combatShoulderOffset = new Vector3(0.8f, 1.5f, 0f);
        [BillFoldoutGroup("Combat Camera")]
        [BillSlider(0.5f, 10f)]
        [SerializeField] private float combatSensitivity = 2f;
        [BillFoldoutGroup("Combat Camera")]
        [BillSlider(-90f, 0f), BillSuffix("°")]
        [SerializeField] private float combatMinPitch = -20f;
        [BillFoldoutGroup("Combat Camera")]
        [BillSlider(0f, 90f), BillSuffix("°")]
        [SerializeField] private float combatMaxPitch = 45f;
        [BillFoldoutGroup("Combat Camera")]
        [BillSlider(1f, 20f)]
        [SerializeField] private float lockOnLerpSpeed = 8f;

        [BillFoldoutGroup("Shared Settings")]
        [BillSlider(1f, 30f)]
        [SerializeField] private float smoothSpeed = 10f;
        [BillFoldoutGroup("Shared Settings")]
        [BillSlider(0.05f, 1f), BillSuffix("m")]
        [SerializeField] private float collisionRadius = 0.3f;
        [BillFoldoutGroup("Shared Settings")]
        [SerializeField] private LayerMask collisionMask = ~0;
        [BillFoldoutGroup("Shared Settings")]
        [BillSlider(1f, 15f)]
        [SerializeField] private float modeTransitionSpeed = 5f;

        private CameraMode currentMode = CameraMode.FreeLook;
        private float yaw;
        private float pitch;
        private float currentDistance;
        private Transform lockOnTarget;
        private Vector3 currentOffset;
        private float targetDistance;

        public CameraMode CurrentMode => currentMode;

        private void Start()
        {
            if (target == null) return;
            Vector3 angles = transform.eulerAngles;
            yaw = angles.y;
            pitch = angles.x;
            currentDistance = freeDistance;
            currentOffset = shoulderOffset;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            switch (currentMode)
            {
                case CameraMode.FreeLook:
                    UpdateFreeLook();
                    break;
                case CameraMode.Combat:
                    UpdateCombat();
                    break;
            }
        }

        public void SetMode(CameraMode mode)
        {
            currentMode = mode;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        public void SetLockOnTarget(Transform lockTarget)
        {
            lockOnTarget = lockTarget;
        }

        public void ClearLockOnTarget()
        {
            lockOnTarget = null;
        }

        private void UpdateFreeLook()
        {
            if (Input.GetMouseButton(1))
            {
                yaw += Input.GetAxis("Mouse X") * freeSensitivity;
                pitch -= Input.GetAxis("Mouse Y") * freeSensitivity;
                pitch = Mathf.Clamp(pitch, freeMinPitch, freeMaxPitch);
            }

            currentOffset = Vector3.Lerp(currentOffset, shoulderOffset, modeTransitionSpeed * Time.deltaTime);
            targetDistance = freeDistance;
            currentDistance = Mathf.Lerp(currentDistance, targetDistance, smoothSpeed * Time.deltaTime);

            ApplyCameraPosition();
        }

        private void UpdateCombat()
        {
            if (lockOnTarget != null)
            {
                Vector3 directionToTarget = lockOnTarget.position - target.position;
                directionToTarget.y = 0f;

                if (directionToTarget.sqrMagnitude > 0.01f)
                {
                    float targetYaw = Quaternion.LookRotation(directionToTarget).eulerAngles.y;
                    yaw = Mathf.LerpAngle(yaw, targetYaw, lockOnLerpSpeed * Time.deltaTime);
                }

                Vector3 toTarget = lockOnTarget.position - target.position;
                float verticalAngle = -Mathf.Atan2(toTarget.y - 1f, toTarget.magnitude) * Mathf.Rad2Deg;
                pitch = Mathf.Lerp(pitch, Mathf.Clamp(verticalAngle, combatMinPitch, combatMaxPitch),
                    lockOnLerpSpeed * Time.deltaTime);
            }
            else
            {
                yaw += Input.GetAxis("Mouse X") * combatSensitivity;
                pitch -= Input.GetAxis("Mouse Y") * combatSensitivity;
                pitch = Mathf.Clamp(pitch, combatMinPitch, combatMaxPitch);
            }

            currentOffset = Vector3.Lerp(currentOffset, combatShoulderOffset, modeTransitionSpeed * Time.deltaTime);
            targetDistance = combatDistance;
            currentDistance = Mathf.Lerp(currentDistance, targetDistance, smoothSpeed * Time.deltaTime);

            ApplyCameraPosition();
        }

        private void ApplyCameraPosition()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivotPoint = target.position + target.TransformDirection(new Vector3(0f, currentOffset.y, 0f));
            Vector3 desiredPosition = pivotPoint - rotation * Vector3.forward * currentDistance
                + rotation * new Vector3(currentOffset.x, 0f, 0f);

            float adjustedDistance = currentDistance;
            if (Physics.SphereCast(pivotPoint, collisionRadius, desiredPosition - pivotPoint,
                out RaycastHit hit, currentDistance, collisionMask, QueryTriggerInteraction.Ignore))
            {
                adjustedDistance = hit.distance - collisionRadius;
                adjustedDistance = Mathf.Max(adjustedDistance, 0.5f);
            }

            Vector3 finalPosition = pivotPoint - rotation * Vector3.forward * adjustedDistance
                + rotation * new Vector3(currentOffset.x, 0f, 0f);

            transform.position = Vector3.Lerp(transform.position, finalPosition, smoothSpeed * Time.deltaTime);
            transform.rotation = rotation;
        }
    }
}
