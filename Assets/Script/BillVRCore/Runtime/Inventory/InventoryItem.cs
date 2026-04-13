using UnityEngine;
using BillVRCore.Interaction;

namespace BillVRCore.Inventory
{
    [RequireComponent(typeof(Grabbable))]
    public class InventoryItem : MonoBehaviour
    {
        [SerializeField] private ItemData itemData;
        [SerializeField] private int quantity = 1;

        public ItemData Data => itemData;
        public int Quantity { get => quantity; set => quantity = Mathf.Max(0, value); }
        public Grabbable Grabbable { get; private set; }

        private void Awake()
        {
            Grabbable = GetComponent<Grabbable>();
        }

        public bool FitsSlot(SlotType slot)
        {
            if (itemData == null) return true;
            return itemData.FitsSlot(slot);
        }
    }
}
