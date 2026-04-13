using System;
using UnityEngine;

namespace BillVRCore.Hand
{
    [Serializable]
    public struct HandPose
    {
        public FingerPoseData[] fingerPoses;
        public Vector3 handOffset;
        public Quaternion rotationOffset;
        public Vector3 handScale;

        public bool IsValid => fingerPoses != null && fingerPoses.Length == 5 && fingerPoses[0].IsValid;

        public HandPose(VRHand hand)
        {
            fingerPoses = new FingerPoseData[5];
            handOffset = Vector3.zero;
            rotationOffset = Quaternion.identity;
            handScale = hand.transform.lossyScale;
            CaptureFrom(hand, null);
        }

        public HandPose(VRHand hand, Transform relativeTo)
        {
            fingerPoses = new FingerPoseData[5];
            handOffset = Vector3.zero;
            rotationOffset = Quaternion.identity;
            handScale = hand.transform.lossyScale;
            CaptureFrom(hand, relativeTo);
        }

        public HandPose(ref HandPose other)
        {
            fingerPoses = new FingerPoseData[5];
            handOffset = other.handOffset;
            rotationOffset = other.rotationOffset;
            handScale = other.handScale;

            for (int i = 0; i < 5; i++)
                fingerPoses[i] = new FingerPoseData(ref other.fingerPoses[i]);
        }

        public void CaptureFrom(VRHand hand, Transform relativeTo)
        {
            var rig = hand.Animator?.GetFingerRig();
            if (rig == null) return;

            if (fingerPoses == null || fingerPoses.Length != 5)
                fingerPoses = new FingerPoseData[5];

            foreach (var chain in rig.Fingers)
            {
                if (chain.joints == null || chain.joints.Length == 0) continue;
                int idx = (int)chain.type;
                if (idx < 0 || idx >= 5) continue;
                fingerPoses[idx].CaptureFrom(hand.transform, chain.joints);
            }

            if (relativeTo != null)
            {
                handOffset = relativeTo.InverseTransformPoint(hand.transform.position);
                rotationOffset = Quaternion.Inverse(relativeTo.rotation) * hand.transform.rotation;
            }
            else
            {
                handOffset = Vector3.zero;
                rotationOffset = Quaternion.identity;
            }

            handScale = hand.transform.lossyScale;
        }

        public void ApplyFingers(VRHand hand)
        {
            if (!IsValid) return;

            var rig = hand.Animator?.GetFingerRig();
            if (rig == null) return;

            Quaternion handRot = hand.transform.rotation;
            foreach (var chain in rig.Fingers)
            {
                int idx = (int)chain.type;
                if (idx < 0 || idx >= 5 || !fingerPoses[idx].IsValid) continue;
                fingerPoses[idx].ApplyTo(handRot, chain.joints);
            }
        }

        public void ApplyPosition(VRHand hand, Transform relativeTo)
        {
            if (relativeTo == null) return;
            Vector3 worldPos = relativeTo.TransformPoint(handOffset);
            Quaternion worldRot = relativeTo.rotation * rotationOffset;
            hand.transform.SetPositionAndRotation(worldPos, worldRot);
        }

        public void CopyFrom(ref HandPose other)
        {
            if (!other.IsValid) return;

            if (fingerPoses == null || fingerPoses.Length != 5)
                fingerPoses = new FingerPoseData[5];

            for (int i = 0; i < 5; i++)
                fingerPoses[i].CopyFrom(ref other.fingerPoses[i]);

            handOffset = other.handOffset;
            rotationOffset = other.rotationOffset;
            handScale = other.handScale;
        }

        public void Lerp(ref HandPose from, ref HandPose to, float t)
        {
            if (!from.IsValid || !to.IsValid) return;

            if (fingerPoses == null || fingerPoses.Length != 5)
                fingerPoses = new FingerPoseData[5];

            for (int i = 0; i < 5; i++)
                fingerPoses[i].Lerp(ref from.fingerPoses[i], ref to.fingerPoses[i], t);

            handOffset = Vector3.Lerp(from.handOffset, to.handOffset, t);
            rotationOffset = Quaternion.Lerp(from.rotationOffset, to.rotationOffset, t);
            handScale = Vector3.Lerp(from.handScale, to.handScale, t);
        }

