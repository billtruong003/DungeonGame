using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BillVRCore.Interaction;

namespace BillVRCore.Hand
{
    [RequireComponent(typeof(VRHand))]
    public class HandHighlighter : MonoBehaviour
    {
        [SerializeField] private Material highlightMaterial;
        [SerializeField] private float highlightScale = 1.05f;
        [SerializeField] private float updateRate = 1f / 30f;
        [SerializeField] [Range(0f, 1f)] private float directionBias = 0.65f;

        private VRHand _hand;
        private GrabHandler _grabHandler;
        private Grabbable _currentTarget;
        private readonly Dictionary<Renderer, GameObject> _highlightObjects = new();
        private Coroutine _highlightRoutine;

        private void Awake()
        {
            _hand = GetComponent<VRHand>();
            _grabHandler = GetComponent<GrabHandler>();
        }

        private void OnEnable()
        {
            _highlightRoutine = StartCoroutine(HighlightLoop());
        }

        private void OnDisable()
        {
            if (_highlightRoutine != null)
                StopCoroutine(_highlightRoutine);

            ClearAllHighlights();
        }

        private IEnumerator HighlightLoop()
        {
            yield return new WaitForEndOfFrame();

            if (_hand.Side == HandSide.Left)
                yield return new WaitForSecondsRealtime(updateRate * 0.5f);

            while (true)
            {
                UpdateHighlight();
                yield return new WaitForSecondsRealtime(updateRate);
            }
        }

        private void UpdateHighlight()
        {
            if (_grabHandler.IsHolding)
            {
                if (_currentTarget != null)
                    RemoveHighlight(_currentTarget);
                _currentTarget = null;
                return;
            }

            Grabbable newTarget = _grabHandler.HoveredObject;

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

                highlightGo.AddComponent<MeshFilter>().sharedMesh = meshFilter.sharedMesh;

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

        private void ClearAllHighlights()
        {
            foreach (var kvp in _highlightObjects)
                if (kvp.Value != null) Destroy(kvp.Value);

            _highlightObjects.Clear();
            _currentTarget = null;
        }

        public void SetDirectionBias(float bias) => directionBias = Mathf.Clamp01(bias);
        public void SetUpdateRate(float rate) => updateRate = Mathf.Max(rate, 0.01f);
    }
}
