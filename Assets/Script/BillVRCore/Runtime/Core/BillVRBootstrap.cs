using UnityEngine;
using BillVRCore.Hand;
using BillVRCore.Input;

namespace BillVRCore
{
    [DefaultExecutionOrder(-100)]
    public class BillVRBootstrap : MonoBehaviour
    {
        [Header("Auto Create If Missing")]
        [SerializeField] private bool autoCreateInputManager = true;
        [SerializeField] private InputMode defaultInputMode = InputMode.LegacyController;
        [SerializeField] private FingerMappingConfig fingerMapping;

        private void Awake()
        {
            EnsureInputManager();
            ValidateHands();
        }

        private void EnsureInputManager()
        {
            if (InputManager.Instance != null) return;
            if (!autoCreateInputManager) return;

            var inputGo = new GameObject("[BillVR] InputManager");
            inputGo.transform.SetParent(transform);
            var manager = inputGo.AddComponent<InputManager>();

            manager.SetPreferredMode(defaultInputMode);
            if (fingerMapping != null)
                manager.SetFingerMapping(fingerMapping);
        }

        private void ValidateHands()
        {
            var hands = FindObjectsByType<VRHand>(FindObjectsSortMode.None);

            bool hasLeft = false, hasRight = false;
            foreach (var hand in hands)
            {
                if (hand.Side == HandSide.Left) hasLeft = true;
                if (hand.Side == HandSide.Right) hasRight = true;
            }

#if UNITY_EDITOR
            if (hands.Length == 0)
                Debug.LogWarning("[BillVR] No VRHand components found in scene.");
            if (!hasLeft)
                Debug.LogWarning("[BillVR] No Left VRHand found.");
            if (!hasRight)
                Debug.LogWarning("[BillVR] No Right VRHand found.");
#endif
        }
    }
}
