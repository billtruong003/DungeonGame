using System;
using UnityEngine;
using BillInspector;

namespace RPGModular
{
    [CreateAssetMenu(menuName = "Game/Recipe Data")]
    [BillTitle("Recipe", "Crafting recipe")]
    public class RecipeData : ScriptableObject
    {
        [BillBoxGroup("Identity")]
        public string recipeID;
        [BillBoxGroup("Identity"), BillLabelText("Name Key (Loc)")]
        public string nameKey;
        [BillBoxGroup("Identity"), BillPreviewField]
        public Sprite icon;
        [BillBoxGroup("Identity"), BillEnumToggleButtons]
        public CraftType craftType;

        [BillBoxGroup("Requirements")]
        public SkillTreeType requiredTree;
        public int requiredSkillLevel;
        [BillTableList]
        public CraftIngredient[] ingredients;
        public int goldCost;

        [BillBoxGroup("Output")]
        [BillRequired]
        public ItemData outputItem;
        public int outputQuantity = 1;

        [BillBoxGroup("Success Rate")]
        [BillSlider(0f, 1f)]
        public float baseSuccessRate = 1f;
    }

    [Serializable]
    public class CraftIngredient
    {
        public ItemData item;
        public int quantity;
    }
}
