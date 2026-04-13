using UnityEngine;
using BillInspector;

namespace RPGModular
{
    [RequireComponent(typeof(CharacterController))]
    public class CombatLocomotion : MonoBehaviour
    {
        [BillTitle("Combat Locomotion")]
        [BillBoxGroup("Dependencies")]
        [SerializeField] private CharacterController controller;
        [BillBoxGroup("Dependencies")]
        [SerializeField] private AnimationController animController;

        [BillBoxGroup("Combat Movement")]
        [BillSlider(1f, 10f), BillSuffix("m/s")]
        [SerializeField] private float combatMoveSpeed = 4f;
        [BillBoxGroup("Combat Movement")]
        [BillSlider(0.5f, 2f)]
        [SerializeField] private float approachSpeedMultiplier = 1.0f;
        [BillBoxGroup("Combat Movement")]
        [BillSlider(0.3f, 1.5f)]
        [SerializeField] private float retreatSpeedMultiplier = 0.75f;
        [BillBoxGroup("Combat Movement")]
        [BillSlider(0.3f, 1.5f)]
        [SerializeField] private float strafeSpeedMultiplier = 0.85f;
        [BillBoxGroup("Combat Movement")]
        [BillSlider(0.5f, 2f)]
        [SerializeField] private float rushSpeedMultiplier = 1.3f;

        [BillBoxGroup("Lock-On")]
        [BillSlider(5f, 30f)]
        [SerializeField] private float rotationSpeed = 15f;
        [BillBoxGroup("Lock-On")]
        [BillSlider(5f, 40f), BillSuffix("m")]
        [SerializeField] private float lockOnMaxDistance = 20f;

        [BillBoxGroup("Knockback")]
        [BillSlider(1f, 15f)]
        [SerializeField] private float knockbackDecay = 5f;

        private Transform lockOnTarget;
        private bool isLockedOn;
        private Vector3 knockbackVelocity;
        private bool isRushing;

        public bool IsLockedOn => isLockedOn;
        public Transform LockOnTarget => lockOnTarget;

        private void Awake()
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            if (animController == null) animController = GetComponentInChildren<AnimationController>();
        }

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

        public void HandleCombatMovement(Vector2 moveInput, float baseSpeed = -1f)
        {
            if (!isLockedOn || lockOnTarget == null) return;

            float speed = baseSpeed > 0 ? baseSpeed : combatMoveSpeed;

            RotateTowardsTarget();

            if (moveInput.magnitude < 0.01f)
            {

                animController?.UpdateCombatLocomotion(Vector2.zero, 0f);
                return;
            }

            Vector3 toTarget = (lockOnTarget.position - transform.position).normalized;
            toTarget.y = 0;

            Vector3 forward = toTarget;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            Vector3 moveDir = (forward * moveInput.y + right * moveInput.x).normalized;

            float forwardDot = Vector3.Dot(moveDir, forward);

            float speedMultiplier;
            if (forwardDot > 0.5f)
            {

                speedMultiplier = isRushing ? rushSpeedMultiplier : approachSpeedMultiplier;
            }
            else if (forwardDot < -0.3f)
            {

                speedMultiplier = retreatSpeedMultiplier;
            }
            else
            {

                speedMultiplier = strafeSpeedMultiplier;
            }

            float finalSpeed = speed * speedMultiplier;

            Vector3 totalMovement = moveDir * finalSpeed + knockbackVelocity;
            controller.Move(totalMovement * Time.deltaTime);

            if (knockbackVelocity.magnitude > 0.1f)
            {
                knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, knockbackDecay * Time.deltaTime);
            }
            else
            {
                knockbackVelocity = Vector3.zero;
            }

            Vector2 localMoveDir = new Vector2(
                Vector3.Dot(moveDir, right),
                Vector3.Dot(moveDir, forward)
            );
            animController?.UpdateCombatLocomotion(localMoveDir, finalSpeed / combatMoveSpeed);
        }

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

        public void ApplyKnockback(Vector3 direction, float force)
        {
            knockbackVelocity = direction.normalized * force;
        }

        public void SetRushing(bool rushing)
        {
            isRushing = rushing;
        }

        public float DistanceToTarget()
        {
            if (lockOnTarget == null) return float.MaxValue;
            return Vector3.Distance(transform.position, lockOnTarget.position);
        }

        public bool IsTargetInRange()
        {
            return DistanceToTarget() <= lockOnMaxDistance;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!isLockedOn || lockOnTarget == null) return;

            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position + Vector3.up, lockOnTarget.position + Vector3.up);

            Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, lockOnMaxDistance);

            if (knockbackVelocity.magnitude > 0.1f)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(transform.position + Vector3.up, knockbackVelocity);
            }
        }
#endif
    }
}
