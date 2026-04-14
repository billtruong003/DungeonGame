using UnityEngine;

namespace RPGModular
{
    public class Portal : MonoBehaviour
    {
        [SerializeField] private ZoneData targetZone;
        [SerializeField] private string targetSpawnID;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            ZoneSystem.Instance?.TravelTo(targetZone, targetSpawnID);
        }
    }
}
