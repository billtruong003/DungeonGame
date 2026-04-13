using UnityEngine;
using UnityEngine.Rendering;

namespace BillVRCore.Hand
{
    [DefaultExecutionOrder(int.MaxValue)]
    public class HandStabilizer : MonoBehaviour
    {
        [SerializeField] private VRHand targetHand;

        public VRHand TargetHand => targetHand;

        private Vector3 _physicsPosition;
        private Quaternion _physicsRotation;
        private bool _stabilized;

        private void Awake()
        {
            if (targetHand == null)
                targetHand = GetComponentInParent<VRHand>();
        }

        private void OnEnable()
        {
            if (GraphicsSettings.defaultRenderPipeline != null)
            {
                RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
                RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
            }
        }

        private void OnDisable()
        {
            if (GraphicsSettings.defaultRenderPipeline != null)
            {
                RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
                RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            }
        }

        private void OnWillRenderObject()
        {
            Stabilize();
        }

        private void OnPreRender()
        {
            Stabilize();
        }

        private void OnPostRender()
        {
            Restore();
        }

        private void OnBeginCameraRendering(ScriptableRenderContext ctx, Camera cam)
        {
            Stabilize();
        }

        private void OnEndCameraRendering(ScriptableRenderContext ctx, Camera cam)
        {
            Restore();
        }

        private void Stabilize()
        {
            if (targetHand == null || targetHand.GrabHandler == null) return;

            if (!_stabilized)
            {
                _physicsPosition = targetHand.transform.position;
                _physicsRotation = targetHand.transform.rotation;
            }

            Transform follow = targetHand.FollowTarget;
            if (follow == null) return;

            targetHand.transform.position = follow.position;
            targetHand.transform.rotation = follow.rotation;
            _stabilized = true;
        }

        private void Restore()
        {
            if (!_stabilized || targetHand == null) return;

            targetHand.transform.position = _physicsPosition;
            targetHand.transform.rotation = _physicsRotation;
            _stabilized = false;
        }

        public void SetHand(VRHand hand) => targetHand = hand;
    }
}
