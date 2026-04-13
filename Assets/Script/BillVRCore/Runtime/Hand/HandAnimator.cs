using UnityEngine;
using BillVRCore.Input;
using BillVRCore.Interaction;

namespace BillVRCore.Hand
{
    [RequireComponent(typeof(VRHand))]
    [DefaultExecutionOrder(10000)]
    public class HandAnimator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private FingerRig fingerRig;

        [Header("Sway")]
        [SerializeField] private float swayStrength = 0.4f;
        [SerializeField] private float swaySmooth = 30f;

        [Header("Grip Offset")]
        [SerializeField] [Range(0f, 1f)] private float gripOffset = 0.14f;

        [Header("Pose Transition")]
        [SerializeField] private float defaultTransitionTime = 0.3f;
        [SerializeField] private AnimationCurve defaultTransitionCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [SerializeField] private float smoothPoseBlend = 0.33f;

        private VRHand _hand;
        private GrabHandler _grabHandler;
        private float _swayVelocity;

        private HandPose _openPose;
        private HandPose _closePose;
        private HandPose _inputPose;
        private HandPose _targetPose;
        private HandPose _currentPose;
        private HandPose _smoothPose;

        private float _poseStartTime;
        private float _poseStopTime;
        private float _poseTransitionDuration;
        private AnimationCurve _activeTransitionCurve;
        private bool _poseActive;

        private PoseArea _currentPoseArea;
        private HandPoseAsset _overridePoseAsset;
        private float _overrideBlend;

        public PoseArea CurrentPoseArea => _currentPoseArea;
        public bool IsPosing => _poseActive || _currentPoseArea != null;

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

            _openPose = HandPose.CreateEmpty();
            _closePose = HandPose.CreateEmpty();
            _inputPose = HandPose.CreateEmpty();
            _targetPose = HandPose.CreateEmpty();
            _currentPose = HandPose.CreateEmpty();
            _smoothPose = HandPose.CreateEmpty();

            _activeTransitionCurve = defaultTransitionCurve ?? AnimationCurve.Linear(0, 0, 1, 1);

