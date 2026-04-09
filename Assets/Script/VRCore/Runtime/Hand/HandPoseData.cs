using UnityEngine;

namespace VRCore.Hand
{
    [CreateAssetMenu(fileName = "HandPose", menuName = "VRCore/Hand Pose")]
    public class HandPoseData : ScriptableObject
    {
        [Range(0f, 1f)] public float thumbCurl;
        [Range(0f, 1f)] public float indexCurl;
        [Range(0f, 1f)] public float middleCurl;
        [Range(0f, 1f)] public float ringCurl;
        [Range(0f, 1f)] public float pinkyCurl;

        public float GetCurl(FingerType finger)
        {
            return finger switch
            {
                FingerType.Thumb => thumbCurl,
                FingerType.Index => indexCurl,
                FingerType.Middle => middleCurl,
                FingerType.Ring => ringCurl,
                FingerType.Pinky => pinkyCurl,
                _ => 0f
            };
        }

        public void SetCurl(FingerType finger, float value)
        {
            value = Mathf.Clamp01(value);
            switch (finger)
            {
                case FingerType.Thumb: thumbCurl = value; break;
                case FingerType.Index: indexCurl = value; break;
                case FingerType.Middle: middleCurl = value; break;
                case FingerType.Ring: ringCurl = value; break;
                case FingerType.Pinky: pinkyCurl = value; break;
            }
        }

        public void SetAll(float thumb, float index, float middle, float ring, float pinky)
        {
            thumbCurl = Mathf.Clamp01(thumb);
            indexCurl = Mathf.Clamp01(index);
            middleCurl = Mathf.Clamp01(middle);
            ringCurl = Mathf.Clamp01(ring);
            pinkyCurl = Mathf.Clamp01(pinky);
        }

        public static HandPoseData CreateRuntime(float thumb, float index, float middle, float ring, float pinky)
        {
            var pose = CreateInstance<HandPoseData>();
            pose.SetAll(thumb, index, middle, ring, pinky);
            return pose;
        }

        public static HandPoseData Lerp(HandPoseData a, HandPoseData b, float t)
        {
            var result = CreateInstance<HandPoseData>();
            result.thumbCurl = Mathf.Lerp(a.thumbCurl, b.thumbCurl, t);
            result.indexCurl = Mathf.Lerp(a.indexCurl, b.indexCurl, t);
            result.middleCurl = Mathf.Lerp(a.middleCurl, b.middleCurl, t);
            result.ringCurl = Mathf.Lerp(a.ringCurl, b.ringCurl, t);
            result.pinkyCurl = Mathf.Lerp(a.pinkyCurl, b.pinkyCurl, t);
            return result;
        }
    }
}
