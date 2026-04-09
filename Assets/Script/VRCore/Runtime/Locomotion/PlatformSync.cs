using UnityEngine;

namespace VRCore.Locomotion
{
    [RequireComponent(typeof(VRPlayerBody))]
    public class PlatformSync : MonoBehaviour
    {
        [SerializeField] private LayerMask platformLayers = ~0;
        [SerializeField] private float detectionRadius = 0.3f;

        public Transform CurrentPlatform { get; private set; }
        public bool IsOnPlatform => CurrentPlatform != null;

        private VRPlayerBody _playerBody;
        private Vector3 _lastPlatformPos;
        private Quaternion _lastPlatformRot;

        private void Awake()
        {
            _playerBody = GetComponent<VRPlayerBody>();
        }

        private void FixedUpdate()
        {
            if (!_playerBody.IsGrounded)
            {
                CurrentPlatform = null;
                return;
            }

            DetectPlatform();
            ApplyPlatformMovement();
        }

        private void DetectPlatform()
        {
            Vector3 origin = transform.position + Vector3.up * 0.1f;

            if (!Physics.SphereCast(origin, detectionRadius, Vector3.down, out RaycastHit hit,
                0.5f, platformLayers, QueryTriggerInteraction.Ignore))
            {
                CurrentPlatform = null;
                return;
            }

            Rigidbody platformRb = hit.collider.attachedRigidbody;
            if (platformRb == null || platformRb.isKinematic == false)
            {
                CurrentPlatform = null;
                return;
            }

            if (CurrentPlatform != hit.transform)
            {
                CurrentPlatform = hit.transform;
                _lastPlatformPos = CurrentPlatform.position;
                _lastPlatformRot = CurrentPlatform.rotation;
                return;
            }

            CurrentPlatform = hit.transform;
        }

        private void ApplyPlatformMovement()
        {
            if (CurrentPlatform == null) return;

            Vector3 posDelta = CurrentPlatform.position - _lastPlatformPos;
            Quaternion rotDelta = CurrentPlatform.rotation * Quaternion.Inverse(_lastPlatformRot);

            if (posDelta.sqrMagnitude > 0.00001f)
                _playerBody.Rb.MovePosition(_playerBody.Rb.position + posDelta);

            rotDelta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 0.01f && !float.IsInfinity(axis.x))
            {
                Vector3 pivot = CurrentPlatform.position;
                Vector3 offset = _playerBody.Rb.position - pivot;
                Vector3 rotatedOffset = rotDelta * offset;
                _playerBody.Rb.MovePosition(pivot + rotatedOffset);
                _playerBody.Rotate(angle * Vector3.Dot(axis, Vector3.up));
            }

            _lastPlatformPos = CurrentPlatform.position;
            _lastPlatformRot = CurrentPlatform.rotation;
        }
    }
}
