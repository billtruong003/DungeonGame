using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace VRCore.Input
{
    [DefaultExecutionOrder(-50)]
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        [SerializeField] private InputMode preferredMode = InputMode.LegacyController;
        [SerializeField] private bool autoDetectHandTracking = true;
        [SerializeField] private FingerMappingConfig fingerMapping;

        public IVRInput Input { get; private set; }
        public FingerMappingConfig FingerMapping => fingerMapping;
        public InputMode CurrentMode { get; private set; }
        public event Action<IVRInput, InputMode> OnProviderChanged;

        private readonly List<InputDevice> _handCheckCache = new(4);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            ApplyProvider(preferredMode);
        }

        private void Update()
        {
            if (autoDetectHandTracking) DetectHandTracking();
            Input?.UpdateState();
        }

        public void SwitchMode(InputMode mode)
        {
            if (mode == CurrentMode) return;
            ApplyProvider(mode);
        }

        public void SetPreferredMode(InputMode mode) => preferredMode = mode;
        public void SetAutoDetectHandTracking(bool enabled) => autoDetectHandTracking = enabled;

        public void SetFingerMapping(FingerMappingConfig config) => fingerMapping = config;

        public void SetCustomProvider(IVRInput provider, InputMode mode = InputMode.LegacyController)
        {
            Input = provider;
            CurrentMode = mode;
            OnProviderChanged?.Invoke(Input, mode);
        }

        public float GetFingerCurl(HandSide side, FingerType finger)
        {
            if (fingerMapping != null)
                return fingerMapping.GetCurlForFinger(finger, Input, side);
            return Input.FingerCurl(side, finger);
        }

        public void GetAllFingerCurls(HandSide side, float[] output)
        {
            if (output == null || output.Length < 5) return;
            for (int i = 0; i < 5; i++)
                output[i] = GetFingerCurl(side, (FingerType)i);
        }

        public bool IsPinching(HandSide side)
        {
            if (fingerMapping != null) return fingerMapping.IsPinching(Input, side);
            return Input.FingerCurl(side, FingerType.Thumb) > 0.8f
                && Input.FingerCurl(side, FingerType.Index) > 0.8f;
        }

        public bool IsPointing(HandSide side)
        {
            if (fingerMapping != null) return fingerMapping.IsPointing(Input, side);
            return Input.FingerCurl(side, FingerType.Index) < 0.3f
                && Input.GripStrength(side) > 0.5f;
        }

        public bool IsConnected(HandSide side) => Input?.IsConnected(side) ?? false;

        public bool AnyButtonDown(HandSide side) =>
            Input != null && (Input.GrabPressed(side) || Input.TriggerPressed(side)
                || Input.PrimaryButtonDown(side) || Input.SecondaryButtonDown(side));

        private void ApplyProvider(InputMode mode)
        {
            Input = mode switch
            {
                InputMode.LegacyController => new QuestLegacyInputProvider(),
                InputMode.HandTracking => new HandTrackingInputProvider(),
                InputMode.Desktop => new DesktopSimulatorInput(),
                _ => new QuestLegacyInputProvider()
            };
            CurrentMode = mode;
            OnProviderChanged?.Invoke(Input, mode);
        }

        private void DetectHandTracking()
        {
            if (CurrentMode == InputMode.HandTracking) return;

            _handCheckCache.Clear();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HandTracking, _handCheckCache);

            if (_handCheckCache.Count > 0 && _handCheckCache[0].isValid)
                ApplyProvider(InputMode.HandTracking);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
