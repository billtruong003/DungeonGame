using UnityEngine;
using UnityEngine.Events;
using VRCore.Hand;

namespace VRCore.Interaction
{
    [RequireComponent(typeof(Grabbable))]
    public class GrabLock : MonoBehaviour
    {
        [SerializeField] private bool lockOnGrab = true;
        [SerializeField] private UnityEvent onGrabWhileLocked;

        public bool IsLocked { get; private set; }

        private Grabbable _grabbable;
        private GrabHandler _lockedHandler;

        private void Awake()
        {
            _grabbable = GetComponent<Grabbable>();
            _grabbable.OnGrabEvent += OnGrabbed;
            _grabbable.OnReleaseEvent += OnReleased;
        }

        private void OnGrabbed(VRHand hand, Grabbable grab)
        {
            if (lockOnGrab && !IsLocked)
            {
                Lock(hand.GrabHandler);
                return;
            }

            if (IsLocked)
                onGrabWhileLocked?.Invoke();
        }

        private void OnReleased(VRHand hand, Grabbable grab)
        {
            if (IsLocked && hand.GrabHandler == _lockedHandler)
                return;
        }

        public void Lock(GrabHandler handler = null)
        {
            IsLocked = true;
            _lockedHandler = handler;
        }

        public void Unlock()
        {
            IsLocked = false;
            _lockedHandler = null;
        }

        public void UnlockAndRelease()
        {
            IsLocked = false;
            if (_lockedHandler != null)
                _lockedHandler.ForceRelease();
            _lockedHandler = null;
        }

        private void OnDestroy()
        {
            if (_grabbable != null)
            {
                _grabbable.OnGrabEvent -= OnGrabbed;
                _grabbable.OnReleaseEvent -= OnReleased;
            }
        }
    }
}
