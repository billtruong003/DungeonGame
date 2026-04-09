using UnityEngine;
using VRCore.Input;

namespace VRCore.Locomotion
{
    public class SmoothTurnProvider : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private HandSide turnHand = HandSide.Right;
        [SerializeField] private float deadzone = 0.15f;
        [SerializeField] private float turnSpeed = 90f;

        private Transform _rigTransform;

        private void Awake()
        {
            _rigTransform = transform.root;
        }

        private void Update()
        {
            if (InputManager.Instance == null) return;

            float horizontal = InputManager.Instance.Input.JoystickAxis(turnHand).x;
            if (Mathf.Abs(horizontal) < deadzone) return;

            float angle = horizontal * turnSpeed * Time.deltaTime;
            Vector3 pivot = Camera.main != null ? Camera.main.transform.position : _rigTransform.position;
            _rigTransform.RotateAround(pivot, Vector3.up, angle);
        }
    }
}
