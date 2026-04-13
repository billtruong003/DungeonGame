using System;
using UnityEngine;

namespace BillVRCore.Hand
{
    [Serializable]
    public struct FingerPoseData
    {
        public Quaternion[] jointRotations;

        public bool IsValid => jointRotations != null && jointRotations.Length >= 3;

        public FingerPoseData(int jointCount)
        {
            jointRotations = new Quaternion[jointCount];
            for (int i = 0; i < jointCount; i++)
                jointRotations[i] = Quaternion.identity;
        }

        public FingerPoseData(Transform handRoot, Transform[] joints)
        {
            jointRotations = new Quaternion[joints.Length];
            CaptureFrom(handRoot, joints);
        }

        public FingerPoseData(ref FingerPoseData other)
        {
            jointRotations = new Quaternion[other.jointRotations.Length];
            Array.Copy(other.jointRotations, jointRotations, other.jointRotations.Length);
        }

        public void CaptureFrom(Transform handRoot, Transform[] joints)
        {
            if (jointRotations == null || jointRotations.Length != joints.Length)
                jointRotations = new Quaternion[joints.Length];

            if (joints.Length == 0) return;

            jointRotations[0] = Quaternion.Inverse(handRoot.rotation) * joints[0].rotation;

            for (int i = 1; i < joints.Length; i++)
                jointRotations[i] = Quaternion.Inverse(joints[i - 1].rotation) * joints[i].rotation;
        }

        public void ApplyTo(Quaternion handWorldRotation, Transform[] joints)
        {
            if (!IsValid || joints == null || joints.Length == 0) return;

            int count = Mathf.Min(jointRotations.Length, joints.Length);
            Quaternion accumulated = handWorldRotation * jointRotations[0];

            if (joints[0] != null) joints[0].rotation = accumulated;

            for (int i = 1; i < count; i++)
            {
                accumulated *= jointRotations[i];
                if (joints[i] != null) joints[i].rotation = accumulated;
            }
        }

        public void CopyFrom(ref FingerPoseData other)
        {
            if (!other.IsValid) return;

            if (jointRotations == null || jointRotations.Length != other.jointRotations.Length)
                jointRotations = new Quaternion[other.jointRotations.Length];

            Array.Copy(other.jointRotations, jointRotations, other.jointRotations.Length);
        }

        public void Lerp(ref FingerPoseData from, ref FingerPoseData to, float t)
        {
            if (!from.IsValid || !to.IsValid) return;

            int count = Mathf.Min(from.jointRotations.Length, to.jointRotations.Length);
            if (jointRotations == null || jointRotations.Length != count)
                jointRotations = new Quaternion[count];

            for (int i = 0; i < count; i++)
                jointRotations[i] = Quaternion.Lerp(from.jointRotations[i], to.jointRotations[i], t);
        }

        public void LerpTo(ref FingerPoseData target, float t)
        {
            if (!IsValid || !target.IsValid) return;

            int count = Mathf.Min(jointRotations.Length, target.jointRotations.Length);
            for (int i = 0; i < count; i++)
                jointRotations[i] = Quaternion.Lerp(jointRotations[i], target.jointRotations[i], t);
        }

        public float AngleDifference(ref FingerPoseData other)
        {
            if (!IsValid || !other.IsValid) return float.MaxValue;

            float total = 0f;
            int count = Mathf.Min(jointRotations.Length, other.jointRotations.Length);
            for (int i = 0; i < count; i++)
                total += Quaternion.Angle(jointRotations[i], other.jointRotations[i]);

            return total;
        }

        public static FingerPoseData Identity(int jointCount)
        {
            return new FingerPoseData(jointCount);
        }
    }
}
