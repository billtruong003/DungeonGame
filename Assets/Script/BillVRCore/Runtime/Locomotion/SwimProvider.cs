using UnityEngine;
using BillVRCore.Hand;

namespace BillVRCore.Locomotion
{
    public class SwimProvider : MonoBehaviour, ILocomotionProvider
    {
        [Header("Water Detection")]
        [SerializeField] private LayerMask waterLayers;
        [SerializeField] private string waterTag = "Water";

        [Header("Buoyancy")]
        [SerializeField] private float buoyancyForce = 12f;
        [SerializeField] private float surfaceOffset = 0.3f;
        [SerializeField] private float waterDrag = 4f;
        [SerializeField] private float waterAngularDrag = 3f;

        [Header("Swimming")]
        [SerializeField] private float strokeForce = 6f;
        [SerializeField] private float maxSwimSpeed = 4f;
        [SerializeField] private float strokeCooldown = 0.1f;

        [Header("References")]
        [SerializeField] private VRHand leftHand;
        [SerializeField] private VRHand rightHand;

        public bool IsActive => _isSubmerged;
        public LocomotionState ProvidedState => LocomotionState.Idle;
        public int Priority => 25;
        public bool IsSubmerged => _isSubmerged;
        public float SubmersionDepth => _submersionDepth;

        private Rigidbody _playerRb;
        private bool _isSubmerged;
        private float _waterSurfaceY;
        private float _submersionDepth;
        private float _savedDrag;
        private float _savedAngularDrag;
        private Vector3 _leftPrevPos;
        private Vector3 _rightPrevPos;
        private float _lastStrokeTime;

        private void Awake()
        {
            _playerRb = GetComponentInParent<Rigidbody>();
        }

        private void Start()
        {
            FindHands();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsWater(other)) return;
            EnterWater(other);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsWater(other)) return;
            ExitWater();
        }

        private void FixedUpdate()
        {
            if (!_isSubmerged) return;

            ApplyBuoyancy();
            ProcessStrokes();
            ClampSpeed();
        }

        private void EnterWater(Collider waterCollider)
        {
            _isSubmerged = true;
            _waterSurfaceY = waterCollider.bounds.max.y;
            _savedDrag = _playerRb.linearDamping;
            _savedAngularDrag = _playerRb.angularDamping;
            _playerRb.linearDamping = waterDrag;
            _playerRb.angularDamping = waterAngularDrag;
            _playerRb.useGravity = false;

            if (leftHand != null) _leftPrevPos = leftHand.transform.position;
            if (rightHand != null) _rightPrevPos = rightHand.transform.position;
        }

        private void ExitWater()
        {
            _isSubmerged = false;
            _playerRb.linearDamping = _savedDrag;
            _playerRb.angularDamping = _savedAngularDrag;
            _playerRb.useGravity = true;
        }

        private void ApplyBuoyancy()
        {
            _submersionDepth = _waterSurfaceY - transform.position.y;
            float buoyancy = Mathf.Clamp01(_submersionDepth / surfaceOffset) * buoyancyForce;
            _playerRb.AddForce(Vector3.up * buoyancy, ForceMode.Acceleration);

            if (_submersionDepth < 0f)
            {
                float pushDown = _submersionDepth * 5f;
                _playerRb.AddForce(Vector3.up * pushDown, ForceMode.Acceleration);
            }
        }

        private void ProcessStrokes()
        {
            if (Time.time - _lastStrokeTime < strokeCooldown) return;

            ProcessHandStroke(leftHand, ref _leftPrevPos);
            ProcessHandStroke(rightHand, ref _rightPrevPos);
        }

        private void ProcessHandStroke(VRHand hand, ref Vector3 prevPos)
        {
            if (hand == null) return;
            if (hand.GrabHandler != null && hand.GrabHandler.IsHolding)
            {
                prevPos = hand.transform.position;
                return;
            }

            Vector3 currentPos = hand.transform.position;
            Vector3 delta = prevPos - currentPos;
            prevPos = currentPos;

            if (delta.sqrMagnitude < 0.0001f) return;

            Vector3 force = delta * strokeForce;
            _playerRb.AddForce(force, ForceMode.VelocityChange);
            _lastStrokeTime = Time.time;
        }

        private void ClampSpeed()
        {
            if (_playerRb.linearVelocity.magnitude > maxSwimSpeed)
                _playerRb.linearVelocity = _playerRb.linearVelocity.normalized * maxSwimSpeed;
        }

        private bool IsWater(Collider col)
        {
            if (!string.IsNullOrEmpty(waterTag) && col.CompareTag(waterTag)) return true;
            return (waterLayers & (1 << col.gameObject.layer)) != 0;
        }

        private void FindHands()
        {
            if (leftHand != null && rightHand != null) return;
            var hands = FindObjectsByType<VRHand>(FindObjectsSortMode.None);
            foreach (var h in hands)
            {
                if (h.Side == HandSide.Left && leftHand == null) leftHand = h;
                if (h.Side == HandSide.Right && rightHand == null) rightHand = h;
            }
        }

        public void SetLocomotionActive(bool active) { }
        public void SetStrokeForce(float force) => strokeForce = force;
        public void SetBuoyancy(float force) => buoyancyForce = force;
    }
}
