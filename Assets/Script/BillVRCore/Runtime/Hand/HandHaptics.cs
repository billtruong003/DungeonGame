using UnityEngine;
using UnityEngine.XR;

namespace BillVRCore.Hand
{
    [RequireComponent(typeof(VRHand))]
    public class HandHaptics : MonoBehaviour
    {
        [Header("Collision Haptics")]
        [SerializeField] private bool enableCollisionHaptics = true;
        [SerializeField] private LayerMask hapticLayers = ~0;
        [SerializeField] private float minImpactVelocity = 0.5f;
        [SerializeField] private float maxImpactVelocity = 5f;
        [SerializeField] private float collisionCooldown = 0.1f;

        [Header("Intensity")]
        [SerializeField] [Range(0f, 1f)] private float baseAmplitude = 0.3f;
        [SerializeField] private float baseDuration = 0.05f;

        private VRHand _hand;
        private float _lastCollisionTime;

        private void Awake()
        {
            _hand = GetComponent<VRHand>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!enableCollisionHaptics) return;
            if (Time.time - _lastCollisionTime < collisionCooldown) return;
            if ((hapticLayers & (1 << collision.gameObject.layer)) == 0) return;

            float impactSpeed = collision.relativeVelocity.magnitude;
            if (impactSpeed < minImpactVelocity) return;

            float normalizedImpact = Mathf.InverseLerp(minImpactVelocity, maxImpactVelocity, impactSpeed);
            float amplitude = Mathf.Lerp(baseAmplitude * 0.3f, baseAmplitude, normalizedImpact);
            float duration = Mathf.Lerp(baseDuration, baseDuration * 3f, normalizedImpact);

            PlayHaptic(amplitude, duration);
            _lastCollisionTime = Time.time;
        }

        public void PlayHaptic(float amplitude, float duration)
        {
            XRNode node = _hand.Side == HandSide.Left ? XRNode.LeftHand : XRNode.RightHand;
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);

            if (device.isValid)
                device.SendHapticImpulse(0, Mathf.Clamp01(amplitude), duration);
        }

        public void PlayGrabHaptic()
        {
            PlayHaptic(baseAmplitude * 0.5f, 0.03f);
        }

        public void PlayReleaseHaptic()
        {
            PlayHaptic(baseAmplitude * 0.3f, 0.02f);
        }

        public void PlayUIHaptic()
        {
            PlayHaptic(0.1f, 0.02f);
        }
    }
}
