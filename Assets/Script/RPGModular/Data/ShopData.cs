using System;
using UnityEngine;
using BillInspector;

namespace RPGModular
{
    [CreateAssetMenu(menuName = "Game/Shop Data")]
    [BillTitle("Shop Data", "Merchant inventory")]
    public class ShopData : ScriptableObject
    {
        [BillBoxGroup("Config")]
        [BillLabelText("Name Key (Loc)")]
        public string nameKey;
        [BillSlider(0.5f, 3f)]
        public float buyPriceMultiplier = 1f;
        [BillSlider(0.1f, 1f)]
        public float sellPriceMultiplier = 0.3f;

        [BillBoxGroup("Inventory")]
        [BillTableList]
        public ShopItem[] items;
    }

    [Serializable]
    public class ShopItem
    {
        public ItemData item;
        public int price;
        public int stock = -1; // -1 = infinite
    }
}
