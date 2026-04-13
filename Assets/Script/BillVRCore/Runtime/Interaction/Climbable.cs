using UnityEngine;

namespace BillVRCore.Interaction
{
    [RequireComponent(typeof(Grabbable))]
    public class Climbable : MonoBehaviour
    {
        [SerializeField] private float climbStrengthMultiplier = 1f;
        [SerializeField] private bool allowMovementWhileClimbing;

        public float StrengthMultiplier => climbStrengthMultiplier;
        public bool AllowMovement => allowMovementWhileClimbing;

        public void SetStrength(float strength) => climbStrengthMultiplier = strength;
        public void SetAllowMovement(bool allow) => allowMovementWhileClimbing = allow;

        private void Start()
        {
            var grabbable = GetComponent<Grabbable>();
            if (grabbable != null)
                grabbable.SetParentOnGrab(false);
        }
    }
}
