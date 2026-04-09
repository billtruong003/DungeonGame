using UnityEngine;

namespace VRCore.BodyIK
{
    [DefaultExecutionOrder(110)]
    public class FootIKSolver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BodyEstimator bodyEstimator;
        [SerializeField] private Transform leftFootTarget;
        [SerializeField] private Transform rightFootTarget;

        [Header("Stepping")]
        [SerializeField] private float stepDistance = 0.32f;
        [SerializeField] private float stepHeight = 0.12f;
        [SerializeField] private float stepSpeed = 8f;
        [SerializeField] private float footSpacing = 0.12f;
        [SerializeField] private float footForwardOffset = 0.08f;

        [Header("Ground")]
        [SerializeField] private LayerMask groundLayer = ~0;
        [SerializeField] private float groundRayHeight = 1f;
        [SerializeField] private float groundRayDistance = 2f;

        [Header("Idle")]
        [SerializeField] private float idleReturnSpeed = 3f;

        private FootState _leftFoot;
        private FootState _rightFoot;
        private bool _isMoving;
        private int _lastStepFoot;

        private struct FootState
        {
            public Vector3 currentPos;
            public Vector3 targetPos;
            public Vector3 startPos;
            public float stepProgress;
            public bool isStepping;
            public Quaternion groundRotation;
        }

        private void Awake()
        {
            if (leftFootTarget == null)
            {
                leftFootTarget = new GameObject("[FootIK] LeftFoot").transform;
                leftFootTarget.SetParent(transform, false);
            }
            if (rightFootTarget == null)
            {
                rightFootTarget = new GameObject("[FootIK] RightFoot").transform;
                rightFootTarget.SetParent(transform, false);
            }
        }

        private void Start()
        {
            Vector3 startPos = transform.position;
            _leftFoot.currentPos = startPos + Vector3.left * footSpacing;
            _leftFoot.targetPos = _leftFoot.currentPos;
            _rightFoot.currentPos = startPos + Vector3.right * footSpacing;
            _rightFoot.targetPos = _rightFoot.currentPos;
        }

        private void LateUpdate()
        {
            if (bodyEstimator == null || bodyEstimator.HipEstimate == null) return;

            _isMoving = Locomotion.LocomotionStateMachine.Instance != null
                && Locomotion.LocomotionStateMachine.Instance.IsMoving;

            if (_isMoving)
            {
                UpdateMovingFeet();
            }
            else
            {
                ReturnToIdle();
            }

            UpdateStepAnimation(ref _leftFoot);
            UpdateStepAnimation(ref _rightFoot);

            ApplyFootPositions();
        }

        private void UpdateMovingFeet()
        {
            Transform hip = bodyEstimator.HipEstimate;
            Vector3 hipRight = hip.right * footSpacing;
            Vector3 hipForward = hip.forward * footForwardOffset;

            Vector3 leftDesired = hip.position + hipForward - hipRight;
            Vector3 rightDesired = hip.position + hipForward + hipRight;

            leftDesired = ProjectToGround(leftDesired, out Quaternion leftRot);
            rightDesired = ProjectToGround(rightDesired, out Quaternion rightRot);

            float leftDist = Vector3.Distance(_leftFoot.currentPos, leftDesired);
            float rightDist = Vector3.Distance(_rightFoot.currentPos, rightDesired);

            bool leftNeedsStep = leftDist > stepDistance && !_leftFoot.isStepping;
            bool rightNeedsStep = rightDist > stepDistance && !_rightFoot.isStepping;

            if (leftNeedsStep && !_rightFoot.isStepping && (_lastStepFoot != 0 || !rightNeedsStep))
            {
                StartStep(ref _leftFoot, leftDesired, leftRot);
                _lastStepFoot = 0;
            }
            else if (rightNeedsStep && !_leftFoot.isStepping)
            {
                StartStep(ref _rightFoot, rightDesired, rightRot);
                _lastStepFoot = 1;
            }
        }

        private void ReturnToIdle()
        {
            if (_leftFoot.isStepping || _rightFoot.isStepping) return;

            Transform hip = bodyEstimator.HipEstimate;
            Vector3 hipRight = hip.right * footSpacing;

            Vector3 leftIdle = ProjectToGround(hip.position - hipRight, out Quaternion leftRot);
            Vector3 rightIdle = ProjectToGround(hip.position + hipRight, out Quaternion rightRot);

            _leftFoot.currentPos = Vector3.Lerp(_leftFoot.currentPos, leftIdle,
                Time.deltaTime * idleReturnSpeed);
            _leftFoot.groundRotation = Quaternion.Slerp(_leftFoot.groundRotation, leftRot,
                Time.deltaTime * idleReturnSpeed);

            _rightFoot.currentPos = Vector3.Lerp(_rightFoot.currentPos, rightIdle,
                Time.deltaTime * idleReturnSpeed);
            _rightFoot.groundRotation = Quaternion.Slerp(_rightFoot.groundRotation, rightRot,
                Time.deltaTime * idleReturnSpeed);
        }

        private void StartStep(ref FootState foot, Vector3 target, Quaternion groundRot)
        {
            foot.startPos = foot.currentPos;
            foot.targetPos = target;
            foot.groundRotation = groundRot;
            foot.stepProgress = 0f;
            foot.isStepping = true;
        }

        private void UpdateStepAnimation(ref FootState foot)
        {
            if (!foot.isStepping) return;

            foot.stepProgress += Time.deltaTime * stepSpeed;

            if (foot.stepProgress >= 1f)
            {
                foot.stepProgress = 1f;
                foot.isStepping = false;
                foot.currentPos = foot.targetPos;
                return;
            }

            float t = foot.stepProgress;
            float smoothT = t * t * (3f - 2f * t);

            Vector3 flatPos = Vector3.Lerp(foot.startPos, foot.targetPos, smoothT);
            float heightCurve = Mathf.Sin(t * Mathf.PI) * stepHeight;
            foot.currentPos = flatPos + Vector3.up * heightCurve;
        }

        private Vector3 ProjectToGround(Vector3 position, out Quaternion surfaceRotation)
        {
            Vector3 rayOrigin = position + Vector3.up * groundRayHeight;
            surfaceRotation = Quaternion.identity;

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit,
                groundRayDistance, groundLayer, QueryTriggerInteraction.Ignore))
            {
                surfaceRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
                return hit.point;
            }

            return new Vector3(position.x, transform.position.y, position.z);
        }

        private void ApplyFootPositions()
        {
            leftFootTarget.position = _leftFoot.currentPos;
            leftFootTarget.rotation = _leftFoot.groundRotation;

            rightFootTarget.position = _rightFoot.currentPos;
            rightFootTarget.rotation = _rightFoot.groundRotation;
        }

        public Transform GetLeftFootTarget() => leftFootTarget;
        public Transform GetRightFootTarget() => rightFootTarget;

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;

            Gizmos.color = _leftFoot.isStepping ? Color.yellow : Color.green;
            Gizmos.DrawSphere(_leftFoot.currentPos, 0.03f);
            Gizmos.DrawWireSphere(_leftFoot.targetPos, 0.02f);

            Gizmos.color = _rightFoot.isStepping ? Color.yellow : Color.red;
            Gizmos.DrawSphere(_rightFoot.currentPos, 0.03f);
            Gizmos.DrawWireSphere(_rightFoot.targetPos, 0.02f);
        }
    }
}
