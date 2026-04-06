using UnityEngine;

namespace BillDev.Core.Rendering
{
    [RequireComponent(typeof(MeshFilter))]
    public sealed class BypassFrustumCulling : MonoBehaviour
    {
        private void Awake()
        {
            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.mesh != null)
            {
                meshFilter.mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000000f);
            }
        }
    }
}