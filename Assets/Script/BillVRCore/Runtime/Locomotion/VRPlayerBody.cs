using UnityEngine;

namespace BillVRCore.Locomotion
{
    [DefaultExecutionOrder(75)]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class VRPlayerBody : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform headCamera;
        [SerializeField] private Transform trackingContainer;

        [Header("Height")]
        [SerializeField] private bool autoAdjustHeight = true;
        [SerializeField] private Vector2 minMaxHeight = new(0.4f, 2.5f);
        [SerializeField] private float heightSmoothSpeed = 10f;

        [Header("Grounding")]
        [SerializeField] private float maxStepHeight = 0.3f;
        [SerializeField] private float maxSlopeAngle = 45f;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private float groundedDrag = 8f;
        [SerializeField] private float airDrag = 0.5f;
        [SerializeField] private float stepSmooth = 10f;

        public Rigidbody Rb { get; private set; }
        public CapsuleCollider Capsule { get; private set; }
        public bool IsGrounded { get; private set; }
        public Vector3 GroundNormal { get; private set; } = Vector3.up;
        public float CurrentHeight { get; private set; }
        public Vector3 HorizontalVelocity => new(Rb.linearVelocity.x, 0f, Rb.linearVelocity.z);
        public float HorizontalSpeed => HorizontalVelocity.magnitude;

        private float _targetCapsuleHeight;
        private Vector3 _lastHeadLocalPos;

        private void Awake()
        {
            Rb = GetComponent<Rigidbody>();
            Capsule = GetComponent<CapsuleCollider>();
            Rb.useGravity = true;
            Rb.isKinematic = false;
            Rb.interpolation = RigidbodyInterpolation.Interpolate;
            Rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Rb.constraints = RigidbodyConstraints.FreezeRotation;
            Rb.mass = 70f;
        }

        private void Start()
        {
            if (headCamera == null)
            {
                var cam = Camera.main;
                if (cam != null) headCamera = cam.transform;
            }
        }

        private void FixedUpdate()
        {
            CheckGround();
            Rb.linearDamping = IsGrounded ? groundedDrag : airDrag;
            SyncTrackingPosition();
            if (autoAdjustHeight) UpdateCapsuleHeight();
        }

        private void CheckGround()
        {
            float radius = Capsule.radius * 0.9f;
            Vector3 origin = transform.position + Vector3.up * (radius + 0.05f);

            IsGrounded = Physics.SphereCast(origin, radius, Vector3.down,
                out RaycastHit hit, maxStepHeight + 0.1f, groundMask, QueryTriggerInteraction.Ignore);

            if (IsGrounded)
            {
                GroundNormal = hit.normal;
                if (Vector3.Angle(Vector3.up, hit.normal) > maxSlopeAngle)
                    IsGrounded = false;
                else
                    HandleStepping(hit);
            }
            else
            {
                GroundNormal = Vector3.up;
            }
        }

        private void HandleStepping(RaycastHit groundHit)
        {
            float stepDelta = groundHit.point.y - transform.position.y;
            if (stepDelta > 0.01f && stepDelta <= maxStepHeight)
            {
                Vector3 pos = transform.position;
                pos.y = Mathf.Lerp(pos.y, groundHit.point.y, Time.fixedDeltaTime * stepSmooth);
                Rb.MovePosition(pos);
            }
        }

        private void SyncTrackingPosition()
        {
            if (headCamera == null || trackingContainer == null) return;

            Vector3 headLocal = trackingContainer.InverseTransformPoint(headCamera.position);
            Vector3 deltaXZ = new Vector3(headLocal.x - _lastHeadLocalPos.x, 0f,
                headLocal.z - _lastHeadLocalPos.z);

            if (deltaXZ.sqrMagnitude > 0.0001f)
            {
                Vector3 worldDelta = trackingContainer.TransformDirection(deltaXZ);
                Rb.MovePosition(Rb.position + worldDelta);
                trackingContainer.position -= worldDelta;
            }

            _lastHeadLocalPos = trackingContainer.InverseTransformPoint(headCamera.position);
        }

        private void UpdateCapsuleHeight()
        {
            if (headCamera == null) return;

            float headHeight = trackingContainer != null
                ? trackingContainer.InverseTransformPoint(headCamera.position).y
                : headCamera.localPosition.y;

            CurrentHeight = Mathf.Clamp(headHeight, minMaxHeight.x, minMaxHeight.y);
            float smoothed = Mathf.Lerp(Capsule.height, CurrentHeight, Time.fixedDeltaTime * heightSmoothSpeed);
            Capsule.height = smoothed;
            Capsule.center = new Vector3(0f, smoothed * 0.5f, 0f);
        }

        public void Teleport(Vector3 position)
        {
            Vector3 delta = position - transform.position;
            transform.position = position;
            if (trackingContainer != null) trackingContainer.position += delta;
            Rb.linearVelocity = Vector3.zero;
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            Teleport(position);
            float targetY = rotation.eulerAngles.y;
            Rotate(targetY - transform.eulerAngles.y);
        }

        public void Teleport(Transform target)
        {
            if (target == null) return;
            Teleport(target.position, target.rotation);
        }

        public void Rotate(float angle)
        {
            Vector3 pivot = headCamera != null ? headCamera.position : transform.position;
            transform.RotateAround(pivot, Vector3.up, angle);
        }

        public void Jump(float force)
        {
            if (!IsGrounded) return;
            Rb.AddForce(Vector3.up * force, ForceMode.Impulse);
        }

        public void AddForce(Vector3 force, ForceMode mode = ForceMode.Force)
        {
            Rb.AddForce(force, mode);
        }

        public void SetVelocity(Vector3 velocity)
        {
            Rb.linearVelocity = velocity;
        }

        public void SetHorizontalVelocity(Vector3 horizontalVelocity)
        {
            Rb.linearVelocity = new Vector3(horizontalVelocity.x, Rb.linearVelocity.y, horizontalVelocity.z);
        }

        public void StopMovement()
        {
            Rb.linearVelocity = Vector3.zero;
            Rb.angularVelocity = Vector3.zero;
        }

        public void SetHeadCamera(Transform cam) => headCamera = cam;
        public void SetTrackingContainer(Transform container) => trackingContainer = container;
        public void SetGroundedDrag(float drag) => groundedDrag = drag;
        public void SetAirDrag(float drag) => airDrag = drag;
    }
}
