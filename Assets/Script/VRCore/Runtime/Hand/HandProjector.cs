using UnityEngine;
using VRCore.Input;

namespace VRCore.Hand
{
    [RequireComponent(typeof(VRHand))]
    public class HandProjector : MonoBehaviour
    {
        [Header("Projection")]
        [SerializeField] private bool enableProjection = true;
        [SerializeField] private Material ghostMaterial;
        [SerializeField] private float ghostAlpha = 0.3f;
        [SerializeField] private float transitionSpeed = 12f;

        [Header("Detection")]
        [SerializeField] private float projectionRadius = 0.12f;
        [SerializeField] private LayerMask grabLayers = ~0;

        public bool IsProjecting { get; private set; }
        public Interaction.Grabbable ProjectedTarget { get; private set; }

        private VRHand _hand;
        private GrabHandler _grabHandler;
        private GameObject _ghostInstance;
        private MeshRenderer[] _ghostRenderers;
        private float _currentAlpha;
        private Collider[] _overlapBuffer = new Collider[8];
        private Transform _projectedAttachPoint;
        private Material[] _ghostMaterials;

        private void Awake()
        {
            _hand = GetComponent<VRHand>();
            _grabHandler = GetComponent<GrabHandler>();
        }

        private void LateUpdate()
        {
            if (!enableProjection || _grabHandler.IsHolding)
            {
                FadeOut();
                return;
            }

            var target = FindBestTarget();

            if (target != null)
                ProjectToTarget(target);
            else
                FadeOut();

            UpdateGhostAlpha();
        }

        private Interaction.Grabbable FindBestTarget()
        {
            Vector3 palmPos = _hand.PalmTransform != null
                ? _hand.PalmTransform.position : transform.position;

            int count = Physics.OverlapSphereNonAlloc(palmPos, projectionRadius,
                _overlapBuffer, grabLayers);

            Interaction.Grabbable closest = null;
            float closestDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var grabbable = _overlapBuffer[i].GetComponentInParent<Interaction.Grabbable>();
                if (grabbable == null || !grabbable.CanBeGrabbedBy(_hand)) continue;

                float dist = Vector3.Distance(palmPos, _overlapBuffer[i].ClosestPoint(palmPos));
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = grabbable;
                }
            }

            return closest;
        }

        private void ProjectToTarget(Interaction.Grabbable target)
        {
            ProjectedTarget = target;
            IsProjecting = true;

            EnsureGhostInstance();

            var snapGrabbable = target as Interaction.SnapGrabbable;
            if (snapGrabbable != null)
            {
                Transform grip = snapGrabbable.GetGripPoint(_hand.Side);
                if (grip != null)
                {
                    _ghostInstance.transform.position = Vector3.Lerp(
                        _ghostInstance.transform.position, grip.position,
                        Time.deltaTime * transitionSpeed);
                    _ghostInstance.transform.rotation = Quaternion.Slerp(
                        _ghostInstance.transform.rotation, grip.rotation,
                        Time.deltaTime * transitionSpeed);
                    return;
                }
            }

            Vector3 targetPos = target.GetComponent<Collider>() != null
                ? target.GetComponent<Collider>().ClosestPoint(
                    _hand.PalmTransform != null ? _hand.PalmTransform.position : transform.position)
                : target.transform.position;

            _ghostInstance.transform.position = Vector3.Lerp(
                _ghostInstance.transform.position, targetPos,
                Time.deltaTime * transitionSpeed);
            _ghostInstance.transform.rotation = Quaternion.Slerp(
                _ghostInstance.transform.rotation, transform.rotation,
                Time.deltaTime * transitionSpeed);
        }

        private void FadeOut()
        {
            IsProjecting = false;
            ProjectedTarget = null;
            _currentAlpha = Mathf.MoveTowards(_currentAlpha, 0f, Time.deltaTime * transitionSpeed);

            if (_ghostInstance != null && _currentAlpha <= 0.001f)
                _ghostInstance.SetActive(false);
        }

        private void UpdateGhostAlpha()
        {
            float targetAlpha = IsProjecting ? ghostAlpha : 0f;
            _currentAlpha = Mathf.MoveTowards(_currentAlpha, targetAlpha,
                Time.deltaTime * transitionSpeed * 0.3f);

            if (_ghostMaterials == null) return;

            foreach (var mat in _ghostMaterials)
            {
                if (mat == null) continue;
                Color c = mat.color;
                c.a = _currentAlpha;
                mat.color = c;
            }
        }

        private void EnsureGhostInstance()
        {
            if (_ghostInstance != null)
            {
                _ghostInstance.SetActive(true);
                return;
            }

            _ghostInstance = new GameObject("[Ghost] " + _hand.Side);
            _ghostInstance.transform.SetParent(null);

            var renderers = GetComponentsInChildren<MeshRenderer>();
            _ghostMaterials = new Material[renderers.Length];

            for (int i = 0; i < renderers.Length; i++)
            {
                var srcFilter = renderers[i].GetComponent<MeshFilter>();
                if (srcFilter == null || srcFilter.sharedMesh == null) continue;

                var ghostChild = new GameObject("GhostMesh_" + i);
                ghostChild.transform.SetParent(_ghostInstance.transform);
                ghostChild.transform.localPosition = renderers[i].transform.localPosition;
                ghostChild.transform.localRotation = renderers[i].transform.localRotation;
                ghostChild.transform.localScale = renderers[i].transform.localScale;

                ghostChild.AddComponent<MeshFilter>().sharedMesh = srcFilter.sharedMesh;

                Material mat;
                if (ghostMaterial != null)
                    mat = new Material(ghostMaterial);
                else
                {
                    mat = new Material(Shader.Find("Sprites/Default"));
                    mat.color = new Color(0.3f, 0.7f, 1f, 0f);
                }

                ghostChild.AddComponent<MeshRenderer>().sharedMaterial = mat;
                _ghostMaterials[i] = mat;
            }
        }

        private void OnDestroy()
        {
            if (_ghostInstance != null)
                Destroy(_ghostInstance);
        }

        public void SetEnabled(bool enabled) => enableProjection = enabled;
        public void SetAlpha(float alpha) => ghostAlpha = alpha;
        public void SetProjectionRadius(float radius) => projectionRadius = radius;
    }
}
