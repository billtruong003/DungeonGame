using UnityEngine;

namespace BillVRCore.Input
{
    [CreateAssetMenu(fileName = "FingerMappingConfig", menuName = "BillVR/Finger Mapping Config")]
    public class FingerMappingConfig : ScriptableObject
    {
        public FingerSource thumbSource = FingerSource.ThumbstickTouch;
        public FingerSource indexSource = FingerSource.Trigger;
        public FingerSource middleSource = FingerSource.Grip;
        public FingerSource ringSource = FingerSource.Grip;
        public FingerSource pinkySource = FingerSource.Grip;

        [Range(0f, 1f)] public float grabThreshold = 0.65f;
        [Range(0f, 1f)] public float pinchThreshold = 0.8f;
        [Range(0f, 1f)] public float pointThreshold = 0.3f;

        [Range(1f, 30f)] public float fingerSmoothSpeed = 15f;

        public float GetCurlForFinger(FingerType finger, IVRInput input, HandSide side)
        {
            FingerSource source = finger switch
            {
                FingerType.Thumb => thumbSource,
                FingerType.Index => indexSource,
                FingerType.Middle => middleSource,
                FingerType.Ring => ringSource,
                FingerType.Pinky => pinkySource,
                _ => FingerSource.Manual
            };

            return source switch
            {
                FingerSource.Trigger => input.TriggerStrength(side),
                FingerSource.Grip => input.GripStrength(side),
                FingerSource.ThumbstickTouch => input.ThumbTouching(side) ? 1f : 0f,
                FingerSource.ButtonTouch => input.IndexTouching(side) ? 1f : 0f,
                _ => 0f
            };
        }

        public bool IsPinching(IVRInput input, HandSide side)
        {
            return GetCurlForFinger(FingerType.Thumb, input, side) > pinchThreshold
                && GetCurlForFinger(FingerType.Index, input, side) > pinchThreshold;
        }

        public bool IsPointing(IVRInput input, HandSide side)
        {
            return GetCurlForFinger(FingerType.Index, input, side) < pointThreshold
                && GetCurlForFinger(FingerType.Middle, input, side) > grabThreshold;
        }
    }
}
