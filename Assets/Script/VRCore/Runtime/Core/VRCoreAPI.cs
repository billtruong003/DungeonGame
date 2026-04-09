using UnityEngine;
using VRCore.Hand;
using VRCore.Input;
using VRCore.Interaction;
using VRCore.Locomotion;
using VRCore.Inventory;
using VRCore.Weapons;

namespace VRCore
{
    public static class VRCoreAPI
    {
        public static IVRInput Input => InputManager.Instance?.Input;

        public static VRHand GetHand(HandSide side)
        {
            var hands = Object.FindObjectsByType<VRHand>(FindObjectsSortMode.None);
            foreach (var hand in hands)
            {
                if (hand.Side == side) return hand;
            }
            return null;
        }

        public static bool GrabPressed(HandSide side) =>
            Input?.GrabPressed(side) ?? false;

        public static bool GrabHeld(HandSide side) =>
            Input?.GrabHeld(side) ?? false;

        public static bool TriggerPressed(HandSide side) =>
            Input?.TriggerPressed(side) ?? false;

        public static float Grip(HandSide side) =>
            Input?.GripStrength(side) ?? 0f;

        public static float Trigger(HandSide side) =>
            Input?.TriggerStrength(side) ?? 0f;

        public static Vector2 Joystick(HandSide side) =>
            Input?.JoystickAxis(side) ?? Vector2.zero;

        public static float Finger(HandSide side, FingerType finger) =>
            InputManager.Instance?.GetFingerCurl(side, finger) ?? 0f;

        public static void Grab(VRHand hand, Grabbable target)
        {
            if (hand == null || target == null) return;
            hand.GrabHandler.TryGrab(target);
        }

        public static void Release(VRHand hand)
        {
            hand?.GrabHandler?.ForceRelease();
        }

        public static void Release(VRHand hand, bool applyThrow)
        {
            if (hand?.GrabHandler == null) return;
            if (applyThrow)
                hand.GrabHandler.ReleaseWithThrow();
            else
                hand.GrabHandler.ForceRelease();
        }

        public static Grabbable GetHeldObject(HandSide side)
        {
            var hand = GetHand(side);
            return hand?.GrabHandler?.HeldObject;
        }

        public static bool IsHolding(HandSide side) =>
            GetHand(side)?.GrabHandler?.IsHolding ?? false;

        public static void Haptic(HandSide side, float amplitude = 0.3f, float duration = 0.05f)
        {
            GetHand(side)?.Haptics?.PlayHaptic(amplitude, duration);
        }

        public static void Haptic(VRHand hand, float amplitude = 0.3f, float duration = 0.05f)
        {
            hand?.Haptics?.PlayHaptic(amplitude, duration);
        }

        public static void TeleportPlayer(Vector3 position)
        {
            var body = Object.FindFirstObjectByType<VRPlayerBody>();
            body?.Teleport(position);
        }

        public static void TeleportPlayer(Vector3 position, float yRotation)
        {
            var body = Object.FindFirstObjectByType<VRPlayerBody>();
            if (body == null) return;
            body.Teleport(position);
            body.Rotate(yRotation - body.transform.eulerAngles.y);
        }

        public static void TeleportPlayer(Transform target)
        {
            if (target == null) return;
            TeleportPlayer(target.position, target.eulerAngles.y);
        }

        public static void MovePlayer(Vector3 direction, float speed)
        {
            var body = Object.FindFirstObjectByType<VRPlayerBody>();
            if (body == null) return;
            body.Rb.linearVelocity = direction.normalized * speed;
        }

        public static void RotatePlayer(float angle)
        {
            var body = Object.FindFirstObjectByType<VRPlayerBody>();
            body?.Rotate(angle);
        }

        public static LocomotionState GetLocomotionState() =>
            LocomotionStateMachine.Instance?.CurrentState ?? LocomotionState.Idle;

        public static bool IsPlayerGrounded()
        {
            var body = Object.FindFirstObjectByType<VRPlayerBody>();
            return body?.IsGrounded ?? false;
        }

        public static void MakeGrabbable(GameObject target)
        {
            if (target.GetComponent<Rigidbody>() == null)
                target.AddComponent<Rigidbody>();
            if (target.GetComponent<Grabbable>() == null)
                target.AddComponent<Grabbable>();

            int layer = LayerMask.NameToLayer("Grabbable");
            if (layer >= 0) target.layer = layer;
        }

        public static void MakeGrabbable(GameObject target, float mass, bool parentOnGrab = true)
        {
            MakeGrabbable(target);
            target.GetComponent<Rigidbody>().mass = mass;
        }

        public static PlacePoint CreatePlacePoint(Vector3 position, SlotType slotType = SlotType.Any)
        {
            var go = new GameObject($"PlacePoint_{slotType}");
            go.transform.position = position;

            var collider = go.AddComponent<SphereCollider>();
            collider.isTrigger = true;

            var placePoint = go.AddComponent<PlacePoint>();

            int layer = LayerMask.NameToLayer("InventorySlot");
            if (layer >= 0) go.layer = layer;

            return placePoint;
        }

        public static bool StoreItem(Grabbable item)
        {
            if (InventoryManager.Instance == null) return false;
            var inventoryItem = item.GetComponent<InventoryItem>();
            if (inventoryItem == null) return false;
            return InventoryManager.Instance.StoreItem(inventoryItem);
        }

        public static Grabbable RetrieveItem(SlotType slot)
        {
            return InventoryManager.Instance?.RetrieveFromSlot(slot);
        }

        public static void DealDamage(GameObject target, float amount, Vector3 direction,
            float force = 10f, DamageType type = DamageType.Melee)
        {
            var damageable = target.GetComponentInParent<IDamageable>();
            if (damageable == null) return;

            damageable.TakeDamage(DamageEvent.Create(
                amount, type, target.transform.position, direction, force));
        }

        public static void DealDamage(RaycastHit hit, float amount, float force,
            DamageType type = DamageType.Ranged, GameObject source = null)
        {
            var damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable == null) return;

            var dmg = DamageEvent.Create(
                amount, type, hit.point, -hit.normal, force, source);

            var zone = HitZone.FindOnCollider(hit.collider);
            if (zone != null) dmg = zone.ApplyZone(dmg);

            dmg.hitCollider = hit.collider;
            damageable.TakeDamage(dmg);
        }

        public static void SwitchInputMode(InputMode mode)
        {
            InputManager.Instance?.SwitchMode(mode);
        }

        public static Pose GetHeadPose() =>
            Input?.GetHeadPose() ?? Pose.identity;

        public static Pose GetHandPose(HandSide side) =>
            Input?.GetControllerPose(side) ?? Pose.identity;
    }
}
