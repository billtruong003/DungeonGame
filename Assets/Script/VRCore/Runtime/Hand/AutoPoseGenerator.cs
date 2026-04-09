using UnityEngine;

namespace VRCore.Hand
{
    public static class AutoPoseGenerator
    {
        private const int CurlSteps = 20;
        private const float StepIncrement = 1f / CurlSteps;

        public static HandPoseData GenerateGrabPose(FingerRig fingerRig, Transform palm, Collider targetCollider)
        {
            if (fingerRig == null || targetCollider == null)
                return HandPoseData.CreateRuntime(0.8f, 0.8f, 0.8f, 0.8f, 0.8f);

            var pose = HandPoseData.CreateRuntime(0f, 0f, 0f, 0f, 0f);

            foreach (var finger in fingerRig.Fingers)
            {
                if (finger.joints == null || finger.joints.Length == 0) continue;

                float bestCurl = FindContactCurl(fingerRig, finger, targetCollider, palm);
                pose.SetCurl(finger.type, bestCurl);
            }

            return pose;
        }

        private static float FindContactCurl(FingerRig rig, FingerRig.FingerChain finger,
            Collider target, Transform palm)
        {
            float originalCurl = finger.currentCurl;

            for (float curl = 0f; curl <= 1f; curl += StepIncrement)
            {
                rig.SetImmediateCurl(finger.type, curl);

                Vector3 tipPos = finger.tip != null
                    ? finger.tip.position
                    : finger.joints[^1].position;

                if (Physics.CheckSphere(tipPos, finger.tipRadius * 1.5f, 1 << target.gameObject.layer))
                {
                    rig.SetImmediateCurl(finger.type, originalCurl);
                    return Mathf.Clamp01(curl + StepIncrement);
                }

                Vector3 closestPoint = target.ClosestPoint(tipPos);
                float distance = Vector3.Distance(tipPos, closestPoint);

                if (distance < finger.tipRadius * 2f)
                {
                    rig.SetImmediateCurl(finger.type, originalCurl);
                    return Mathf.Clamp01(curl);
                }
            }

            rig.SetImmediateCurl(finger.type, originalCurl);
            return 0.85f;
        }

        public static HandPoseData GenerateFromClosestPoints(FingerRig fingerRig, Collider targetCollider)
        {
            if (fingerRig == null || targetCollider == null)
                return HandPoseData.CreateRuntime(0.8f, 0.8f, 0.8f, 0.8f, 0.8f);

            var pose = HandPoseData.CreateRuntime(0f, 0f, 0f, 0f, 0f);

            foreach (var finger in fingerRig.Fingers)
            {
                if (finger.joints == null || finger.joints.Length == 0) continue;

                Vector3 tipPos = finger.tip != null
                    ? finger.tip.position
                    : finger.joints[^1].position;

                Vector3 basePos = finger.joints[0].position;
                Vector3 closestToTip = targetCollider.ClosestPoint(tipPos);
                Vector3 closestToBase = targetCollider.ClosestPoint(basePos);

                float fingerLength = EstimateFingerLength(finger);
                float distanceToSurface = Vector3.Distance(tipPos, closestToTip);
                float normalizedDist = Mathf.Clamp01(1f - (distanceToSurface / fingerLength));

                pose.SetCurl(finger.type, normalizedDist);
            }

            return pose;
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
