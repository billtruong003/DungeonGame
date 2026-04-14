using UnityEngine.UIElements;
using BillGameCore;

namespace RPGModular.UI
{
    public class InventoryPanel : BasePanel
    {
        private VisualElement slotsGrid;
        private Label goldLabel;
        private Label titleLabel;

        protected override void Build(VisualElement root)
        {
            root.style.backgroundColor = new StyleColor(new UnityEngine.Color(0, 0, 0, 0.7f));
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;

            var panel = new VisualElement();
            panel.style.width = 500; panel.style.height = 600;
            panel.style.backgroundColor = new StyleColor(new UnityEngine.Color(0.12f, 0.12f, 0.15f, 0.95f));
            panel.style.borderBottomLeftRadius = panel.style.borderBottomRightRadius =
                panel.style.borderTopLeftRadius = panel.style.borderTopRightRadius = 12;
            panel.style.paddingTop = panel.style.paddingBottom = 16;
            panel.style.paddingLeft = panel.style.paddingRight = 20;
            root.Add(panel);

            // Header
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.marginBottom = 12;
            panel.Add(header);

            titleLabel = new Label(Loc.Get("ui.hud.gold"));
            titleLabel.style.fontSize = 22;
            titleLabel.style.color = new StyleColor(UnityEngine.Color.white);
            header.Add(titleLabel);

            goldLabel = new Label("0 G");
            goldLabel.style.fontSize = 18;
            goldLabel.style.color = new StyleColor(new UnityEngine.Color(1f, 0.85f, 0.2f));
            header.Add(goldLabel);

            var closeBtn = new Button(() => Bill.UI.Close<InventoryPanel>());
            closeBtn.text = "X";
            closeBtn.style.width = 30; closeBtn.style.height = 30;
            header.Add(closeBtn);

            // Slot grid
            slotsGrid = new VisualElement();
            slotsGrid.style.flexDirection = FlexDirection.Row;
            slotsGrid.style.flexWrap = Wrap.Wrap;
            panel.Add(slotsGrid);

            int maxSlots = Game.Inv?.MaxSlots ?? 30;
            for (int i = 0; i < maxSlots; i++)
            {
                var slot = CreateSlot(i);
                slotsGrid.Add(slot);
            }
        }

        public override void OnOpened()
        {
            RefreshAll();
            if (Game.Inv != null)
            {
                Game.Inv.OnSlotChanged += OnSlotChanged;
                Game.Inv.OnGoldChanged += OnGoldChanged;
            }
        }

        public override void OnClosed()
        {
            if (Game.Inv != null)
            {
                Game.Inv.OnSlotChanged -= OnSlotChanged;
                Game.Inv.OnGoldChanged -= OnGoldChanged;
            }
        }

        private VisualElement CreateSlot(int index)
        {
            var slot = new VisualElement();
            slot.style.width = 52; slot.style.height = 52;
            slot.style.marginRight = 4; slot.style.marginBottom = 4;
            slot.style.backgroundColor = new StyleColor(new UnityEngine.Color(0.2f, 0.2f, 0.25f));
            slot.style.borderBottomLeftRadius = slot.style.borderBottomRightRadius =
                slot.style.borderTopLeftRadius = slot.style.borderTopRightRadius = 6;
            slot.style.borderBottomWidth = slot.style.borderTopWidth =
                slot.style.borderLeftWidth = slot.style.borderRightWidth = 1;
            slot.style.borderBottomColor = slot.style.borderTopColor =
                slot.style.borderLeftColor = slot.style.borderRightColor =
                new StyleColor(new UnityEngine.Color(0.3f, 0.3f, 0.35f));
            slot.style.alignItems = Align.Center;
            slot.style.justifyContent = Justify.Center;

            var qtyLabel = new Label("");
            qtyLabel.style.fontSize = 10;
            qtyLabel.style.color = new StyleColor(UnityEngine.Color.white);
            qtyLabel.style.position = Position.Absolute;
            qtyLabel.style.right = 2; qtyLabel.style.bottom = 1;
            qtyLabel.name = $"qty_{index}";
            slot.Add(qtyLabel);

            slot.name = $"slot_{index}";

            // Right-click to use consumable
            int idx = index;
            slot.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.button == 1) // right-click
                    Game.Inv?.UseItem(idx);
            });

            return slot;
        }

        private void RefreshAll()
        {
            goldLabel.text = $"{Game.Inv?.Gold ?? 0} G";
            int maxSlots = Game.Inv?.MaxSlots ?? 30;
            for (int i = 0; i < maxSlots; i++)
                RefreshSlot(i);
        }

        private void RefreshSlot(int index)
        {
            var stack = Game.Inv?.GetSlot(index) ?? ItemStack.Empty;
            var slot = slotsGrid.Q($"slot_{index}");
            var qty = slotsGrid.Q<Label>($"qty_{index}");
            if (slot == null) return;

            if (stack.IsEmpty)
            {
                slot.style.backgroundColor = new StyleColor(new UnityEngine.Color(0.2f, 0.2f, 0.25f));
                if (qty != null) qty.text = "";
            }
            else
            {
                // Color by rarity
                var color = stack.Data.rarity switch
                {
                    ItemRarity.Common => new UnityEngine.Color(0.25f, 0.25f, 0.3f),
                    ItemRarity.Uncommon => new UnityEngine.Color(0.15f, 0.3f, 0.15f),
                    ItemRarity.Rare => new UnityEngine.Color(0.15f, 0.2f, 0.4f),
                    ItemRarity.Epic => new UnityEngine.Color(0.3f, 0.15f, 0.4f),
                    ItemRarity.Legendary => new UnityEngine.Color(0.4f, 0.35f, 0.1f),
                    _ => new UnityEngine.Color(0.25f, 0.25f, 0.3f)
                };
                slot.style.backgroundColor = new StyleColor(color);
                if (qty != null) qty.text = stack.Quantity > 1 ? stack.Quantity.ToString() : "";
            }
        }

        private void OnSlotChanged(int index, ItemStack stack) => RefreshSlot(index);
        private void OnGoldChanged(int gold) => goldLabel.text = $"{gold} G";
    }
}