            CaptureOpenClosePoses();
        }

        private void LateUpdate()
        {
            if (fingerRig == null || InputManager.Instance == null) return;

            if (_hand.IsHolding && _grabHandler.IsHolding)
            {
                ApplyHeldPose();
            }
            else
            {
                UpdateInputPose();
                UpdateTargetPoseTransition();
                ApplyCurrentPose();
            }
        }

        private void CaptureOpenClosePoses()
        {
            if (fingerRig == null) return;

            foreach (var chain in fingerRig.Fingers)
            {
                if (chain.joints == null || chain.joints.Length == 0) continue;

                int idx = (int)chain.type;
                if (idx < 0 || idx >= 5) continue;

                var savedRotations = new Quaternion[chain.joints.Length];
                for (int j = 0; j < chain.joints.Length; j++)
                    if (chain.joints[j] != null)
                        savedRotations[j] = chain.joints[j].localRotation;

                if (chain.openLocalRotations != null)
                {
                    _openPose.fingerPoses[idx] = new FingerPoseData(chain.joints.Length);
                    for (int j = 0; j < chain.joints.Length; j++)
                        if (chain.joints[j] != null)
                            chain.joints[j].localRotation = chain.openLocalRotations[j];
                    _openPose.fingerPoses[idx].CaptureFrom(_hand.transform, chain.joints);
                }

                if (chain.closedLocalRotations != null)
                {
                    _closePose.fingerPoses[idx] = new FingerPoseData(chain.joints.Length);
                    for (int j = 0; j < chain.joints.Length; j++)
                        if (chain.joints[j] != null)
                            chain.joints[j].localRotation = chain.closedLocalRotations[j];
                    _closePose.fingerPoses[idx].CaptureFrom(_hand.transform, chain.joints);
                }

                for (int j = 0; j < chain.joints.Length; j++)
                    if (chain.joints[j] != null)
                        chain.joints[j].localRotation = savedRotations[j];
            }
        }

        private void UpdateInputPose()
        {
            float directionalSway = ComputeDirectionalSway();
            float grip = gripOffset + swayStrength * directionalSway;

            IVRInput input = InputManager.Instance.Input;
            HandSide side = _hand.Side;

            for (int i = 0; i < 5; i++)
            {
                FingerType finger = (FingerType)i;
                float curl = InputManager.Instance.GetFingerCurl(side, finger);
                float blendedCurl = Mathf.Clamp01(grip + curl);

                if (_openPose.fingerPoses[i].IsValid && _closePose.fingerPoses[i].IsValid)
                    _inputPose.fingerPoses[i].Lerp(ref _openPose.fingerPoses[i], ref _closePose.fingerPoses[i], blendedCurl);
                else
                    fingerRig.SetFingerCurl(finger, blendedCurl);
            }
        }

        private void UpdateTargetPoseTransition()
        {
            float t;

            if (_poseActive)
            {
                float elapsed = (Time.time - _poseStartTime) / Mathf.Max(_poseTransitionDuration, 0.001f);
                t = _activeTransitionCurve.Evaluate(Mathf.Clamp01(elapsed));
            }
            else
            {
                float elapsed = (Time.time - _poseStopTime) / Mathf.Max(_poseTransitionDuration, 0.001f);
                t = _activeTransitionCurve.Evaluate(1f - Mathf.Clamp01(elapsed));
            }

            bool inputValid = _inputPose.fingerPoses != null && _inputPose.fingerPoses[0].IsValid;
            bool targetValid = _targetPose.fingerPoses != null && _targetPose.fingerPoses[0].IsValid;

            if (t > 0.001f && t < 0.999f && inputValid && targetValid)
                _currentPose.Lerp(ref _inputPose, ref _targetPose, t);
            else if (_poseActive && targetValid)
                _currentPose.CopyFrom(ref _targetPose);
            else if (inputValid)
                _currentPose.CopyFrom(ref _inputPose);

            if (_smoothPose.IsValid && _currentPose.IsValid)
                _smoothPose.LerpTo(ref _currentPose, smoothPoseBlend);
            else if (_currentPose.IsValid)
                _smoothPose.CopyFrom(ref _currentPose);
        }

        private void ApplyCurrentPose()
        {
            if (!_smoothPose.IsValid) return;

            bool usePoseData = _openPose.fingerPoses != null && _openPose.fingerPoses[0].IsValid;

            if (usePoseData)
            {
                for (int i = 0; i < 5 && i < fingerRig.Fingers.Length; i++)
                {
                    var chain = fingerRig.Fingers[i];
                    if (chain.joints == null || !_smoothPose.fingerPoses[i].IsValid) continue;
                    _smoothPose.fingerPoses[i].ApplyTo(_hand.transform.rotation, chain.joints);
                }
            }
            else
            {
                fingerRig.UpdateFingers(Time.deltaTime);
            }
        }

        private float ComputeDirectionalSway()
        {
            Vector3 averageVel = Vector3.zero;
            var history = _hand.PositionHistory;

            for (int i = 1; i < history.Length; i++)
                averageVel += history[i - 1] - history[i];

            averageVel /= Mathf.Max(history.Length - 1, 1);

            if (transform.parent != null)
                averageVel = (Quaternion.Inverse(_hand.PalmTransform.rotation) * transform.parent.rotation) * averageVel;

            float vel = (averageVel * 60f).z;
            float target = vel;

            _swayVelocity = Mathf.MoveTowards(_swayVelocity, target, Time.deltaTime * Mathf.Abs(_swayVelocity - target) * swaySmooth);
            return _swayVelocity;
        }

        public void ApplyHeldPose()
        {
            if (fingerRig == null || !_hand.IsHolding) return;

            var held = _grabHandler.HeldObject;
            var snapGrabbable = held as SnapGrabbable;

            if (snapGrabbable != null && snapGrabbable.HasCustomPose)
            {
                for (int i = 0; i < 5; i++)
                {
                    FingerType finger = (FingerType)i;
                    float snapCurl = snapGrabbable.GetFingerCurl(finger);
                    float inputCurl = InputManager.Instance.GetFingerCurl(_hand.Side, finger);
                    fingerRig.SetFingerCurl(finger, Mathf.Max(snapCurl, inputCurl));
                }
                fingerRig.UpdateFingers(Time.deltaTime);
                return;
            }

            for (int i = 0; i < 5; i++)
            {
                FingerType finger = (FingerType)i;
                float inputCurl = InputManager.Instance.GetFingerCurl(_hand.Side, finger);
                fingerRig.SetFingerCurl(finger, Mathf.Max(inputCurl, 0.6f));
            }
            fingerRig.UpdateFingers(Time.deltaTime);
        }

        public void SetTargetPose(ref HandPose pose, float transitionTime, AnimationCurve curve)
        {
            _activeTransitionCurve = curve ?? defaultTransitionCurve;
            _poseTransitionDuration = transitionTime;
            _poseStartTime = Time.time;
            _poseActive = true;
            _targetPose.CopyFrom(ref pose);

            if (transitionTime <= 0f)
                _currentPose.CopyFrom(ref pose);
        }

        public void SetTargetPose(ref HandPose pose, float transitionTime)
        {
            SetTargetPose(ref pose, transitionTime, defaultTransitionCurve);
        }

        public void SetTargetPose(ref HandPose pose)
        {
            SetTargetPose(ref pose, defaultTransitionTime, defaultTransitionCurve);
        }

        public void CancelPose(float transitionOutTime)
        {
            _poseTransitionDuration = transitionOutTime;
            _poseStopTime = Time.time;
            _poseActive = false;
        }

        public void CancelPose() => CancelPose(defaultTransitionTime);

        public void EnterPoseArea(PoseArea area)
        {
            if (_hand.IsHolding || area == null) return;
            if (!area.HasPose(_hand.Side)) return;

            if (_currentPoseArea != null && _currentPoseArea != area)
                ExitPoseArea();

            _currentPoseArea = area;
            var pose = area.GetPose(_hand.Side);
            SetTargetPose(ref pose, area.TransitionTime, area.TransitionCurve);
            area.NotifyHandEnter(_hand);
        }

        public void ExitPoseArea()
        {
            if (_currentPoseArea == null) return;

            _currentPoseArea.NotifyHandExit(_hand);
            CancelPose();
            _currentPoseArea = null;
        }

        public void SetOverridePose(HandPoseAsset asset, float blend = 1f)
        {
            _overridePoseAsset = asset;
            _overrideBlend = Mathf.Clamp01(blend);
        }

        public void ClearOverridePose()
        {
            _overridePoseAsset = null;
            _overrideBlend = 0f;
        }

        public ref HandPose GetCurrentPose() => ref _currentPose;
        public ref HandPose GetInputPose() => ref _inputPose;
        public FingerRig GetFingerRig() => fingerRig;
        public float GripOffset => gripOffset;
        public float SwayStrength => swayStrength;

        public void SetGripOffset(float offset) => gripOffset = offset;
        public void SetSwayStrength(float strength) => swayStrength = strength;
        public void SetTransitionTime(float time) => defaultTransitionTime = time;
    }
}
