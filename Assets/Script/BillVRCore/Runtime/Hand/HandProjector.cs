using UnityEngine;
using BillVRCore.Input;
using BillVRCore.Interaction;

namespace BillVRCore.Hand
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

        [Header("Ghost Fingers")]
        [SerializeField] private bool animateGhostFingers = true;
        [SerializeField] private float defaultGrabCurl = 0.7f;

        public bool IsProjecting { get; private set; }
        public Grabbable ProjectedTarget { get; private set; }

        private VRHand _hand;
        private GrabHandler _grabHandler;
        private FingerRig _fingerRig;
        private GameObject _ghostRoot;
        private FingerRig _ghostFingerRig;
        private Material[] _ghostMaterials;
        private float _currentAlpha;
        private readonly Collider[] _overlapBuffer = new Collider[8];

        private void Awake()
        {
            _hand = GetComponent<VRHand>();
            _grabHandler = GetComponent<GrabHandler>();
        }

        private void Start()
        {
            var animator = _hand.Animator;
            _fingerRig = animator != null ? animator.GetFingerRig() : GetComponentInChildren<FingerRig>();
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

        private Grabbable FindBestTarget()
        {
            Vector3 palmPos = _hand.PalmTransform != null ? _hand.PalmTransform.position : transform.position;
            int count = Physics.OverlapSphereNonAlloc(palmPos, projectionRadius, _overlapBuffer, grabLayers);

            Grabbable closest = null;
            float closestDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var grabbable = _overlapBuffer[i].GetComponentInParent<Grabbable>();
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

        private void ProjectToTarget(Grabbable target)
        {
            ProjectedTarget = target;
            IsProjecting = true;
            EnsureGhostInstance();

            Vector3 targetPos;
            Quaternion targetRot;

            var snapGrabbable = target as SnapGrabbable;
            if (snapGrabbable != null)
            {
                Transform grip = snapGrabbable.GetGripPoint(_hand.Side);
                if (grip != null)
                {
                    targetPos = grip.position;
                    targetRot = grip.rotation;

                    if (animateGhostFingers && _ghostFingerRig != null)
                        ApplySnapPoseToGhost(snapGrabbable);

                    LerpGhostTransform(targetPos, targetRot);
                    return;
                }
            }

            Collider col = target.GetComponent<Collider>();
            targetPos = col != null
                ? col.ClosestPoint(_hand.PalmTransform != null ? _hand.PalmTransform.position : transform.position)
                : target.transform.position;
            targetRot = transform.rotation;

            if (animateGhostFingers && _ghostFingerRig != null)
                ApplyDefaultGrabPoseToGhost();

            LerpGhostTransform(targetPos, targetRot);
        }

        private void LerpGhostTransform(Vector3 pos, Quaternion rot)
        {
            float t = Time.deltaTime * transitionSpeed;
            _ghostRoot.transform.position = Vector3.Lerp(_ghostRoot.transform.position, pos, t);
            _ghostRoot.transform.rotation = Quaternion.Slerp(_ghostRoot.transform.rotation, rot, t);
        }

        private void ApplySnapPoseToGhost(SnapGrabbable snap)
        {
            if (_ghostFingerRig == null) return;

            for (int i = 0; i < 5; i++)
            {
                FingerType finger = (FingerType)i;
                _ghostFingerRig.SetImmediateCurl(finger, snap.GetFingerCurl(finger));
            }
        }

        private void ApplyDefaultGrabPoseToGhost()
        {
            if (_ghostFingerRig == null) return;

            for (int i = 0; i < 5; i++)
                _ghostFingerRig.SetImmediateCurl((FingerType)i, defaultGrabCurl);
        }

        private void FadeOut()
        {
            IsProjecting = false;
            ProjectedTarget = null;
            _currentAlpha = Mathf.MoveTowards(_currentAlpha, 0f, Time.deltaTime * transitionSpeed);

            if (_ghostRoot != null && _currentAlpha <= 0.001f)
                _ghostRoot.SetActive(false);
        }

        private void UpdateGhostAlpha()
        {
            float targetAlpha = IsProjecting ? ghostAlpha : 0f;
            _currentAlpha = Mathf.MoveTowards(_currentAlpha, targetAlpha, Time.deltaTime * transitionSpeed * 0.3f);

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
            if (_ghostRoot != null)
            {
                _ghostRoot.SetActive(true);
                return;
            }

            BuildGhostHand();
        }

        private void BuildGhostHand()
        {
            _ghostRoot = new GameObject($"[GhostHand] {_hand.Side}");
            _ghostRoot.transform.SetParent(null);

            var sourceRig = _fingerRig;
            bool hasRig = sourceRig != null && sourceRig.Fingers != null;

            if (hasRig)
            {
                var rigCopy = CloneHierarchy(transform, _ghostRoot.transform);
                _ghostFingerRig = rigCopy.GetComponentInChildren<FingerRig>();

                if (_ghostFingerRig != null)
                    _ghostFingerRig.Initialize();

                RemoveNonVisualComponents(rigCopy);
            }

            ApplyGhostMaterials(_ghostRoot);
            RemoveColliders(_ghostRoot);
        }

        private GameObject CloneHierarchy(Transform source, Transform parent)
        {
            var clone = new GameObject(source.name);
            clone.transform.SetParent(parent, false);
            clone.transform.localPosition = source.localPosition;
            clone.transform.localRotation = source.localRotation;
            clone.transform.localScale = source.localScale;

            var meshFilter = source.GetComponent<MeshFilter>();
            var meshRenderer = source.GetComponent<MeshRenderer>();

            if (meshFilter != null && meshRenderer != null)
            {
                clone.AddComponent<MeshFilter>().sharedMesh = meshFilter.sharedMesh;
                clone.AddComponent<MeshRenderer>();
            }

            var sourceFingerRig = source.GetComponent<FingerRig>();
            if (sourceFingerRig != null)
            {
                var cloneRig = clone.AddComponent<FingerRig>();
                CopyFingerRigReferences(sourceFingerRig, cloneRig, source, clone.transform);
            }

            for (int i = 0; i < source.childCount; i++)
                CloneHierarchy(source.GetChild(i), clone.transform);

            return clone;
        }

        private void CopyFingerRigReferences(FingerRig source, FingerRig dest, Transform sourceRoot, Transform destRoot)
        {
            var sourceFingers = source.Fingers;
            if (sourceFingers == null) return;

            var destFingers = new FingerRig.FingerChain[sourceFingers.Length];
            for (int i = 0; i < sourceFingers.Length; i++)
            {
                destFingers[i] = new FingerRig.FingerChain
                {
                    type = sourceFingers[i].type,
                    tipRadius = sourceFingers[i].tipRadius
                };

                if (sourceFingers[i].joints != null)
                {
                    destFingers[i].joints = new Transform[sourceFingers[i].joints.Length];
                    for (int j = 0; j < sourceFingers[i].joints.Length; j++)
                    {
                        if (sourceFingers[i].joints[j] == null) continue;
                        destFingers[i].joints[j] = FindCorrespondingTransform(sourceRoot, destRoot, sourceFingers[i].joints[j]);
                    }
                }

                if (sourceFingers[i].tip != null)
                    destFingers[i].tip = FindCorrespondingTransform(sourceRoot, destRoot, sourceFingers[i].tip);

                if (sourceFingers[i].openLocalRotations != null)
                {
                    destFingers[i].openLocalRotations = new Quaternion[sourceFingers[i].openLocalRotations.Length];
                    System.Array.Copy(sourceFingers[i].openLocalRotations, destFingers[i].openLocalRotations, sourceFingers[i].openLocalRotations.Length);
                }

                if (sourceFingers[i].closedLocalRotations != null)
                {
                    destFingers[i].closedLocalRotations = new Quaternion[sourceFingers[i].closedLocalRotations.Length];
                    System.Array.Copy(sourceFingers[i].closedLocalRotations, destFingers[i].closedLocalRotations, sourceFingers[i].closedLocalRotations.Length);
                }
            }

            dest.SetFingerChains(destFingers);
        }

        private Transform FindCorrespondingTransform(Transform sourceRoot, Transform destRoot, Transform sourceTarget)
        {
            string path = GetRelativePath(sourceRoot, sourceTarget);
            if (string.IsNullOrEmpty(path)) return destRoot;
            return destRoot.Find(path);
        }

        private string GetRelativePath(Transform root, Transform target)
        {
            if (target == root) return "";

            var parts = new System.Collections.Generic.List<string>();
            Transform current = target;

            while (current != null && current != root)
            {
                parts.Add(current.name);
                current = current.parent;
            }

            if (current != root) return null;

            parts.Reverse();
            return string.Join("/", parts);
        }

        private void ApplyGhostMaterials(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>();
            _ghostMaterials = new Material[renderers.Length];

            for (int i = 0; i < renderers.Length; i++)
            {
                Material mat;
                if (ghostMaterial != null)
                    mat = new Material(ghostMaterial);
                else
                {
                    mat = new Material(Shader.Find("Sprites/Default"));
                    mat.color = new Color(0.3f, 0.7f, 1f, 0f);
                }

                renderers[i].sharedMaterial = mat;
                _ghostMaterials[i] = mat;
            }
        }

        private void RemoveColliders(GameObject root)
        {
            foreach (var col in root.GetComponentsInChildren<Collider>())
                Destroy(col);
        }

        private void RemoveNonVisualComponents(GameObject root)
        {
            foreach (var rb in root.GetComponentsInChildren<Rigidbody>())
                Destroy(rb);

            foreach (var comp in root.GetComponentsInChildren<MonoBehaviour>())
            {
                if (comp is FingerRig) continue;
                Destroy(comp);
            }
        }

        private void OnDestroy()
        {
            if (_ghostRoot != null)
                Destroy(_ghostRoot);
        }

        public void SetEnabled(bool enabled) => enableProjection = enabled;
        public void SetAlpha(float alpha) => ghostAlpha = alpha;
        public void SetProjectionRadius(float radius) => projectionRadius = radius;
        public void SetAnimateGhostFingers(bool animate) => animateGhostFingers = animate;
    }
}
