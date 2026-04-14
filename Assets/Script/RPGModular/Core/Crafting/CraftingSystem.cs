using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGModular
{
    public struct CraftResult
    {
        public bool Success;
        public ItemData OutputItem;
        public int OutputQuantity;
    }

    public class CraftingSystem : MonoBehaviour
    {
        public static CraftingSystem Instance { get; private set; }

        public event Action<RecipeData, ItemData> OnCraftSuccess;
        public event Action<RecipeData> OnCraftFail;

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public bool CanCraft(RecipeData recipe)
        {
            if (recipe == null || Game.Inv == null) return false;

            // Check skill level
            if (Game.SkillBook != null)
            {
                // Simplified: check if player has the required tree at required level
                // Full implementation would look up specific skill in that tree
            }

            // Check materials
            if (recipe.ingredients != null)
            {
                foreach (var ing in recipe.ingredients)
                {
                    if (!Game.Inv.HasItem(ing.item, ing.quantity))
                        return false;
                }
            }

            // Check gold
            if (recipe.goldCost > 0 && Game.Inv.Gold < recipe.goldCost)
                return false;

            return true;
        }

        public CraftResult Craft(RecipeData recipe)
        {
            if (!CanCraft(recipe))
                return new CraftResult { Success = false };

            // Consume materials
            if (recipe.ingredients != null)
            {
                foreach (var ing in recipe.ingredients)
                    Game.Inv.RemoveItem(ing.item, ing.quantity);
            }

            // Consume gold
            if (recipe.goldCost > 0)
                Game.Inv.SpendGold(recipe.goldCost);

            // Roll success
            float successRate = recipe.baseSuccessRate;
            // Bonus from skill level: +5% per level above requirement
            // Simplified for now
            bool success = UnityEngine.Random.value <= successRate;

            if (success)
            {
                Game.Inv.AddItem(recipe.outputItem, recipe.outputQuantity);
                OnCraftSuccess?.Invoke(recipe, recipe.outputItem);
                return new CraftResult
                {
                    Success = true,
                    OutputItem = recipe.outputItem,
                    OutputQuantity = recipe.outputQuantity
                };
            }
            else
            {
                OnCraftFail?.Invoke(recipe);
                return new CraftResult { Success = false };
            }
        }
    }
}
