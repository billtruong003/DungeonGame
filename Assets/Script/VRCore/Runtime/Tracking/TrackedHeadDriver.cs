using UnityEngine;
using UnityEngine.XR;

namespace VRCore.Tracking
{
    public class TrackedHeadDriver : MonoBehaviour
    {
        [SerializeField] private Vector3 positionOffset;
        [SerializeField] private Vector3 rotationOffset;

        public bool IsTracking { get; private set; }

        private InputDevice _headDevice;

        private void OnEnable()
        {
            Application.onBeforeRender += OnBeforeRender;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= OnBeforeRender;
        }

        [BeforeRenderOrder(-30000)]
        private void OnBeforeRender()
        {
            if (!_headDevice.isValid)
                _headDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);

            if (!_headDevice.isValid) { IsTracking = false; return; }

            _headDevice.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 pos);
            _headDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rot);

            IsTracking = true;
            transform.localPosition = pos + positionOffset;
            transform.localRotation = rot * Quaternion.Euler(rotationOffset);
        }

        public void SetPositionOffset(Vector3 offset) => positionOffset = offset;
    }
}
