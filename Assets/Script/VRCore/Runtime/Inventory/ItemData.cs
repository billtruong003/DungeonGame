using UnityEngine;

namespace VRCore.Inventory
{
    [CreateAssetMenu(fileName = "ItemData", menuName = "VRCore/Item Data")]
    public class ItemData : ScriptableObject
    {
        public string itemName;
        public Sprite icon;
        public ItemCategory category;
        public SlotType[] compatibleSlots;
        public bool stackable;
        public int maxStack = 1;
        public float weight = 1f;

        public bool FitsSlot(SlotType slot)
        {
            if (compatibleSlots == null || compatibleSlots.Length == 0) return true;

            foreach (var s in compatibleSlots)
            {
                if (s == slot) return true;
            }
            return false;
        }
    }

    public enum ItemCategory { Weapon, Ammo, Consumable, Tool, Key, Generic }
    public enum SlotType { Any, HipLeft, HipRight, Back, Chest, Wrist, Belt, Custom }
}
