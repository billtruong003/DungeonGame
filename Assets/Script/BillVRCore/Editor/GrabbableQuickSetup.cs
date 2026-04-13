#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using BillVRCore.Interaction;
using BillVRCore.Interaction.Gadgets;

namespace BillVRCore.Editor
{
    public static class GrabbableQuickSetup
    {
        [MenuItem("GameObject/VRCore/Make Grabbable", false, 10)]
        private static void MakeGrabbableFromMenu()
        {
            foreach (var go in Selection.gameObjects)
                SetupGrabbable(go);
        }

        [MenuItem("GameObject/VRCore/Make Grabbable", true)]
        private static bool MakeGrabbableValidation() => Selection.activeGameObject != null;

        [MenuItem("GameObject/VRCore/Make Two-Hand Grabbable", false, 11)]
        private static void MakeTwoHandGrabbable()
        {
            foreach (var go in Selection.gameObjects)
                SetupTwoHand(go);
        }

        [MenuItem("GameObject/VRCore/Make Snap Grabbable", false, 12)]
        private static void MakeSnapGrabbable()
        {
            foreach (var go in Selection.gameObjects)
                SetupSnap(go);
        }

        [MenuItem("GameObject/VRCore/Add PlacePoint", false, 20)]
        private static void AddPlacePoint()
        {
            foreach (var go in Selection.gameObjects)
            {
                if (go.GetComponent<PlacePoint>() == null)
                {
                    Undo.AddComponent<PlacePoint>(go);
                    SetLayer(go, "InventorySlot");
                }
            }
        }

        [MenuItem("GameObject/VRCore/Add Physics Button", false, 30)]
        private static void AddPhysicsButton()
        {
            foreach (var go in Selection.gameObjects)
            {
                EnsureRigidbody(go);
                if (go.GetComponent<PhysicsButton>() == null)
                    Undo.AddComponent<PhysicsButton>(go);
            }
        }

        [MenuItem("GameObject/VRCore/Add Climbable", false, 31)]
        private static void AddClimbable()
        {
            foreach (var go in Selection.gameObjects)
            {
                SetupGrabbable(go);
                if (go.GetComponent<Climbable>() == null)
                    Undo.AddComponent<Climbable>(go);
            }
        }

        private static void SetupGrabbable(GameObject go)
        {
            EnsureRigidbody(go);
            EnsureCollider(go);

            if (go.GetComponent<Grabbable>() == null)
                Undo.AddComponent<Grabbable>(go);

            SetLayer(go, "Grabbable");
        }

        private static void SetupTwoHand(GameObject go)
        {
            EnsureRigidbody(go);
            EnsureCollider(go);

            if (go.GetComponent<TwoHandGrabbable>() == null)
                Undo.AddComponent<TwoHandGrabbable>(go);

            SetLayer(go, "Grabbable");
        }

        private static void SetupSnap(GameObject go)
        {
            EnsureRigidbody(go);
            EnsureCollider(go);

            if (go.GetComponent<SnapGrabbable>() == null)
                Undo.AddComponent<SnapGrabbable>(go);

            SetLayer(go, "Grabbable");
        }

        private static void EnsureRigidbody(GameObject go)
        {
            if (go.GetComponent<Rigidbody>() == null)
            {
                var rb = Undo.AddComponent<Rigidbody>(go);
                rb.mass = 0.5f;
                rb.linearDamping = 0.5f;
            }
        }

        private static void EnsureCollider(GameObject go)
        {
            if (go.GetComponent<Collider>() == null)
            {
                if (go.GetComponent<MeshFilter>() != null)
                    Undo.AddComponent<MeshCollider>(go).convex = true;
                else
                    Undo.AddComponent<BoxCollider>(go);
            }
        }

        private static void SetLayer(GameObject go, string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer != -1)
                go.layer = layer;
        }
    }
}
#endif
