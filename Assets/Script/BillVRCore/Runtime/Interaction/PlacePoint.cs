using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using BillVRCore.Hand;

namespace BillVRCore.Interaction
{
    [RequireComponent(typeof(SphereCollider))]
    public class PlacePoint : MonoBehaviour
    {
        [Header("Placement")]
        [SerializeField] private Transform placedOffset;
        [SerializeField] private float placeRadius = 0.12f;
        [SerializeField] private bool parentOnPlace = true;
        [SerializeField] private bool forcePlace;
        [SerializeField] private bool forceHandRelease = true;
        [SerializeField] private bool makeKinematicOnPlace = true;
        [SerializeField] private bool disableGrabOnPlace;

        [Header("Filtering")]
        [SerializeField] private string[] allowedTags;
        [SerializeField] private string[] blacklistTags;
        [SerializeField] private Grabbable[] onlyAllows;

        [Header("Visual")]
        [SerializeField] private GameObject highlightVisual;

        [Header("Events")]
        [SerializeField] private UnityEvent<PlacePoint, Grabbable> onPlace;
        [SerializeField] private UnityEvent<PlacePoint, Grabbable> onRemove;

        public Grabbable PlacedObject { get; private set; }
        public Grabbable HighlightingObject { get; private set; }
        public bool HasPlacedObject => PlacedObject != null;

        public event Action<PlacePoint, Grabbable> OnPlaceEvent;
        public event Action<PlacePoint, Grabbable> OnRemoveEvent;
        public event Action<PlacePoint, Grabbable> OnHighlightEvent;
        public event Action<PlacePoint, Grabbable> OnStopHighlightEvent;

        private SphereCollider _trigger;
        private readonly HashSet<Grabbable> _candidates = new();
        private bool _placedWasKinematic;
        private Rigidbody _placedOriginalRb;

        private void Awake()
        {
            _trigger = GetComponent<SphereCollider>();
            _trigger.isTrigger = true;
            _trigger.radius = placeRadius;

            if (placedOffset == null)
                placedOffset = transform;

            if (highlightVisual != null)
                highlightVisual.SetActive(false);
        }

        private void OnTriggerStay(Collider other)
        {
            var grabbable = other.GetComponentInParent<Grabbable>();
            if (grabbable == null) return;
            if (!CanPlace(grabbable)) return;

            _candidates.Add(grabbable);

            if (forcePlace && grabbable.IsHeld)
            {
                ExecutePlace(grabbable);
                return;
            }

            if (!grabbable.IsHeld && PlacedObject == null)
            {
                ExecutePlace(grabbable);
                return;
            }

            UpdateHighlight(grabbable);
        }

        private void OnTriggerExit(Collider other)
        {
            var grabbable = other.GetComponentInParent<Grabbable>();
            if (grabbable == null) return;

            _candidates.Remove(grabbable);

            if (HighlightingObject == grabbable)
                ClearHighlight();
        }

        private void Update()
        {
            if (HighlightingObject != null && !HighlightingObject.IsHeld && PlacedObject == null)
            {
                ExecutePlace(HighlightingObject);
            }
        }

        public bool CanPlace(Grabbable grabbable)
        {
            if (PlacedObject != null) return false;
            if (grabbable == null) return false;

            if (onlyAllows != null && onlyAllows.Length > 0)
            {
                bool found = false;
                foreach (var allowed in onlyAllows)
                {
                    if (allowed == grabbable) { found = true; break; }
                }
                if (!found) return false;
            }

            string objTag = grabbable.gameObject.tag;

            if (allowedTags != null && allowedTags.Length > 0)
            {
                bool tagMatch = false;
                foreach (var tag in allowedTags)
                {
                    if (objTag == tag) { tagMatch = true; break; }
                }
                if (!tagMatch) return false;
            }

            if (blacklistTags != null)
            {
                foreach (var tag in blacklistTags)
                {
                    if (objTag == tag) return false;
                }
            }

            return true;
        }

        public void Place(Grabbable grabbable)
        {
            if (!CanPlace(grabbable)) return;
            ExecutePlace(grabbable);
        }

        public void Remove()
        {
            if (PlacedObject == null) return;
            ExecuteRemove();
        }

        private void ExecutePlace(Grabbable grabbable)
        {
            if (forceHandRelease && grabbable.IsHeld)
                grabbable.ForceRelease();

            PlacedObject = grabbable;
            ClearHighlight();

            _placedOriginalRb = grabbable.Rb;
            _placedWasKinematic = grabbable.Rb.isKinematic;

            if (makeKinematicOnPlace)
                grabbable.Rb.isKinematic = true;

            if (parentOnPlace)
                grabbable.transform.SetParent(placedOffset);

            grabbable.transform.position = placedOffset.position;
            grabbable.transform.rotation = placedOffset.rotation;

            grabbable.Rb.linearVelocity = Vector3.zero;
            grabbable.Rb.angularVelocity = Vector3.zero;

            if (disableGrabOnPlace)
                grabbable.enabled = false;

            grabbable.OnGrabEvent += OnPlacedObjectGrabbed;

            onPlace?.Invoke(this, grabbable);
            OnPlaceEvent?.Invoke(this, grabbable);
        }

        private void ExecuteRemove()
        {
            var removed = PlacedObject;
            PlacedObject = null;

            removed.OnGrabEvent -= OnPlacedObjectGrabbed;

            if (makeKinematicOnPlace)
                removed.Rb.isKinematic = _placedWasKinematic;

            if (disableGrabOnPlace)
                removed.enabled = true;

            if (parentOnPlace)
                removed.transform.SetParent(null);

            onRemove?.Invoke(this, removed);
            OnRemoveEvent?.Invoke(this, removed);
        }

        private void OnPlacedObjectGrabbed(VRHand hand, Grabbable grabbable)
        {
            if (grabbable == PlacedObject)
                ExecuteRemove();
        }

        private void UpdateHighlight(Grabbable grabbable)
        {
            if (HighlightingObject == grabbable) return;

            ClearHighlight();
            HighlightingObject = grabbable;

            if (highlightVisual != null)
                highlightVisual.SetActive(true);

            OnHighlightEvent?.Invoke(this, grabbable);
        }

        private void ClearHighlight()
        {
            if (HighlightingObject == null) return;

            var prev = HighlightingObject;
            HighlightingObject = null;

            if (highlightVisual != null)
                highlightVisual.SetActive(false);

            OnStopHighlightEvent?.Invoke(this, prev);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = HasPlacedObject ? Color.green : new Color(1f, 0.8f, 0f, 0.5f);
            Gizmos.DrawWireSphere(placedOffset != null ? placedOffset.position : transform.position, placeRadius);
        }
    }
}
