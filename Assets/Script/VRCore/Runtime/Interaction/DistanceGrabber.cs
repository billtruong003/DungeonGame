using UnityEngine;
using VRCore.Hand;
using VRCore.Input;

namespace VRCore.Interaction
{
    [RequireComponent(typeof(VRHand))]
    public class DistanceGrabber : MonoBehaviour
    {
        [Header("Ray")]
        [SerializeField] private float maxDistance = 10f;
        [SerializeField] private LayerMask grabLayers = ~0;
        [SerializeField] private float rayRadius = 0.02f;

        [Header("Pull")]
        [SerializeField] private float pullSpeed = 12f;
        [SerializeField] private float pullArriveDistance = 0.15f;

        [Header("Visual")]
        [SerializeField] private LineRenderer rayLine;
        [SerializeField] private Color idleColor = new(0.5f, 0.5f, 1f, 0.3f);
        [SerializeField] private Color targetColor = new(0.3f, 1f, 0.5f, 0.6f);

        public Grabbable DistanceTarget { get; private set; }
        public bool IsPulling { get; private set; }
        public bool IsAiming => !_hand.GrabHandler.IsHolding && !IsPulling;

        private VRHand _hand;
        private Grabbable _pullingObject;
        private Vector3 _pullStartPos;
        private float _pullProgress;
        private float _savedDrag;
        private float _savedGravity;

        private void Awake()
        {
            _hand = GetComponent<VRHand>();
        }

        private void Update()
        {
            if (InputManager.Instance == null) return;

            if (_hand.GrabHandler.IsHolding)
            {
                HideRay();
                return;
            }

            if (IsPulling)
            {
                UpdatePull();
                return;
            }

            ScanForDistanceTarget();
            UpdateRayVisual();
            CheckGrabInput();
        }

        private void ScanForDistanceTarget()
        {
            Transform palm = _hand.PalmTransform != null ? _hand.PalmTransform : transform;
            DistanceTarget = null;

            if (Physics.SphereCast(palm.position, rayRadius, palm.forward, out RaycastHit hit,
                maxDistance, grabLayers, QueryTriggerInteraction.Ignore))
            {
                var grabbable = hit.collider.GetComponentInParent<Grabbable>();
                if (grabbable != null && grabbable.CanBeGrabbedBy(_hand))
                    DistanceTarget = grabbable;
            }
        }

        private void CheckGrabInput()
        {
            if (DistanceTarget == null) return;
            if (InputManager.Instance.Input.TriggerPressed(_hand.Side))
                StartPull(DistanceTarget);
        }

        public void StartPull(Grabbable target)
        {
            if (target == null || IsPulling) return;

            _pullingObject = target;
            _pullStartPos = target.transform.position;
            _pullProgress = 0f;
            IsPulling = true;

            _savedDrag = target.Rb.linearDamping;
            _savedGravity = target.Rb.useGravity ? 1f : 0f;
            target.Rb.useGravity = false;
            target.Rb.linearDamping = 5f;
        }

        public void CancelPull()
        {
            if (_pullingObject == null) return;

            _pullingObject.Rb.useGravity = _savedGravity > 0.5f;
            _pullingObject.Rb.linearDamping = _savedDrag;

            _pullingObject = null;
            IsPulling = false;
        }

        private void UpdatePull()
        {
            if (_pullingObject == null) { IsPulling = false; return; }

            Vector3 handPos = _hand.PalmTransform != null ? _hand.PalmTransform.position : _hand.transform.position;
            float totalDist = Vector3.Distance(_pullStartPos, handPos);
            if (totalDist < 0.01f) totalDist = 1f;

            _pullProgress += Time.deltaTime * pullSpeed / totalDist;
            _pullProgress = Mathf.Clamp01(_pullProgress);

            float t = _pullProgress * _pullProgress * (3f - 2f * _pullProgress);
            Vector3 targetPos = Vector3.Lerp(_pullStartPos, handPos, t);
            _pullingObject.Rb.linearVelocity = (targetPos - _pullingObject.transform.position) / Time.deltaTime * 0.5f;

            if (Vector3.Distance(_pullingObject.transform.position, handPos) <= pullArriveDistance || _pullProgress >= 1f)
                CompletePull();

            if (InputManager.Instance.Input.TriggerReleased(_hand.Side))
                CancelPull();

            UpdateRayVisual();
        }

        private void CompletePull()
        {
            var target = _pullingObject;
            target.Rb.useGravity = _savedGravity > 0.5f;
            target.Rb.linearDamping = _savedDrag;
            target.Rb.linearVelocity = Vector3.zero;

            _pullingObject = null;
            IsPulling = false;

            _hand.GrabHandler.TryGrab(target);
            HideRay();
        }

        private void UpdateRayVisual()
        {
            if (rayLine == null) return;

            Transform palm = _hand.PalmTransform != null ? _hand.PalmTransform : transform;

            rayLine.enabled = true;
            rayLine.positionCount = 2;

            if (IsPulling && _pullingObject != null)
            {
                rayLine.SetPosition(0, palm.position);
                rayLine.SetPosition(1, _pullingObject.transform.position);
                rayLine.startColor = targetColor;
                rayLine.endColor = targetColor;
            }
            else if (DistanceTarget != null)
            {
                rayLine.SetPosition(0, palm.position);
                rayLine.SetPosition(1, DistanceTarget.transform.position);
                rayLine.startColor = targetColor;
                rayLine.endColor = targetColor;
            }
            else
            {
                rayLine.SetPosition(0, palm.position);
                rayLine.SetPosition(1, palm.position + palm.forward * maxDistance);
                rayLine.startColor = idleColor;
                rayLine.endColor = new Color(idleColor.r, idleColor.g, idleColor.b, 0f);
            }
        }

        private void HideRay()
        {
            if (rayLine != null) rayLine.enabled = false;
            DistanceTarget = null;
        }

        public void SetMaxDistance(float distance) => maxDistance = distance;
        public void SetPullSpeed(float speed) => pullSpeed = speed;
        public void SetGrabLayers(LayerMask layers) => grabLayers = layers;
    }
}
