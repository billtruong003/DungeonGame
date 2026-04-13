using System;
using UnityEngine;

namespace BillVRCore.Hand
{
    public class FingerRig : MonoBehaviour
    {
        [SerializeField] private FingerChain[] fingers = new FingerChain[5];
        [SerializeField] private float smoothSpeed = 15f;
        [SerializeField] private bool usePoseDataMode;

        public FingerChain[] Fingers => fingers;
        public bool UsePoseDataMode => usePoseDataMode;

        [Serializable]
        public class FingerChain
        {
            public FingerType type;
            public Transform[] joints;
            public Transform tip;
            public float tipRadius = 0.008f;

            [HideInInspector] public Quaternion[] openLocalRotations;
            [HideInInspector] public Quaternion[] closedLocalRotations;
            [HideInInspector] public float currentCurl;
            [HideInInspector] public float targetCurl;
            [HideInInspector] public bool initialized;

            [HideInInspector] public FingerPoseData currentPoseData;
            [HideInInspector] public FingerPoseData targetPoseData;
            [HideInInspector] public bool hasPoseTarget;
        }

        public void Initialize()
        {
            foreach (var finger in fingers)
            {
                if (finger.joints == null || finger.joints.Length == 0) continue;

                finger.openLocalRotations = new Quaternion[finger.joints.Length];
                finger.closedLocalRotations = new Quaternion[finger.joints.Length];

                for (int i = 0; i < finger.joints.Length; i++)
                {
                    if (finger.joints[i] == null) continue;
                    finger.openLocalRotations[i] = finger.joints[i].localRotation;
                    finger.closedLocalRotations[i] = finger.joints[i].localRotation;
                }

                finger.currentPoseData = new FingerPoseData(finger.joints.Length);
                finger.targetPoseData = new FingerPoseData(finger.joints.Length);
                finger.initialized = true;
            }
        }

        public void SaveOpenPose()
        {
            foreach (var finger in fingers)
            {
                if (finger.joints == null) continue;
                finger.openLocalRotations = new Quaternion[finger.joints.Length];
                for (int i = 0; i < finger.joints.Length; i++)
                    if (finger.joints[i] != null)
                        finger.openLocalRotations[i] = finger.joints[i].localRotation;
            }
        }

        public void SaveClosedPose()
        {
            foreach (var finger in fingers)
            {
                if (finger.joints == null) continue;
                finger.closedLocalRotations = new Quaternion[finger.joints.Length];
                for (int i = 0; i < finger.joints.Length; i++)
                    if (finger.joints[i] != null)
                        finger.closedLocalRotations[i] = finger.joints[i].localRotation;
            }
        }

        public void SetFingerCurl(FingerType type, float curl)
        {
            int idx = (int)type;
            if (idx < 0 || idx >= fingers.Length) return;
            fingers[idx].targetCurl = Mathf.Clamp01(curl);
            fingers[idx].hasPoseTarget = false;
        }

        public void SetFingerPose(FingerType type, ref FingerPoseData poseData)
        {
            int idx = (int)type;
            if (idx < 0 || idx >= fingers.Length) return;
            fingers[idx].targetPoseData.CopyFrom(ref poseData);
            fingers[idx].hasPoseTarget = true;
        }

        public void SetFullPose(ref HandPose pose)
        {
            if (!pose.IsValid) return;
            for (int i = 0; i < 5 && i < fingers.Length; i++)
            {
                if (!pose.fingerPoses[i].IsValid) continue;
                fingers[i].targetPoseData.CopyFrom(ref pose.fingerPoses[i]);
                fingers[i].hasPoseTarget = true;
            }
        }

        public void SetFromPoseData(HandPoseAsset asset)
        {
            if (asset == null) return;
            SetAllFingerCurls(asset.thumbCurl, asset.indexCurl, asset.middleCurl, asset.ringCurl, asset.pinkyCurl);
        }

