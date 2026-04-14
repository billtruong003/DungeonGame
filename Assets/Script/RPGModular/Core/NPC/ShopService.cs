using System;
using UnityEngine;

namespace RPGModular
{
    public enum BuyResult { Success, NoGold, NoSpace, OutOfStock }

    public class ShopService : MonoBehaviour
    {
        public static ShopService Instance { get; private set; }

        public ShopData CurrentShop { get; private set; }
        public bool IsOpen => CurrentShop != null;

        public event Action<ItemData, int, int> OnItemBought;
        public event Action<ItemData, int, int> OnItemSold;
        public event Action<ShopData> OnShopOpened;
        public event Action OnShopClosed;

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void OpenShop(ShopData shop)
        {
            CurrentShop = shop;
            OnShopOpened?.Invoke(shop);
        }

        public void CloseShop()
        {
            CurrentShop = null;
            OnShopClosed?.Invoke();
        }

        public BuyResult Buy(int itemIndex, int quantity = 1)
        {
            if (CurrentShop == null || CurrentShop.items == null) return BuyResult.OutOfStock;
            if (itemIndex < 0 || itemIndex >= CurrentShop.items.Length) return BuyResult.OutOfStock;

            var shopItem = CurrentShop.items[itemIndex];
            if (shopItem.item == null) return BuyResult.OutOfStock;
            if (shopItem.stock >= 0 && shopItem.stock < quantity) return BuyResult.OutOfStock;

            int price = shopItem.price > 0 ? shopItem.price
                : Mathf.RoundToInt(shopItem.item.sellPrice * CurrentShop.buyPriceMultiplier);
            int totalCost = price * quantity;

            if (Game.Inv == null || !Game.Inv.SpendGold(totalCost))
                return BuyResult.NoGold;

            int overflow = Game.Inv.AddItem(shopItem.item, quantity);
            if (overflow > 0)
            {
                Game.Inv.AddGold(overflow * price); // refund overflow
                quantity -= overflow;
            }

            if (quantity <= 0) return BuyResult.NoSpace;

            if (shopItem.stock >= 0) shopItem.stock -= quantity;
            OnItemBought?.Invoke(shopItem.item, quantity, totalCost);
            return BuyResult.Success;
        }

        public int Sell(ItemData item, int quantity = 1)
        {
            if (item == null || Game.Inv == null) return 0;

            int removed = Game.Inv.RemoveItem(item, quantity);
            if (removed <= 0) return 0;

            float mult = CurrentShop != null ? CurrentShop.sellPriceMultiplier : 0.3f;
            int goldPerItem = Mathf.RoundToInt(item.sellPrice * mult);
            int totalGold = goldPerItem * removed;
            Game.Inv.AddGold(totalGold);

            OnItemSold?.Invoke(item, removed, totalGold);
            return totalGold;
        }

        public int GetSellPrice(ItemData item)
        {
            if (item == null) return 0;
            float mult = CurrentShop != null ? CurrentShop.sellPriceMultiplier : 0.3f;
            return Mathf.RoundToInt(item.sellPrice * mult);
        }
    }
}
