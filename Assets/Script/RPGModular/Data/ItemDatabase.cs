using System.Collections.Generic;
using UnityEngine;
using BillInspector;

namespace RPGModular
{
    /// <summary>
    /// Registry of all items. ID-based lookup for Save/Load and SpacetimeDB.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Item Database")]
    [BillTitle("Item Database", "Registry of all game items")]
    public class ItemDatabase : ScriptableObject
    {
        [BillTableList]
        public ItemData[] allItems;
        [BillTableList]
        public WeaponData[] allWeapons;

        private Dictionary<string, ItemData> _itemLookup;
        private Dictionary<string, WeaponData> _weaponLookup;

        public ItemData GetItemByID(string itemID)
        {
            if (_itemLookup == null) BuildLookups();
            return _itemLookup.TryGetValue(itemID, out var item) ? item : null;
        }

        public WeaponData GetWeaponByID(string weaponID)
        {
            if (_weaponLookup == null) BuildLookups();
            return _weaponLookup.TryGetValue(weaponID, out var weapon) ? weapon : null;
        }

        private void BuildLookups()
        {
            _itemLookup = new Dictionary<string, ItemData>();
            if (allItems != null)
                foreach (var item in allItems)
                    if (item != null && !string.IsNullOrEmpty(item.itemID))
                        _itemLookup[item.itemID] = item;

            _weaponLookup = new Dictionary<string, WeaponData>();
            if (allWeapons != null)
                foreach (var w in allWeapons)
                    if (w != null && !string.IsNullOrEmpty(w.name))
                        _weaponLookup[w.name] = w;
        }

        private void OnEnable() => _itemLookup = null; // rebuild on load
    }
}
