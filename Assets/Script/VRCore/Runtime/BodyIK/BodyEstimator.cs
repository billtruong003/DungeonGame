using UnityEngine;

namespace VRCore.BodyIK
{
    [DefaultExecutionOrder(100)]
    public class BodyEstimator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform headTarget;
        [SerializeField] private Transform leftHandTarget;
        [SerializeField] private Transform rightHandTarget;

        [Header("Body Proportions")]
        [SerializeField] private float neckToHipRatio = 0.52f;
        [SerializeField] private float shoulderWidth = 0.36f;
        [SerializeField] private float shoulderDropFromHead = 0.22f;
        [SerializeField] private float hipWidth = 0.28f;

        [Header("Smoothing")]
        [SerializeField] private float hipSmoothSpeed = 8f;
        [SerializeField] private float spineSmoothSpeed = 12f;

        [Header("Lean")]
        [SerializeField] private float leanAmount = 0.3f;
        [SerializeField] private float leanSmooth = 5f;

        public Transform HipEstimate { get; private set; }
        public Transform SpineEstimate { get; private set; }
        public Transform LeftShoulderEstimate { get; private set; }
        public Transform RightShoulderEstimate { get; private set; }

        private Vector3 _smoothedHipPos;
        private Quaternion _smoothedHipRot;
        private Vector3 _leanOffset;

        private void Awake()
        {
            HipEstimate = CreateEstimateTransform("HipEstimate");
            SpineEstimate = CreateEstimateTransform("SpineEstimate");
            LeftShoulderEstimate = CreateEstimateTransform("LeftShoulderEstimate");
            RightShoulderEstimate = CreateEstimateTransform("RightShoulderEstimate");
        }

        private void Start()
        {
            if (headTarget == null)
            {
                var cam = Camera.main;
                if (cam != null) headTarget = cam.transform;
            }

            if (headTarget != null)
                _smoothedHipPos = EstimateHipPosition();
        }

        private void LateUpdate()
        {
            if (headTarget == null) return;

            UpdateHip();
            UpdateSpine();
            UpdateShoulders();
        }

        private void UpdateHip()
        {
            Vector3 targetHipPos = EstimateHipPosition();
            Quaternion targetHipRot = EstimateHipRotation();

            _smoothedHipPos = Vector3.Lerp(_smoothedHipPos, targetHipPos,
                Time.deltaTime * hipSmoothSpeed);
            _smoothedHipRot = Quaternion.Slerp(_smoothedHipRot, targetHipRot,
                Time.deltaTime * hipSmoothSpeed);

            HipEstimate.position = _smoothedHipPos;
            HipEstimate.rotation = _smoothedHipRot;
        }

        private void UpdateSpine()
        {
            Vector3 spinePos = Vector3.Lerp(HipEstimate.position,
                headTarget.position - Vector3.up * shoulderDropFromHead, 0.5f);

            SpineEstimate.position = Vector3.Lerp(SpineEstimate.position, spinePos,
                Time.deltaTime * spineSmoothSpeed);

            Vector3 spineDir = (headTarget.position - HipEstimate.position).normalized;
            if (spineDir.sqrMagnitude > 0.001f)
                SpineEstimate.rotation = Quaternion.LookRotation(
                    Vector3.ProjectOnPlane(HipEstimate.forward, Vector3.up), spineDir);
        }

        private void UpdateShoulders()
        {
            Vector3 shoulderCenter = headTarget.position - Vector3.up * shoulderDropFromHead;
            Vector3 right = HipEstimate.right;

            LeftShoulderEstimate.position = shoulderCenter - right * (shoulderWidth * 0.5f);
            RightShoulderEstimate.position = shoulderCenter + right * (shoulderWidth * 0.5f);

            LeftShoulderEstimate.rotation = HipEstimate.rotation;
            RightShoulderEstimate.rotation = HipEstimate.rotation;
        }

        private Vector3 EstimateHipPosition()
        {
            Vector3 headPos = headTarget.position;
            float headHeight = headPos.y - transform.position.y;
            float hipHeight = headHeight * (1f - neckToHipRatio);

            Vector3 hipPos = new Vector3(headPos.x, transform.position.y + hipHeight, headPos.z);

            Vector3 headForwardFlat = Vector3.ProjectOnPlane(headTarget.forward, Vector3.up).normalized;
            float headPitch = Vector3.SignedAngle(headForwardFlat, headTarget.forward, headTarget.right);
            Vector3 lean = headForwardFlat * (headPitch * leanAmount * 0.01f);
            _leanOffset = Vector3.Lerp(_leanOffset, lean, Time.deltaTime * leanSmooth);

            return hipPos + _leanOffset;
        }

        private Quaternion EstimateHipRotation()
        {
            Vector3 headForward = Vector3.ProjectOnPlane(headTarget.forward, Vector3.up);
            if (headForward.sqrMagnitude < 0.001f)
                headForward = Vector3.forward;

            if (leftHandTarget != null && rightHandTarget != null)
            {
                Vector3 handsMidForward = Vector3.ProjectOnPlane(
                    (leftHandTarget.forward + rightHandTarget.forward) * 0.5f, Vector3.up);

                if (handsMidForward.sqrMagnitude > 0.01f)
                    headForward = Vector3.Slerp(headForward, handsMidForward.normalized, 0.3f);
            }

            return Quaternion.LookRotation(headForward.normalized, Vector3.up);
        }

        private Transform CreateEstimateTransform(string name)
        {
            var go = new GameObject($"[BodyIK] {name}");
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        public void SetTargets(Transform head, Transform leftHand, Transform rightHand)
        {
            headTarget = head;
            leftHandTarget = leftHand;
            rightHandTarget = rightHand;
        }
    }
}
