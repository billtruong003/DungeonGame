using UnityEngine;

namespace BillVRCore.Hand
{
    public static class AutoPoseGenerator
    {
        private const int CurlSteps = 20;
        private const float StepIncrement = 1f / CurlSteps;

        public struct CurlResult
        {
            public float thumb, index, middle, ring, pinky;

            public float Get(FingerType type) => type switch
            {
                FingerType.Thumb => thumb,
                FingerType.Index => index,
                FingerType.Middle => middle,
                FingerType.Ring => ring,
                FingerType.Pinky => pinky,
                _ => 0f
            };

            public void Set(FingerType type, float value)
            {
                switch (type)
                {
                    case FingerType.Thumb: thumb = value; break;
                    case FingerType.Index: index = value; break;
                    case FingerType.Middle: middle = value; break;
                    case FingerType.Ring: ring = value; break;
                    case FingerType.Pinky: pinky = value; break;
                }
            }

            public static CurlResult Default() => new CurlResult
                { thumb = 0.8f, index = 0.8f, middle = 0.8f, ring = 0.8f, pinky = 0.8f };
        }

        public static CurlResult GenerateGrabPose(FingerRig fingerRig, Transform palm, Collider targetCollider)
        {
            if (fingerRig == null || targetCollider == null)
                return CurlResult.Default();

            var result = new CurlResult();

            foreach (var finger in fingerRig.Fingers)
            {
                if (finger.joints == null || finger.joints.Length == 0) continue;
                result.Set(finger.type, FindContactCurl(fingerRig, finger, targetCollider, palm));
            }

            return result;
        }

        public static CurlResult GenerateFromClosestPoints(FingerRig fingerRig, Collider targetCollider)
        {
            if (fingerRig == null || targetCollider == null)
                return CurlResult.Default();

            var result = new CurlResult();

            foreach (var finger in fingerRig.Fingers)
            {
                if (finger.joints == null || finger.joints.Length == 0) continue;

                Vector3 tipPos = finger.tip != null ? finger.tip.position : finger.joints[^1].position;
                Vector3 closestToTip = targetCollider.ClosestPoint(tipPos);
                float fingerLength = EstimateFingerLength(finger);
                float distanceToSurface = Vector3.Distance(tipPos, closestToTip);

                result.Set(finger.type, Mathf.Clamp01(1f - distanceToSurface / fingerLength));
            }

            return result;
        }

        public static HandPoseAsset GenerateGrabPoseAsset(FingerRig fingerRig, Transform palm, Collider targetCollider)
        {
            var curls = GenerateGrabPose(fingerRig, palm, targetCollider);
            var asset = ScriptableObject.CreateInstance<HandPoseAsset>();
            asset.thumbCurl = curls.thumb;
            asset.indexCurl = curls.index;
            asset.middleCurl = curls.middle;
            asset.ringCurl = curls.ring;
            asset.pinkyCurl = curls.pinky;
            return asset;
        }

        private static float FindContactCurl(FingerRig rig, FingerRig.FingerChain finger,
            Collider target, Transform palm)
        {
            float originalCurl = finger.currentCurl;

            for (float curl = 0f; curl <= 1f; curl += StepIncrement)
            {
                rig.SetImmediateCurl(finger.type, curl);

                Vector3 tipPos = finger.tip != null ? finger.tip.position : finger.joints[^1].position;

                if (Physics.CheckSphere(tipPos, finger.tipRadius * 1.5f, 1 << target.gameObject.layer))
                {
                    rig.SetImmediateCurl(finger.type, originalCurl);
                    return Mathf.Clamp01(curl + StepIncrement);
                }

                float distance = Vector3.Distance(tipPos, target.ClosestPoint(tipPos));
                if (distance < finger.tipRadius * 2f)
                {
                    rig.SetImmediateCurl(finger.type, originalCurl);
                    return Mathf.Clamp01(curl);
                }
            }

            rig.SetImmediateCurl(finger.type, originalCurl);
            return 0.85f;
        }

        private static float EstimateFingerLength(FingerRig.FingerChain finger)
        {
            float length = 0f;
            for (int i = 0; i < finger.joints.Length - 1; i++)
            {
                if (finger.joints[i] == null || finger.joints[i + 1] == null) continue;
                length += Vector3.Distance(finger.joints[i].position, finger.joints[i + 1].position);
            }
            return Mathf.Max(length, 0.01f);
        }
    }
}
