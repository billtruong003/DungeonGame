using System;
using UnityEngine;

namespace VRCore.Locomotion
{
    [DefaultExecutionOrder(50)]
    public class LocomotionStateMachine : MonoBehaviour
    {
        public static LocomotionStateMachine Instance { get; private set; }

        public LocomotionState CurrentState { get; private set; } = LocomotionState.Idle;
        public LocomotionState PreviousState { get; private set; } = LocomotionState.Idle;
        public event Action<LocomotionState, LocomotionState> OnStateChanged;

        private ILocomotionProvider[] _providers;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            _providers = GetComponentsInChildren<ILocomotionProvider>();
            SortByPriority();
        }

        private void FixedUpdate()
        {
            LocomotionState newState = EvaluateState();

            if (newState != CurrentState)
            {
                PreviousState = CurrentState;
                CurrentState = newState;
                NotifyProviders();
                OnStateChanged?.Invoke(PreviousState, CurrentState);
            }
        }

        private LocomotionState EvaluateState()
        {
            if (_providers == null) return LocomotionState.Idle;

            foreach (var provider in _providers)
            {
                if (provider.IsActive)
                    return provider.ProvidedState;
            }

            return LocomotionState.Idle;
        }

        private void NotifyProviders()
        {
            if (_providers == null) return;

            foreach (var provider in _providers)
            {
                bool shouldBeActive = provider.ProvidedState == CurrentState;
                provider.SetLocomotionActive(shouldBeActive);
            }
        }

        private void SortByPriority()
        {
            if (_providers == null) return;
            Array.Sort(_providers, (a, b) => b.Priority.CompareTo(a.Priority));
        }

        public bool IsMoving => CurrentState != LocomotionState.Idle;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }

    public interface ILocomotionProvider
    {
        bool IsActive { get; }
        LocomotionState ProvidedState { get; }
        int Priority { get; }
        void SetLocomotionActive(bool active);
    }
}
