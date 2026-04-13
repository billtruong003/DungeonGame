using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace BillVRCore.Input
{
    public class QuestLegacyInputProvider : IVRInput
    {
        public InputSourceType ActiveSource => InputSourceType.Controller;

        private readonly InputDevice[] _devices = new InputDevice[2];
        private readonly float[] _prevGrip = new float[2];
        private readonly float[] _prevTrigger = new float[2];
        private readonly bool[] _prevPrimary = new bool[2];
        private readonly bool[] _prevSecondary = new bool[2];
        private readonly bool[] _prevJoystickClick = new bool[2];
        private readonly float[] _curGrip = new float[2];
        private readonly float[] _curTrigger = new float[2];
        private readonly bool[] _curPrimary = new bool[2];
        private readonly bool[] _curSecondary = new bool[2];
        private readonly bool[] _curJoystickClick = new bool[2];
        private readonly Vector2[] _curJoystick = new Vector2[2];
        private readonly float[] _curThumbTouch = new float[2];
        private readonly float[] _curIndexTouch = new float[2];
        private readonly bool[] _connected = new bool[2];
        private bool _prevMenu;
        private bool _curMenu;

        private static readonly InputFeatureUsage<float> _thumbTouchUsage = new InputFeatureUsage<float>("ThumbTouch");
        private static readonly InputFeatureUsage<float> _indexTouchUsage = new InputFeatureUsage<float>("IndexTouch");

        private const float GrabThreshold = 0.65f;
        private const float GrabReleaseThreshold = 0.45f;
        private const float TriggerThreshold = 0.5f;

        private readonly bool[] _prevGrabState = new bool[2];
        private readonly bool[] _curGrabState = new bool[2];

        public void UpdateState()
        {
            RefreshDevices();

            for (int i = 0; i < 2; i++)
            {
                _prevGrip[i] = _curGrip[i];
                _prevTrigger[i] = _curTrigger[i];
                _prevPrimary[i] = _curPrimary[i];
                _prevSecondary[i] = _curSecondary[i];
                _prevJoystickClick[i] = _curJoystickClick[i];

                if (!_devices[i].isValid)
                {
                    _connected[i] = false;
                    continue;
                }

                _connected[i] = true;
                _devices[i].TryGetFeatureValue(CommonUsages.grip, out _curGrip[i]);
                _devices[i].TryGetFeatureValue(CommonUsages.trigger, out _curTrigger[i]);
                _devices[i].TryGetFeatureValue(CommonUsages.primaryButton, out _curPrimary[i]);
                _devices[i].TryGetFeatureValue(CommonUsages.secondaryButton, out _curSecondary[i]);
                _devices[i].TryGetFeatureValue(CommonUsages.primary2DAxisClick, out _curJoystickClick[i]);
                _devices[i].TryGetFeatureValue(CommonUsages.primary2DAxis, out _curJoystick[i]);
                _devices[i].TryGetFeatureValue(_thumbTouchUsage, out _curThumbTouch[i]);
                _devices[i].TryGetFeatureValue(_indexTouchUsage, out _curIndexTouch[i]);
            }

            _prevMenu = _curMenu;
            if (_devices[0].isValid)
                _devices[0].TryGetFeatureValue(CommonUsages.menuButton, out _curMenu);

            // Hysteresis: grab triggers at 0.65, release triggers at 0.45.
            // Prevents accidental release/regrab when grip fluctuates near threshold.
            for (int i = 0; i < 2; i++)
            {
                _prevGrabState[i] = _curGrabState[i];
                if (_curGrabState[i])
                {
                    if (_curGrip[i] < GrabReleaseThreshold)
                        _curGrabState[i] = false;
                }
                else
                {
                    if (_curGrip[i] >= GrabThreshold)
                        _curGrabState[i] = true;
                }
            }
        }

        public bool GrabPressed(HandSide side)
        {
            int i = (int)side;
            return _curGrabState[i] && !_prevGrabState[i];
        }

        public bool GrabReleased(HandSide side)
        {
            int i = (int)side;
            return !_curGrabState[i] && _prevGrabState[i];
        }

        public bool GrabHeld(HandSide side) => _curGrabState[(int)side];

        public bool TriggerPressed(HandSide side)
        {
            int i = (int)side;
            return _curTrigger[i] >= TriggerThreshold && _prevTrigger[i] < TriggerThreshold;
        }

        public bool TriggerReleased(HandSide side)
        {
            int i = (int)side;
            return _curTrigger[i] < TriggerThreshold && _prevTrigger[i] >= TriggerThreshold;
        }

        public bool TriggerHeld(HandSide side) => _curTrigger[(int)side] >= TriggerThreshold;

        public bool PrimaryButtonDown(HandSide side)
        {
            int i = (int)side;
            return _curPrimary[i] && !_prevPrimary[i];
        }

        public bool SecondaryButtonDown(HandSide side)
        {
            int i = (int)side;
            return _curSecondary[i] && !_prevSecondary[i];
        }

        public bool JoystickClick(HandSide side)
        {
            int i = (int)side;
            return _curJoystickClick[i] && !_prevJoystickClick[i];
        }

        public bool MenuButtonDown() => _curMenu && !_prevMenu;

        public float GripStrength(HandSide side) => _curGrip[(int)side];
        public float TriggerStrength(HandSide side) => _curTrigger[(int)side];
        public Vector2 JoystickAxis(HandSide side) => _curJoystick[(int)side];
        public bool ThumbTouching(HandSide side) => _curThumbTouch[(int)side] > 0.5f;
        public bool IndexTouching(HandSide side) => _curIndexTouch[(int)side] > 0.5f;
        public bool IsConnected(HandSide side) => _connected[(int)side];

        public float FingerCurl(HandSide side, FingerType finger)
        {
            int i = (int)side;
            return finger switch
            {
                FingerType.Thumb => _curThumbTouch[i] > 0.5f ? 1f : 0f,
                FingerType.Index => _curTrigger[i],
                FingerType.Middle => _curGrip[i],
                FingerType.Ring => _curGrip[i],
                FingerType.Pinky => _curGrip[i],
                _ => 0f
            };
        }

        public Pose GetControllerPose(HandSide side)
        {
            if (!_devices[(int)side].isValid)
                return Pose.identity;

            _devices[(int)side].TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 pos);
            _devices[(int)side].TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rot);
            return new Pose(pos, rot);
        }

        public Pose GetHeadPose()
        {
            InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (!head.isValid) return Pose.identity;
            head.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 pos);
            head.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rot);
            return new Pose(pos, rot);
        }

        private void RefreshDevices()
        {
            if (!_devices[0].isValid)
                _devices[0] = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            if (!_devices[1].isValid)
                _devices[1] = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        }
    }
}
