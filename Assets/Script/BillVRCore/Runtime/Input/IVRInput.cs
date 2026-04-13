using UnityEngine;

namespace BillVRCore.Input
{
    public interface IVRInput
    {
        bool GrabPressed(HandSide side);
        bool GrabReleased(HandSide side);
        bool GrabHeld(HandSide side);
        bool TriggerPressed(HandSide side);
        bool TriggerReleased(HandSide side);
        bool TriggerHeld(HandSide side);
        bool PrimaryButtonDown(HandSide side);
        bool SecondaryButtonDown(HandSide side);
        bool JoystickClick(HandSide side);
        bool MenuButtonDown();

        float GripStrength(HandSide side);
        float TriggerStrength(HandSide side);

        Vector2 JoystickAxis(HandSide side);

        float FingerCurl(HandSide side, FingerType finger);

        bool ThumbTouching(HandSide side);
        bool IndexTouching(HandSide side);

        Pose GetControllerPose(HandSide side);
        Pose GetHeadPose();

        InputSourceType ActiveSource { get; }
        bool IsConnected(HandSide side);

        void UpdateState();
    }
}
