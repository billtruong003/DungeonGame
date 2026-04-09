using UnityEngine;
using VRCore.Hand;

namespace VRCore.Interaction
{
    public class Pushable : MonoBehaviour
    {
        [SerializeField] private Vector3 pushStrength = Vector3.one;
        [SerializeField] private float pushAcceleration = 20f;
        [SerializeField] private float pushDrag = 10f;

        public Vector3 Strength => pushStrength;
        public float Acceleration => pushAcceleration;
        public float Drag => pushDrag;
    }
}