        public void LerpTo(ref HandPose target, float t)
        {
            if (!IsValid || !target.IsValid) return;

            for (int i = 0; i < 5; i++)
                fingerPoses[i].LerpTo(ref target.fingerPoses[i], t);

            handOffset = Vector3.Lerp(handOffset, target.handOffset, t);
            rotationOffset = Quaternion.Lerp(rotationOffset, target.rotationOffset, t);
        }

        public static HandPose CreateEmpty()
        {
            var pose = new HandPose
            {
                fingerPoses = new FingerPoseData[5],
                handOffset = Vector3.zero,
                rotationOffset = Quaternion.identity,
                handScale = Vector3.one
            };

            for (int i = 0; i < 5; i++)
                pose.fingerPoses[i] = new FingerPoseData(3);

            return pose;
        }

        public static HandPose FromCurlValues(VRHand hand, float thumb, float index, float middle, float ring, float pinky)
        {
            var rig = hand.Animator?.GetFingerRig();
            if (rig == null) return CreateEmpty();

            float[] curls = { thumb, index, middle, ring, pinky };
            var savedRotations = new Quaternion[5][];

            foreach (var chain in rig.Fingers)
            {
                int idx = (int)chain.type;
                if (idx < 0 || idx >= 5 || chain.joints == null) continue;

                savedRotations[idx] = new Quaternion[chain.joints.Length];
                for (int j = 0; j < chain.joints.Length; j++)
                    if (chain.joints[j] != null)
                        savedRotations[idx][j] = chain.joints[j].localRotation;

                rig.SetImmediateCurl(chain.type, curls[idx]);
            }

            var pose = new HandPose(hand);

            foreach (var chain in rig.Fingers)
            {
                int idx = (int)chain.type;
                if (idx < 0 || idx >= 5 || savedRotations[idx] == null) continue;

                for (int j = 0; j < chain.joints.Length; j++)
                    if (chain.joints[j] != null)
                        chain.joints[j].localRotation = savedRotations[idx][j];
            }

            return pose;
        }
    }

    [CreateAssetMenu(fileName = "HandPose", menuName = "BillVR/Hand Pose Asset")]
    public class HandPoseAsset : ScriptableObject
    {
        public HandPose leftPose;
        public HandPose rightPose;
        public bool leftSaved;
        public bool rightSaved;

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

        public void SetAll(float thumb, float index, float middle, float ring, float pinky)
        {
            thumbCurl = Mathf.Clamp01(thumb);
            indexCurl = Mathf.Clamp01(index);
            middleCurl = Mathf.Clamp01(middle);
            ringCurl = Mathf.Clamp01(ring);
            pinkyCurl = Mathf.Clamp01(pinky);
        }

        public void SetCurl(FingerType finger, float curl)
        {
            curl = Mathf.Clamp01(curl);
            switch (finger)
            {
                case FingerType.Thumb:  thumbCurl  = curl; break;
                case FingerType.Index:  indexCurl  = curl; break;
                case FingerType.Middle: middleCurl = curl; break;
                case FingerType.Ring:   ringCurl   = curl; break;
                case FingerType.Pinky:  pinkyCurl  = curl; break;
            }
        }

        public bool HasPose(HandSide side) => side == HandSide.Left ? leftSaved : rightSaved;

        public ref HandPose GetPose(HandSide side) => ref side == HandSide.Left ? ref leftPose : ref rightPose;

        public void SavePose(VRHand hand, Transform relativeTo = null)
        {
            if (hand.Side == HandSide.Left)
            {
                leftPose = new HandPose(hand, relativeTo);
                leftSaved = true;
            }
            else
            {
                rightPose = new HandPose(hand, relativeTo);
                rightSaved = true;
            }
        }
    }
}
