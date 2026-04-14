using UnityEngine;
using BillInspector;

namespace RPGModular
{
    [CreateAssetMenu(menuName = "Game/Item Data")]
    [BillTitle("Item Data", "Base item definition")]
    public class ItemData : ScriptableObject
    {
        [BillBoxGroup("Identity")]
        public string itemID;
        [BillBoxGroup("Identity"), BillLabelText("Name Key (Loc)")]
        public string nameKey;
        [BillBoxGroup("Identity"), BillLabelText("Desc Key (Loc)")]
        public string descKey;
        [BillBoxGroup("Identity"), BillPreviewField]
        public Sprite icon;
        [BillBoxGroup("Identity"), BillEnumToggleButtons]
        public ItemType type;
        [BillBoxGroup("Identity"), BillEnumToggleButtons]
        public ItemRarity rarity;

        [BillBoxGroup("Stack")]
        [BillSlider(1, 99)]
        public int maxStack = 99;

        [BillBoxGroup("Economy")]
        public int sellPrice = 10;

        [BillBoxGroup("Equipment")]
        [BillInfoBox("Only relevant for equippable items")]
        public bool isEquippable;
        [BillShowIf("isEquippable")]
        public EquipSlot defaultSlot;
        [BillShowIf("isEquippable"), BillTableList]
        public StatBonus[] equipBonuses;
        [BillShowIf("isEquippable"), BillTableList]
        public StatRequirement[] requirements;

        [BillBoxGroup("Weapon")]
        [BillShowIf("type", ItemType.Weapon)]
        public WeaponType weaponType;
        [BillShowIf("type", ItemType.Weapon)]
        public float baseDamage;
        [BillShowIf("type", ItemType.Weapon)]
        [BillSlider(0.5f, 2f)]
        public float attackSpeedModifier = 1f;

        [BillBoxGroup("Consumable")]
        [BillShowIf("type", ItemType.Consumable)]
        public float healAmount;
        [BillShowIf("type", ItemType.Consumable)]
        public float manaAmount;
        [BillShowIf("type", ItemType.Consumable)]
        public float staminaAmount;
        [BillShowIf("type", ItemType.Consumable)]
        public float chiAmount;
        [BillShowIf("type", ItemType.Consumable)]
        public StatusEffectData appliedBuff;
    }
}
