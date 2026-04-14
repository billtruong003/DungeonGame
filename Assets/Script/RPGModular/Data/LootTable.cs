using System.Collections.Generic;
using UnityEngine;
using BillInspector;

namespace RPGModular
{
    [CreateAssetMenu(menuName = "Game/Loot Table")]
    [BillTitle("Loot Table", "Drop rate table")]
    public class LootTable : ScriptableObject
    {
        [BillTableList]
        public LootEntry[] entries;

        [BillBoxGroup("Guaranteed")]
        public ItemData guaranteedDrop;
        public int guaranteedMinQty = 1;
        public int guaranteedMaxQty = 1;

        public List<(ItemData item, int qty)> Roll()
        {
            var result = new List<(ItemData, int)>();

            if (guaranteedDrop != null)
                result.Add((guaranteedDrop, Random.Range(guaranteedMinQty, guaranteedMaxQty + 1)));

            if (entries != null)
            {
                foreach (var e in entries)
                {
                    if (e.item != null && Random.value <= e.dropChance)
                        result.Add((e.item, Random.Range(e.minQuantity, e.maxQuantity + 1)));
                }
            }

            return result;
        }
    }
}
