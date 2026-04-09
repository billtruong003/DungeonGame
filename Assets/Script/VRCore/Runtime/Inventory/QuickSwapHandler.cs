using UnityEngine;
using VRCore.Hand;
using VRCore.Input;

namespace VRCore.Inventory
{
    public class QuickSwapHandler : MonoBehaviour
    {
        [Header("Bindings")]
        [SerializeField] private SlotType primaryButtonSlot = SlotType.HipRight;
        [SerializeField] private SlotType secondaryButtonSlot = SlotType.Back;

        [Header("Settings")]
        [SerializeField] private float swapCooldown = 0.3f;

        private float _lastSwapTime;

        private void Update()
        {
            if (InputManager.Instance == null || InventoryManager.Instance == null) return;
            if (Time.time - _lastSwapTime < swapCooldown) return;

            var input = InputManager.Instance.Input;

            TryQuickSwap(input, HandSide.Left);
            TryQuickSwap(input, HandSide.Right);
        }

        private void TryQuickSwap(IVRInput input, HandSide side)
        {
            if (input.PrimaryButtonDown(side))
                PerformSwap(side, primaryButtonSlot);
            else if (input.SecondaryButtonDown(side))
                PerformSwap(side, secondaryButtonSlot);
        }

        private void PerformSwap(HandSide side, SlotType slotType)
        {
            var hands = FindObjectsByType<VRHand>(FindObjectsSortMode.None);
            VRHand targetHand = null;

            foreach (var hand in hands)
            {
                if (hand.Side == side)
                {
                    targetHand = hand;
                    break;
                }
            }

            if (targetHand == null) return;

            var slot = InventoryManager.Instance.FindSlotByType(slotType);
            if (slot == null) return;

            if (targetHand.GrabHandler.IsHolding)
            {
                var heldItem = targetHand.GrabHandler.HeldObject.GetComponent<InventoryItem>();
                if (heldItem != null && slot.CanAcceptItem(heldItem) && !slot.HasItem)
                {
                    targetHand.GrabHandler.ForceRelease();
                    slot.PlacePoint.Place(heldItem.Grabbable);
                    _lastSwapTime = Time.time;
                }
                return;
            }

            if (slot.HasItem)
            {
                var item = slot.StoredItem;
                slot.PlacePoint.Remove();
                _lastSwapTime = Time.time;
            }
        }
    }
}
