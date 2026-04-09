using System.Collections.Generic;
using UnityEngine;
using VRCore.Interaction;

namespace VRCore.Hand
{
    [RequireComponent(typeof(VRHand))]
    public class HandHighlighter : MonoBehaviour
    {
        [SerializeField] private Material highlightMaterial;
        [SerializeField] private float highlightScale = 1.05f;

        private VRHand _hand;
        private GrabHandler _grabHandler;
        private Grabbable _currentTarget;
        private readonly Dictionary<Renderer, GameObject> _highlightObjects = new();

        private void Awake()
        {
            _hand = GetComponent<VRHand>();
            _grabHandler = GetComponent<GrabHandler>();
        }

        private void LateUpdate()
        {
            Grabbable newTarget = _grabHandler.IsHolding ? null : _grabHandler.HoveredObject;

            if (newTarget == _currentTarget) return;

            if (_currentTarget != null)
                RemoveHighlight(_currentTarget);

            _currentTarget = newTarget;

            if (_currentTarget != null && highlightMaterial != null)
                ApplyHighlight(_currentTarget);
        }

        private void ApplyHighlight(Grabbable target)
        {
            var renderers = target.GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in renderers)
            {
                if (_highlightObjects.ContainsKey(renderer)) continue;

                var meshFilter = renderer.GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null) continue;

                var highlightGo = new GameObject("_Highlight");
                highlightGo.transform.SetParent(renderer.transform, false);
                highlightGo.transform.localPosition = Vector3.zero;
                highlightGo.transform.localRotation = Quaternion.identity;
                highlightGo.transform.localScale = Vector3.one * highlightScale;

                var mf = highlightGo.AddComponent<MeshFilter>();
                mf.sharedMesh = meshFilter.sharedMesh;

                var mr = highlightGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = highlightMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;

                _highlightObjects[renderer] = highlightGo;
            }
        }

        private void RemoveHighlight(Grabbable target)
        {
            var renderers = target.GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in renderers)
            {
                if (_highlightObjects.TryGetValue(renderer, out var highlightGo))
                {
                    Destroy(highlightGo);
                    _highlightObjects.Remove(renderer);
                }
            }
        }

        private void OnDisable()
        {
            ClearAllHighlights();
        }

        private void ClearAllHighlights()
        {
            foreach (var kvp in _highlightObjects)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value);
            }
            _highlightObjects.Clear();
            _currentTarget = null;
        }
    }
}
