using UnityEngine;
using VRCore.Hand;
using VRCore.Input;
using VRCore.Locomotion;

namespace VRCore.DebugTools
{
    public class VRCoreDebugOverlay : MonoBehaviour
    {
        [SerializeField] private bool showOverlay = true;
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;
        [SerializeField] private int fontSize = 14;
        [SerializeField] private float refreshInterval = 0.15f;

        private GUIStyle _labelStyle;
        private GUIStyle _headerStyle;
        private float _fps;
        private float _fpsTimer;
        private int _frameCount;

        private string _cachedText;
        private float _lastRefreshTime;

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(toggleKey))
                showOverlay = !showOverlay;

            _frameCount++;
            _fpsTimer += Time.unscaledDeltaTime;
            if (_fpsTimer >= 0.5f)
            {
                _fps = _frameCount / _fpsTimer;
                _frameCount = 0;
                _fpsTimer = 0f;
            }

            if (showOverlay && Time.unscaledTime - _lastRefreshTime > refreshInterval)
            {
                _lastRefreshTime = Time.unscaledTime;
                RebuildCachedText();
            }
        }

        private void OnGUI()
        {
            if (!showOverlay || _cachedText == null) return;

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = fontSize,
                    normal = { textColor = Color.white },
                    richText = true
                };
            }

            GUI.Label(new Rect(10, 10, 600, 800), _cachedText, _labelStyle);
        }

        private void RebuildCachedText()
        {
            var sb = new System.Text.StringBuilder(512);

            sb.Append("<color=cyan><b>FPS: ").Append((int)_fps).Append("</b></color>\n");

            if (InputManager.Instance != null)
            {
                var input = InputManager.Instance.Input;
                sb.Append("<color=cyan><b>Input: ").Append(InputManager.Instance.CurrentMode).Append("</b></color>\n");

                AppendHand(sb, input, HandSide.Left, "L");
                AppendHand(sb, input, HandSide.Right, "R");
            }

            var hands = FindObjectsByType<VRHand>(FindObjectsSortMode.None);
            for (int i = 0; i < hands.Length; i++)
            {
                var h = hands[i];
                sb.Append(h.Side == HandSide.Left ? "L" : "R").Append(" Hand: ");
                sb.Append(h.GrabHandler != null ? h.GrabHandler.State.ToString() : "N/A");
                if (h.IsHolding) sb.Append(" [").Append(h.HeldObject.name).Append(']');
                sb.Append('\n');
            }

            if (LocomotionStateMachine.Instance != null)
            {
                sb.Append("<color=cyan><b>Loco: ").Append(LocomotionStateMachine.Instance.CurrentState).Append("</b></color>\n");

                var body = FindFirstObjectByType<VRPlayerBody>();
                if (body != null)
                {
                    sb.Append("  Ground: ").Append(body.IsGrounded ? "Y" : "N");
                    sb.Append(" | Vel: ").Append(body.HorizontalSpeed.ToString("F1")).Append("m/s");
                    sb.Append(" | H: ").Append(body.CurrentHeight.ToString("F2")).Append("m\n");
                }
            }

            _cachedText = sb.ToString();
        }

        private void AppendHand(System.Text.StringBuilder sb, IVRInput input, HandSide side, string prefix)
        {
            sb.Append("  ").Append(prefix);
            sb.Append(" G:").Append(input.GripStrength(side).ToString("F1"));
            sb.Append(" T:").Append(input.TriggerStrength(side).ToString("F1"));
            var axis = input.JoystickAxis(side);
            sb.Append(" J:(").Append(axis.x.ToString("F1")).Append(',').Append(axis.y.ToString("F1")).Append(")\n");
        }
    }
}