        public void SetAllFingerCurls(float thumb, float index, float middle, float ring, float pinky)
        {
            if (fingers.Length > 0) { fingers[0].targetCurl = Mathf.Clamp01(thumb); fingers[0].hasPoseTarget = false; }
            if (fingers.Length > 1) { fingers[1].targetCurl = Mathf.Clamp01(index); fingers[1].hasPoseTarget = false; }
            if (fingers.Length > 2) { fingers[2].targetCurl = Mathf.Clamp01(middle); fingers[2].hasPoseTarget = false; }
            if (fingers.Length > 3) { fingers[3].targetCurl = Mathf.Clamp01(ring); fingers[3].hasPoseTarget = false; }
            if (fingers.Length > 4) { fingers[4].targetCurl = Mathf.Clamp01(pinky); fingers[4].hasPoseTarget = false; }
        }

        public void SetImmediateCurl(FingerType type, float curl)
        {
            int idx = (int)type;
            if (idx < 0 || idx >= fingers.Length) return;

            var finger = fingers[idx];
            finger.targetCurl = Mathf.Clamp01(curl);
            finger.currentCurl = finger.targetCurl;
            finger.hasPoseTarget = false;
            ApplyCurlRotation(finger);
        }

        public void UpdateFingers(float deltaTime)
        {
            float speed = smoothSpeed * deltaTime;

            foreach (var finger in fingers)
            {
                if (!finger.initialized || finger.joints == null) continue;

                if (finger.hasPoseTarget && finger.targetPoseData.IsValid)
                {
                    finger.currentPoseData.LerpTo(ref finger.targetPoseData, speed);
                    ApplyPoseDataRotation(finger);
                }
                else
                {
                    finger.currentCurl = Mathf.Lerp(finger.currentCurl, finger.targetCurl, speed);
                    ApplyCurlRotation(finger);
                }
            }
        }

        private void ApplyCurlRotation(FingerChain finger)
        {
            for (int i = 0; i < finger.joints.Length; i++)
            {
                if (finger.joints[i] == null) continue;
                if (finger.openLocalRotations == null || finger.closedLocalRotations == null) continue;
                if (i >= finger.openLocalRotations.Length || i >= finger.closedLocalRotations.Length) continue;

                finger.joints[i].localRotation = Quaternion.Slerp(
                    finger.openLocalRotations[i],
                    finger.closedLocalRotations[i],
                    finger.currentCurl);
            }
        }

        private void ApplyPoseDataRotation(FingerChain finger)
        {
            if (!finger.currentPoseData.IsValid || finger.joints == null) return;

            Transform handRoot = transform.parent ?? transform;
            Quaternion handRot = handRoot.rotation;
            finger.currentPoseData.ApplyTo(handRot, finger.joints);
        }

        public void CapturePoseData(Transform handRoot)
        {
            foreach (var finger in fingers)
            {
                if (finger.joints == null || finger.joints.Length == 0) continue;
                finger.currentPoseData.CaptureFrom(handRoot, finger.joints);
                finger.targetPoseData.CopyFrom(ref finger.currentPoseData);
            }
        }

        public float GetCurrentCurl(FingerType type)
        {
            int idx = (int)type;
            if (idx < 0 || idx >= fingers.Length) return 0f;
            return fingers[idx].currentCurl;
        }

        public ref FingerPoseData GetCurrentPoseData(FingerType type)
        {
            int idx = (int)type;
            return ref fingers[idx].currentPoseData;
        }

        public Vector3 GetTipPosition(FingerType type)
        {
            int idx = (int)type;
            if (idx < 0 || idx >= fingers.Length) return transform.position;
            if (fingers[idx].tip != null) return fingers[idx].tip.position;
            if (fingers[idx].joints != null && fingers[idx].joints.Length > 0)
                return fingers[idx].joints[^1].position;
            return transform.position;
        }

        public void SetSmoothSpeed(float speed) => smoothSpeed = speed;
        public void SetPoseDataMode(bool enabled) => usePoseDataMode = enabled;
        public void SetFingerChains(FingerChain[] chains) => fingers = chains;
    }
}
