using UnityEngine;
using BillVRCore.Hand;

namespace BillVRCore.Interaction
{
    public class SnapGrabbable : Grabbable
    {
        [Header("Snap Settings")]
        [SerializeField] private Transform leftGripPoint;
        [SerializeField] private Transform rightGripPoint;
        [SerializeField] private float snapSpeed = 20f;
        [SerializeField] private bool lockHandToGrip = true;

        [Header("Hand Pose (optional)")]
        [SerializeField] [Range(0f, 1f)] private float thumbCurl = 0.8f;
        [SerializeField] [Range(0f, 1f)] private float indexCurl = 0.7f;
        [SerializeField] [Range(0f, 1f)] private float middleCurl = 0.9f;
        [SerializeField] [Range(0f, 1f)] private float ringCurl = 0.9f;
        [SerializeField] [Range(0f, 1f)] private float pinkyCurl = 0.9f;

        public Transform GetGripPoint(HandSide side)
        {
            return side == HandSide.Left ? leftGripPoint : rightGripPoint;
        }

        public float GetFingerCurl(FingerType finger)
        {
            return finger switch
            {
                FingerType.Thumb => thumbCurl,
                FingerType.Index => indexCurl,
                FingerType.Middle => middleCurl,
                FingerType.Ring => ringCurl,
                FingerType.Pinky => pinkyCurl,
                _ => 0.8f
            };
        }

        public bool HasCustomPose => true;
        public bool LockHand => lockHandToGrip;
        public float SnapSpeed => snapSpeed;

        public override void OnGrab(VRHand hand)
        {
            base.OnGrab(hand);

            if (!lockHandToGrip) return;

            Transform grip = GetGripPoint(hand.Side);
            if (grip == null) return;

            Vector3 offset = hand.transform.position - grip.position;
            Rb.position -= offset;
        }

        protected override void Awake()
        {
            base.Awake();

            if (leftGripPoint == null)
            {
                leftGripPoint = CreateDefaultGrip("LeftGrip", Vector3.left * 0.02f);
            }
            if (rightGripPoint == null)
            {
                rightGripPoint = CreateDefaultGrip("RightGrip", Vector3.right * 0.02f);
            }
        }

        private Transform CreateDefaultGrip(string name, Vector3 offset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = offset;
            return go.transform;
        }

        private void OnDrawGizmosSelected()
        {
            DrawGripGizmo(leftGripPoint, Color.blue);
            DrawGripGizmo(rightGripPoint, Color.red);
        }

        private void DrawGripGizmo(Transform grip, Color color)
        {
            if (grip == null) return;
            Gizmos.color = color;
            Gizmos.DrawWireSphere(grip.position, 0.02f);
            Gizmos.DrawRay(grip.position, grip.forward * 0.05f);
        }
    }
}
