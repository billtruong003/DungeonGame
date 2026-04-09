using System;
using UnityEngine;

namespace VRCore.Hand
{
    public class FingerRig : MonoBehaviour
    {
        [SerializeField] private FingerChain[] fingers = new FingerChain[5];
        [SerializeField] private float smoothSpeed = 15f;

        public FingerChain[] Fingers => fingers;

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
                {
                    if (finger.joints[i] != null)
                        finger.openLocalRotations[i] = finger.joints[i].localRotation;
                }
            }
        }

        public void SaveClosedPose()
        {
            foreach (var finger in fingers)
            {
                if (finger.joints == null) continue;

                finger.closedLocalRotations = new Quaternion[finger.joints.Length];
                for (int i = 0; i < finger.joints.Length; i++)
                {
                    if (finger.joints[i] != null)
                        finger.closedLocalRotations[i] = finger.joints[i].localRotation;
                }
            }
        }

        public void SetFingerCurl(FingerType type, float curl)
        {
            int idx = (int)type;
            if (idx < 0 || idx >= fingers.Length) return;
            fingers[idx].targetCurl = Mathf.Clamp01(curl);
        }

        public void SetAllFingerCurls(float thumb, float index, float middle, float ring, float pinky)
        {
            if (fingers.Length > 0) fingers[0].targetCurl = Mathf.Clamp01(thumb);
            if (fingers.Length > 1) fingers[1].targetCurl = Mathf.Clamp01(index);
            if (fingers.Length > 2) fingers[2].targetCurl = Mathf.Clamp01(middle);
            if (fingers.Length > 3) fingers[3].targetCurl = Mathf.Clamp01(ring);
            if (fingers.Length > 4) fingers[4].targetCurl = Mathf.Clamp01(pinky);
        }

        public void SetFromPoseData(HandPoseData pose)
        {
            if (pose == null) return;
            for (int i = 0; i < fingers.Length && i < 5; i++)
                fingers[i].targetCurl = pose.GetCurl((FingerType)i);
        }

        public void SetImmediateCurl(FingerType type, float curl)
        {
            int idx = (int)type;
            if (idx < 0 || idx >= fingers.Length) return;

            var finger = fingers[idx];
            finger.targetCurl = Mathf.Clamp01(curl);
            finger.currentCurl = finger.targetCurl;
            ApplyFingerRotation(finger);
        }

        public void UpdateFingers(float deltaTime)
        {
            float speed = smoothSpeed * deltaTime;

            foreach (var finger in fingers)
            {
                if (!finger.initialized || finger.joints == null) continue;

                finger.currentCurl = Mathf.Lerp(finger.currentCurl, finger.targetCurl, speed);
                ApplyFingerRotation(finger);
            }
        }

        private void ApplyFingerRotation(FingerChain finger)
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

        public float GetCurrentCurl(FingerType type)
        {
            int idx = (int)type;
            if (idx < 0 || idx >= fingers.Length) return 0f;
            return fingers[idx].currentCurl;
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
    }
}
