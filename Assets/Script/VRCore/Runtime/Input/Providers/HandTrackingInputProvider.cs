using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace VRCore.Input
{
    public class HandTrackingInputProvider : IVRInput
    {
        public InputSourceType ActiveSource => InputSourceType.HandTracking;

        private readonly float[] _fingerCurls = new float[10];
        private readonly bool[] _connected = new bool[2];
        private readonly Pose[] _handPoses = new Pose[2];
        private Pose _headPose;

        private bool _prevLeftGrab, _prevRightGrab;
        private bool _prevLeftPinch, _prevRightPinch;
        private bool _curLeftGrab, _curRightGrab;
        private bool _curLeftPinch, _curRightPinch;

        private readonly List<InputDevice> _deviceCache = new(4);
        private readonly InputDevice[] _trackedHands = new InputDevice[2];

        private const float GrabThreshold = 0.7f;
        private const float PinchThreshold = 0.8f;

        public void UpdateState()
        {
            _prevLeftGrab = _curLeftGrab;
            _prevRightGrab = _curRightGrab;
            _prevLeftPinch = _curLeftPinch;
            _prevRightPinch = _curRightPinch;

            UpdateHand(0, XRNode.LeftHand);
            UpdateHand(1, XRNode.RightHand);
            UpdateHeadData();

            _curLeftGrab = IsGrabGesture(0);
            _curRightGrab = IsGrabGesture(1);
            _curLeftPinch = IsPinchGesture(0);
            _curRightPinch = IsPinchGesture(1);
        }

        private void UpdateHand(int sideIdx, XRNode node)
        {
            if (!_trackedHands[sideIdx].isValid)
            {
                _deviceCache.Clear();
                InputDevices.GetDevicesAtXRNode(node, _deviceCache);
                for (int i = 0; i < _deviceCache.Count; i++)
                {
                    if ((_deviceCache[i].characteristics & InputDeviceCharacteristics.HandTracking) != 0)
                    {
                        _trackedHands[sideIdx] = _deviceCache[i];
                        break;
                    }
                }
            }

            var device = _trackedHands[sideIdx];
            _connected[sideIdx] = device.isValid;
            if (!device.isValid)
            {
                for (int f = 0; f < 5; f++) _fingerCurls[sideIdx * 5 + f] = 0f;
                return;
            }

            device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 pos);
            device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rot);
            _handPoses[sideIdx] = new Pose(pos, rot);

            device.TryGetFeatureValue(CommonUsages.grip, out float grip);
            device.TryGetFeatureValue(CommonUsages.trigger, out float trigger);

            int o = sideIdx * 5;
            _fingerCurls[o + 0] = grip > 0.3f ? grip : 0f;
            _fingerCurls[o + 1] = trigger;
            _fingerCurls[o + 2] = grip;
            _fingerCurls[o + 3] = grip;
            _fingerCurls[o + 4] = grip;
        }

        private void UpdateHeadData()
        {
            InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (!head.isValid) return;
            head.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 pos);
            head.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rot);
            _headPose = new Pose(pos, rot);
        }

        private bool IsGrabGesture(int side)
        {
            int o = side * 5;
            return _fingerCurls[o + 1] > GrabThreshold
                && _fingerCurls[o + 2] > GrabThreshold
                && _fingerCurls[o + 3] > GrabThreshold;
        }

        private bool IsPinchGesture(int side)
        {
            int o = side * 5;
            return _fingerCurls[o] > PinchThreshold
                && _fingerCurls[o + 1] > PinchThreshold;
        }

        public bool GrabPressed(HandSide s) =>
            s == HandSide.Left ? (_curLeftGrab && !_prevLeftGrab) : (_curRightGrab && !_prevRightGrab);
        public bool GrabReleased(HandSide s) =>
            s == HandSide.Left ? (!_curLeftGrab && _prevLeftGrab) : (!_curRightGrab && _prevRightGrab);
        public bool GrabHeld(HandSide s) =>
            s == HandSide.Left ? _curLeftGrab : _curRightGrab;
        public bool TriggerPressed(HandSide s) =>
            s == HandSide.Left ? (_curLeftPinch && !_prevLeftPinch) : (_curRightPinch && !_prevRightPinch);
        public bool TriggerReleased(HandSide s) =>
            s == HandSide.Left ? (!_curLeftPinch && _prevLeftPinch) : (!_curRightPinch && _prevRightPinch);
        public bool TriggerHeld(HandSide s) =>
            s == HandSide.Left ? _curLeftPinch : _curRightPinch;
        public bool PrimaryButtonDown(HandSide s) => false;
        public bool SecondaryButtonDown(HandSide s) => false;
        public bool JoystickClick(HandSide s) => false;
        public bool MenuButtonDown() => false;
        public float GripStrength(HandSide s)
        {
            int o = (int)s * 5;
            return (_fingerCurls[o + 2] + _fingerCurls[o + 3] + _fingerCurls[o + 4]) / 3f;
        }
        public float TriggerStrength(HandSide s) => _fingerCurls[(int)s * 5 + 1];
        public Vector2 JoystickAxis(HandSide s) => Vector2.zero;
        public float FingerCurl(HandSide s, FingerType f) => _fingerCurls[(int)s * 5 + (int)f];
        public bool ThumbTouching(HandSide s) => _fingerCurls[(int)s * 5] > 0.5f;
        public bool IndexTouching(HandSide s) => _fingerCurls[(int)s * 5 + 1] > 0.3f;
        public bool IsConnected(HandSide s) => _connected[(int)s];
        public Pose GetControllerPose(HandSide s) => _handPoses[(int)s];
        public Pose GetHeadPose() => _headPose;
    }
}
