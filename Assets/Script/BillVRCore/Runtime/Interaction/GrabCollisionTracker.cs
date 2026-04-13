using UnityEngine;

namespace BillVRCore.Interaction
{
    /// <summary>
    /// Lightweight helper added to held objects during grab.
    /// Tracks collision contacts so GrabHandler can project velocity
    /// away from blocked surfaces instead of pushing through them.
    /// </summary>
    [DisallowMultipleComponent]
    internal class GrabCollisionTracker : MonoBehaviour
    {
        private Vector3 _normalSum;
        private int _contactCount;
        private float _lastContactTime;

        public bool HasContact => _contactCount > 0 && (Time.fixedTime - _lastContactTime) < Time.fixedDeltaTime * 2f;
        public Vector3 AverageNormal => _contactCount > 0 ? _normalSum.normalized : Vector3.zero;
        public int ContactCount => _contactCount;

        private void OnCollisionStay(Collision collision)
        {
            for (int i = 0; i < collision.contactCount; i++)
            {
                _normalSum += collision.GetContact(i).normal;
                _contactCount++;
            }
            _lastContactTime = Time.fixedTime;
        }

        /// <summary>
        /// Called by GrabHandler after reading contact state each physics frame.
        /// </summary>
        public void ConsumeContacts()
        {
            _normalSum = Vector3.zero;
            _contactCount = 0;
        }
    }
}
