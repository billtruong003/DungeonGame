using UnityEngine;
using BillVRCore.Input;

namespace BillVRCore.Locomotion
{
    public class SnapTurnProvider : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private HandSide turnHand = HandSide.Right;
        [SerializeField] private float activateThreshold = 0.7f;
        [SerializeField] private float deactivateThreshold = 0.3f;

        [Header("Snap Turn")]
        [SerializeField] private bool useSnapTurn = true;
        [SerializeField] private float snapAngle = 45f;

        [Header("Smooth Turn")]
        [SerializeField] private float smoothTurnSpeed = 90f;

        private Transform _rigTransform;
        private bool _turnReady = true;

        private void Awake()
        {
            _rigTransform = transform.root;
        }

        private void Update()
        {
            if (InputManager.Instance == null) return;

            Vector2 axis = InputManager.Instance.Input.JoystickAxis(turnHand);
            float horizontal = axis.x;

            if (useSnapTurn)
                ProcessSnapTurn(horizontal);
            else
                ProcessSmoothTurn(horizontal);
        }

        private void ProcessSnapTurn(float horizontal)
        {
            if (Mathf.Abs(horizontal) < deactivateThreshold)
            {
                _turnReady = true;
                return;
            }

            if (!_turnReady) return;
            if (Mathf.Abs(horizontal) < activateThreshold) return;

            float direction = horizontal > 0f ? 1f : -1f;
            RotateRig(direction * snapAngle);
            _turnReady = false;
        }

        private void ProcessSmoothTurn(float horizontal)
        {
            if (Mathf.Abs(horizontal) < deactivateThreshold) return;

            RotateRig(horizontal * smoothTurnSpeed * Time.deltaTime);
        }

        private void RotateRig(float angle)
        {
            if (_rigTransform == null) return;

            Vector3 headWorldPos = Camera.main != null ? Camera.main.transform.position : _rigTransform.position;
            _rigTransform.RotateAround(headWorldPos, Vector3.up, angle);
        }
    }
}
