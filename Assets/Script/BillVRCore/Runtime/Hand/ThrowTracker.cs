using UnityEngine;

namespace BillVRCore.Hand
{
    public class ThrowTracker
    {
        private readonly VRHand _hand;
        private readonly VelocitySample[] _velocitySamples;
        private readonly VelocitySample[] _angularSamples;
        private int _velocityCount;
        private int _angularCount;
        private int _velocityHead;
        private int _angularHead;

        private const int MaxSamples = 20;
        private const float VelocityWindow = 0.125f;
        private const float AngularWindow = 0.25f;

        private struct VelocitySample
        {
            public Vector3 velocity;
            public float time;
        }

        public ThrowTracker(VRHand hand)
        {
            _hand = hand;
            _velocitySamples = new VelocitySample[MaxSamples];
            _angularSamples = new VelocitySample[MaxSamples];
        }

        public void RecordVelocity(Vector3 linearVelocity, Vector3 angularVelocity)
        {
            float now = Time.time;

            _velocitySamples[_velocityHead] = new VelocitySample { velocity = linearVelocity, time = now };
            _velocityHead = (_velocityHead + 1) % MaxSamples;
            _velocityCount = Mathf.Min(_velocityCount + 1, MaxSamples);

            _angularSamples[_angularHead] = new VelocitySample { velocity = angularVelocity, time = now };
            _angularHead = (_angularHead + 1) % MaxSamples;
            _angularCount = Mathf.Min(_angularCount + 1, MaxSamples);
        }

        public Vector3 GetThrowVelocity()
        {
            return ComputeWindowedAverage(_velocitySamples, _velocityCount, _velocityHead, VelocityWindow) * _hand.ThrowPower;
        }

        public Vector3 GetThrowAngularVelocity()
        {
            return ComputeWindowedAverage(_angularSamples, _angularCount, _angularHead, AngularWindow) * Mathf.Sqrt(_hand.ThrowPower) * 0.5f;
        }

        public void Clear()
        {
            _velocityCount = 0;
            _angularCount = 0;
            _velocityHead = 0;
            _angularHead = 0;
        }

        private Vector3 ComputeWindowedAverage(VelocitySample[] samples, int count, int head, float window)
        {
            if (count == 0) return Vector3.zero;

            float cutoff = Time.time - window;
            Vector3 sum = Vector3.zero;
            int validCount = 0;

            for (int i = 0; i < count; i++)
            {
                int idx = (head - 1 - i + MaxSamples) % MaxSamples;
                if (samples[idx].time < cutoff) break;
                sum += samples[idx].velocity;
                validCount++;
            }

            return validCount > 0 ? sum / validCount : Vector3.zero;
        }
    }
}
