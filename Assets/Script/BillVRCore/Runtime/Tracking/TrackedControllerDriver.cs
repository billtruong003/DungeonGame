using UnityEngine;
using UnityEngine.XR;

namespace BillVRCore.Tracking
{
    public class TrackedControllerDriver : MonoBehaviour
    {
        [SerializeField] private XRNode node = XRNode.RightHand;
        [SerializeField] private TrackingMode positionMode = TrackingMode.Tracked;
        [SerializeField] private TrackingMode rotationMode = TrackingMode.Tracked;
        [SerializeField] private Vector3 positionOffset;
        [SerializeField] private Vector3 rotationOffset;
        [SerializeField] private UpdateMethod updateMethod = UpdateMethod.BeforeRender;

        public bool IsTracking { get; private set; }
        public Vector3 TrackedPosition { get; private set; }
        public Quaternion TrackedRotation { get; private set; }

        public enum TrackingMode { Tracked, None }
        public enum UpdateMethod { Update, BeforeRender, FixedUpdate }

        private InputDevice _device;
        private bool _deviceSearched;

        private void OnEnable()
        {
            if (updateMethod == UpdateMethod.BeforeRender)
                Application.onBeforeRender += OnBeforeRender;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= OnBeforeRender;
        }

        private void Update()
        {
            if (updateMethod == UpdateMethod.Update)
                UpdateTracking();
        }

        private void FixedUpdate()
        {
            if (updateMethod == UpdateMethod.FixedUpdate)
                UpdateTracking();
        }

        [BeforeRenderOrder(-30000)]
        private void OnBeforeRender()
        {
            UpdateTracking();
        }

        private void UpdateTracking()
        {
            EnsureDevice();
            if (!_device.isValid) { IsTracking = false; return; }

            bool posValid = _device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 pos);
            bool rotValid = _device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rot);

            IsTracking = posValid || rotValid;
            if (!IsTracking) return;

            if (positionMode == TrackingMode.Tracked && posValid)
            {
                TrackedPosition = pos;
                transform.localPosition = pos + rot * positionOffset;
            }

            if (rotationMode == TrackingMode.Tracked && rotValid)
            {
                TrackedRotation = rot;
                transform.localRotation = rot * Quaternion.Euler(rotationOffset);
            }
        }

        private void EnsureDevice()
        {
            if (_device.isValid) return;
            _device = InputDevices.GetDeviceAtXRNode(node);
        }

        public void SetNode(XRNode xrNode)
        {
            node = xrNode;
            _device = default;
        }

        public void SetPositionOffset(Vector3 offset) => positionOffset = offset;
        public void SetRotationOffset(Vector3 offset) => rotationOffset = offset;
    }
}
