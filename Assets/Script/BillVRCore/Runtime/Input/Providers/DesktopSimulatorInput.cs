using UnityEngine;

namespace BillVRCore.Input
{
    public class DesktopSimulatorInput : IVRInput
    {
        public InputSourceType ActiveSource => InputSourceType.Desktop;

        private bool _prevLeftGrab;
        private bool _prevRightGrab;
        private bool _prevLeftTrigger;
        private bool _prevRightTrigger;
        private bool _curLeftGrab;
        private bool _curRightGrab;
        private bool _curLeftTrigger;
        private bool _curRightTrigger;

        private readonly Camera _camera;
        private Vector3 _simulatedLeftPos;
        private Vector3 _simulatedRightPos;
        private float _simulatedGrip;

        public DesktopSimulatorInput()
        {
            _camera = Camera.main;
            _simulatedLeftPos = new Vector3(-0.2f, 1.3f, 0.3f);
            _simulatedRightPos = new Vector3(0.2f, 1.3f, 0.3f);
        }

        public void UpdateState()
        {
            _prevLeftGrab = _curLeftGrab;
            _prevRightGrab = _curRightGrab;
            _prevLeftTrigger = _curLeftTrigger;
            _prevRightTrigger = _curRightTrigger;

            _curLeftGrab = UnityEngine.Input.GetMouseButton(1);
            _curRightGrab = UnityEngine.Input.GetMouseButton(0);
            _curLeftTrigger = UnityEngine.Input.GetKey(KeyCode.Q);
            _curRightTrigger = UnityEngine.Input.GetKey(KeyCode.E);

            _simulatedGrip = _curRightGrab ? 1f : 0f;

            if (_camera != null)
            {
                Ray ray = _camera.ScreenPointToRay(UnityEngine.Input.mousePosition);
                _simulatedRightPos = ray.GetPoint(0.5f);
                _simulatedLeftPos = _simulatedRightPos + Vector3.left * 0.3f;
            }
        }

        public bool GrabPressed(HandSide side) => side == HandSide.Left
            ? (_curLeftGrab && !_prevLeftGrab)
            : (_curRightGrab && !_prevRightGrab);

        public bool GrabReleased(HandSide side) => side == HandSide.Left
            ? (!_curLeftGrab && _prevLeftGrab)
            : (!_curRightGrab && _prevRightGrab);

        public bool GrabHeld(HandSide side) => side == HandSide.Left ? _curLeftGrab : _curRightGrab;

        public bool TriggerPressed(HandSide side) => side == HandSide.Left
            ? (_curLeftTrigger && !_prevLeftTrigger)
            : (_curRightTrigger && !_prevRightTrigger);

        public bool TriggerReleased(HandSide side) => side == HandSide.Left
            ? (!_curLeftTrigger && _prevLeftTrigger)
            : (!_curRightTrigger && _prevRightTrigger);

        public bool TriggerHeld(HandSide side) => side == HandSide.Left ? _curLeftTrigger : _curRightTrigger;

        public bool PrimaryButtonDown(HandSide side) => UnityEngine.Input.GetKeyDown(
            side == HandSide.Left ? KeyCode.Alpha1 : KeyCode.Alpha3);

        public bool SecondaryButtonDown(HandSide side) => UnityEngine.Input.GetKeyDown(
            side == HandSide.Left ? KeyCode.Alpha2 : KeyCode.Alpha4);

        public bool JoystickClick(HandSide side) => false;
        public bool MenuButtonDown() => UnityEngine.Input.GetKeyDown(KeyCode.Escape);

        public float GripStrength(HandSide side) =>
            (side == HandSide.Left ? _curLeftGrab : _curRightGrab) ? 1f : 0f;

        public float TriggerStrength(HandSide side) =>
            (side == HandSide.Left ? _curLeftTrigger : _curRightTrigger) ? 1f : 0f;

        public Vector2 JoystickAxis(HandSide side)
        {
            if (side == HandSide.Left)
                return new Vector2(
                    UnityEngine.Input.GetAxis("Horizontal"),
                    UnityEngine.Input.GetAxis("Vertical"));
            return Vector2.zero;
        }

        public float FingerCurl(HandSide side, FingerType finger)
        {
            bool grab = side == HandSide.Left ? _curLeftGrab : _curRightGrab;
            return finger switch
            {
                FingerType.Index => TriggerStrength(side),
                _ => grab ? 1f : 0f
            };
        }

        public bool ThumbTouching(HandSide side) => false;
        public bool IndexTouching(HandSide side) => TriggerHeld(side);
        public bool IsConnected(HandSide side) => true;

        public Pose GetControllerPose(HandSide side) => new Pose(
            side == HandSide.Left ? _simulatedLeftPos : _simulatedRightPos,
            _camera != null ? _camera.transform.rotation : Quaternion.identity);

        public Pose GetHeadPose() => _camera != null
            ? new Pose(_camera.transform.position, _camera.transform.rotation)
            : Pose.identity;
    }
}
