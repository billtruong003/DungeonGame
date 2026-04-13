using UnityEngine;

namespace BillVRCore
{
    public static class Extensions
    {
        public static Vector3 Flatten(this Vector3 v)
        {
            return new Vector3(v.x, 0f, v.z);
        }

        public static Vector3 FlattenNormalized(this Vector3 v)
        {
            var flat = new Vector3(v.x, 0f, v.z);
            return flat.sqrMagnitude > 0.001f ? flat.normalized : Vector3.forward;
        }

        public static float FlatDistance(this Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public static Vector3 ClampMagnitude(this Vector3 v, float min, float max)
        {
            float mag = v.magnitude;
            if (mag < 0.0001f) return Vector3.zero;
            if (mag < min) return v.normalized * min;
            if (mag > max) return v.normalized * max;
            return v;
        }

        public static Quaternion ShortestRotation(this Quaternion from, Quaternion to)
        {
            if (Quaternion.Dot(from, to) < 0f)
                return to * Quaternion.Inverse(new Quaternion(-from.x, -from.y, -from.z, -from.w));
            return to * Quaternion.Inverse(from);
        }

        public static bool IsInLayerMask(this GameObject go, LayerMask mask)
        {
            return (mask & (1 << go.layer)) != 0;
        }

        public static T GetOrAddComponent<T>(this GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        public static float Remap(float value, float fromMin, float fromMax, float toMin, float toMax)
        {
            float t = Mathf.InverseLerp(fromMin, fromMax, value);
            return Mathf.Lerp(toMin, toMax, t);
        }

        public static float SmoothDamp01(float current, float target, ref float velocity, float smoothTime)
        {
            return Mathf.SmoothDamp(current, target, ref velocity, smoothTime);
        }

        public static Vector3 SafeNormalize(this Vector3 v, Vector3 fallback = default)
        {
            return v.sqrMagnitude > 0.0001f ? v.normalized : fallback;
        }

        public static bool ApproximatelyEqual(this float a, float b, float tolerance = 0.001f)
        {
            return Mathf.Abs(a - b) < tolerance;
        }

        public static void SetLayerRecursive(this GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                child.gameObject.SetLayerRecursive(layer);
        }
    }
}
