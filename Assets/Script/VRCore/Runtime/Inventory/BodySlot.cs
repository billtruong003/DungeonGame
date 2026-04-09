using UnityEngine;
using VRCore.Interaction;

namespace VRCore.Inventory
{
    [RequireComponent(typeof(PlacePoint))]
    public class BodySlot : MonoBehaviour
    {
        [Header("Slot Identity")]
        [SerializeField] private SlotType slotType = SlotType.Any;
        [SerializeField] private string slotName;

        [Header("Body Mount")]
        [SerializeField] private Transform bodyAnchor;
        [SerializeField] private Vector3 localOffset;
        [SerializeField] private Vector3 localRotationEuler;
        [SerializeField] private bool followBody = true;

        [Header("Access")]
        [SerializeField] private float reachRadius = 0.3f;
        [SerializeField] private bool requireLookAt;
        [SerializeField] private float lookAtAngle = 60f;

        public SlotType Type => slotType;
        public string SlotName => string.IsNullOrEmpty(slotName) ? slotType.ToString() : slotName;
        public PlacePoint PlacePoint { get; private set; }
        public bool HasItem => PlacePoint != null && PlacePoint.HasPlacedObject;
        public Grabbable StoredItem => PlacePoint?.PlacedObject;

        private Transform _headCamera;

        private void Awake()
        {
            PlacePoint = GetComponent<PlacePoint>();
        }

        private void Start()
        {
            var cam = Camera.main;
            if (cam != null) _headCamera = cam.transform;
        }

        private void LateUpdate()
        {
            if (!followBody || bodyAnchor == null) return;

            transform.position = bodyAnchor.TransformPoint(localOffset);
            transform.rotation = bodyAnchor.rotation * Quaternion.Euler(localRotationEuler);
        }

        public bool IsAccessible(Vector3 handPosition)
        {
            float dist = Vector3.Distance(handPosition, transform.position);
            if (dist > reachRadius) return false;

            if (requireLookAt && _headCamera != null)
            {
                Vector3 toSlot = (transform.position - _headCamera.position).normalized;
                float angle = Vector3.Angle(_headCamera.forward, toSlot);
                if (angle > lookAtAngle) return false;
            }

            return true;
        }

        public bool CanAcceptItem(InventoryItem item)
        {
            if (item == null) return false;
            return item.FitsSlot(slotType);
        }

        public void SetBodyAnchor(Transform anchor)
        {
            bodyAnchor = anchor;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = HasItem ? Color.green : new Color(1f, 0.6f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, reachRadius);

            if (bodyAnchor != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(bodyAnchor.position, transform.position);
            }
        }
    }
}
