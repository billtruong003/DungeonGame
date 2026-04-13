namespace BillVRCore
{
    public enum HandSide { Left, Right }

    public enum FingerType { Thumb, Index, Middle, Ring, Pinky }

    public enum FingerJoint { Knuckle, Middle, Distal, Tip }

    public enum InputSourceType { Controller, HandTracking, Desktop }

    public enum InputMode { LegacyController, NewInputSystem, HandTracking, Desktop }

    public enum GrabType { Default, HandToGrabbable, GrabbableToHand, Instant }

    public enum HandRestriction { Both, LeftOnly, RightOnly }

    public enum LocomotionState { Idle, JoystickMoving, GorillaMoving, Teleporting, Climbing }

    public enum GrabState { Empty, Hovering, Grabbing }

    public enum GrabMode { Default, Hold, Toggle }

    public enum FingerSource { Trigger, Grip, ThumbstickTouch, ButtonTouch, Manual }
}
