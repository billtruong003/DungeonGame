using System.Collections.Generic;
using UnityEngine;
using VRCore.Hand;

namespace VRCore.Interaction
{
    public class DispenserPoint : MonoBehaviour
    {
        [Header("Dispensing")]
        [SerializeField] private GameObject itemPrefab;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private float cooldown = 0.5f;
        [SerializeField] private int maxActive = 5;

        [Header("Detection")]
        [SerializeField] private float grabRadius = 0.1f;
        [SerializeField] private LayerMask handLayers;

        public System.Action<Grabbable> OnDispensed;

        private float _lastDispenseTime;
        private readonly List<GameObject> _activeItems = new();

        private void Awake()
        {
            if (spawnPoint == null) spawnPoint = transform;
        }

        private void Update()
        {
            CleanupDestroyedItems();
        }

        private void OnTriggerEnter(Collider other)
        {
            var hand = other.GetComponentInParent<VRHand>();
            if (hand == null) return;
            if (hand.GrabHandler.IsHolding) return;

            TryDispense(hand);
        }

        public Grabbable TryDispense(VRHand hand = null)
        {
            if (itemPrefab == null) return null;
            if (Time.time - _lastDispenseTime < cooldown) return null;
            if (_activeItems.Count >= maxActive) return null;

            _lastDispenseTime = Time.time;

            var spawned = Instantiate(itemPrefab, spawnPoint.position, spawnPoint.rotation);
            _activeItems.Add(spawned);

            var grabbable = spawned.GetComponent<Grabbable>();
            if (grabbable == null)
                grabbable = spawned.GetComponentInChildren<Grabbable>();

            OnDispensed?.Invoke(grabbable);
            return grabbable;
        }

        private void CleanupDestroyedItems()
        {
            _activeItems.RemoveAll(item => item == null);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.5f);
            Gizmos.DrawWireSphere(spawnPoint != null ? spawnPoint.position : transform.position, grabRadius);
        }
    }
}
