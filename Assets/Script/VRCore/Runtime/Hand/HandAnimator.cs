using UnityEngine;
using VRCore.Input;
using VRCore.Interaction;

namespace VRCore.Hand
{
    [RequireComponent(typeof(VRHand))]
    public class HandAnimator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private FingerRig fingerRig;

        [Header("Sway")]
        [SerializeField] private float swayStrength = 0.05f;
        [SerializeField] private float swaySmooth = 8f;

        [Header("Grip Offset")]
        [SerializeField] [Range(0f, 1f)] private float gripOffset = 0.14f;

        private VRHand _hand;
        private GrabHandler _grabHandler;
        private Vector3 _previousPosition;
        private float _swayValue;
        private HandPoseData _overridePose;
        private float _overrideBlend;

        private void Awake()
        {
            _hand = GetComponent<VRHand>();
            _grabHandler = GetComponent<GrabHandler>();

            if (fingerRig == null)
                fingerRig = GetComponentInChildren<FingerRig>();
        }

        private void Start()
        {
            if (fingerRig != null)
                fingerRig.Initialize();

            _previousPosition = transform.position;
        }

        private void LateUpdate()
        {
            if (fingerRig == null || InputManager.Instance == null) return;

            UpdateSway();
            UpdateFingerTargets();
            fingerRig.UpdateFingers(Time.deltaTime);
        }

        private void UpdateFingerTargets()
        {
            IVRInput input = InputManager.Instance.Input;
            HandSide side = _hand.Side;

            if (_overridePose != null && _overrideBlend > 0.01f)
            {
                ApplyOverridePose(input, side);
                return;
            }

            if (_grabHandler.IsHolding)
            {
                ApplyHeldPose(input, side);
                return;
            }

            ApplyIdlePose(input, side);
        }

        private void ApplyIdlePose(IVRInput input, HandSide side)
        {
            for (int i = 0; i < 5; i++)
            {
                FingerType finger = (FingerType)i;
                float curl = InputManager.Instance.GetFingerCurl(side, finger);
                curl = Mathf.Clamp01(curl + gripOffset + _swayValue);
                fingerRig.SetFingerCurl(finger, curl);
            }
        }

        private void ApplyHeldPose(IVRInput input, HandSide side)
        {
            var held = _grabHandler.HeldObject;
            var snapGrabbable = held as SnapGrabbable;

            if (snapGrabbable != null && snapGrabbable.HasCustomPose)
            {
                for (int i = 0; i < 5; i++)
                {
                    FingerType finger = (FingerType)i;
                    float snapCurl = snapGrabbable.GetFingerCurl(finger);
                    float inputCurl = InputManager.Instance.GetFingerCurl(side, finger);
                    float blended = Mathf.Max(snapCurl, inputCurl);
                    fingerRig.SetFingerCurl(finger, blended);
                }
                return;
            }

            for (int i = 0; i < 5; i++)
            {
                FingerType finger = (FingerType)i;
                float inputCurl = InputManager.Instance.GetFingerCurl(side, finger);
                float minGrabCurl = 0.6f;
                fingerRig.SetFingerCurl(finger, Mathf.Max(inputCurl, minGrabCurl));
            }
        }

        private void ApplyOverridePose(IVRInput input, HandSide side)
        {
            for (int i = 0; i < 5; i++)
            {
                FingerType finger = (FingerType)i;
                float inputCurl = InputManager.Instance.GetFingerCurl(side, finger);
                inputCurl = Mathf.Clamp01(inputCurl + gripOffset);

                float overrideCurl = _overridePose.GetCurl(finger);
                float blended = Mathf.Lerp(inputCurl, overrideCurl, _overrideBlend);
                fingerRig.SetFingerCurl(finger, blended);
            }
        }

        private void UpdateSway()
        {
            Vector3 velocity = (transform.position - _previousPosition) / Time.deltaTime;
            _previousPosition = transform.position;

            float targetSway = velocity.magnitude * swayStrength;
            _swayValue = Mathf.Lerp(_swayValue, targetSway, Time.deltaTime * swaySmooth);
            _swayValue = Mathf.Clamp(_swayValue, -0.2f, 0.2f);
        }

        public void SetOverridePose(HandPoseData pose, float blend = 1f)
        {
            _overridePose = pose;
            _overrideBlend = Mathf.Clamp01(blend);
        }

        public void ClearOverridePose()
        {
            _overridePose = null;
            _overrideBlend = 0f;
        }

        public FingerRig GetFingerRig() => fingerRig;
    }
}
