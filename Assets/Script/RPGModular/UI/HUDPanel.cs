using UnityEngine.UIElements;
using BillGameCore;

namespace RPGModular.UI
{
    /// <summary>
    /// HUD chính: HP/MP/Chi/Stamina, EXP bar, skill bar, combo, target info, gold.
    /// Luôn hiển thị khi gameplay.
    /// </summary>
    public class HUDPanel : BasePanel
    {
        // Resource bars
        private VisualElement hpBar, mpBar, chiBar, staminaBar, expBar;
        private Label hpLabel, mpLabel, chiLabel, staminaLabel;
        private Label levelLabel, goldLabel, expLabel;

        // Skill bar
        private VisualElement[] skillSlots = new VisualElement[6];
        private Label[] skillCooldowns = new Label[6];

        // Target
        private VisualElement targetPanel;
        private VisualElement targetHPBar;
        private Label targetNameLabel;

        // Combo
        private Label comboLabel;

        protected override void Build(VisualElement root)
        {
            root.pickingMode = PickingMode.Ignore;

            BuildResourceBars(root);
            BuildExpBar(root);
            BuildSkillBar(root);
            BuildTargetInfo(root);
            BuildComboDisplay(root);
            BuildGoldDisplay(root);
        }

        public override void OnOpened()
        {
            SubscribeEvents();
            RefreshAll();
        }

        public override void OnClosed()
        {
            UnsubscribeEvents();
        }

        // ═══════════════════════════════════════════════════════
        // Build UI
        // ═══════════════════════════════════════════════════════

        private void BuildResourceBars(VisualElement root)
        {
            var container = new VisualElement();
            container.style.position = Position.Absolute;
            container.style.left = 20; container.style.bottom = 140;
            container.style.width = 280;
            root.Add(container);

            hpBar = CreateBar(container, "HP", new StyleColor(new UnityEngine.Color(0.8f, 0.2f, 0.2f)), out hpLabel);
            mpBar = CreateBar(container, "MP", new StyleColor(new UnityEngine.Color(0.2f, 0.4f, 0.9f)), out mpLabel);
            chiBar = CreateBar(container, "Chi", new StyleColor(new UnityEngine.Color(0.2f, 0.8f, 0.6f)), out chiLabel);
            staminaBar = CreateBar(container, "Sta", new StyleColor(new UnityEngine.Color(0.9f, 0.7f, 0.1f)), out staminaLabel);
        }

        private void BuildExpBar(VisualElement root)
        {
            var container = new VisualElement();
            container.style.position = Position.Absolute;
            container.style.left = 20; container.style.bottom = 110;
            container.style.width = 280; container.style.height = 16;
            root.Add(container);

            levelLabel = new Label("Lv.1");
            levelLabel.style.fontSize = 12;
            levelLabel.style.color = new StyleColor(UnityEngine.Color.white);
            container.Add(levelLabel);

            var barBg = new VisualElement();
            barBg.style.height = 8;
            barBg.style.backgroundColor = new StyleColor(new UnityEngine.Color(0.15f, 0.15f, 0.15f));
            barBg.style.borderBottomLeftRadius = barBg.style.borderBottomRightRadius =
                barBg.style.borderTopLeftRadius = barBg.style.borderTopRightRadius = 4;
            container.Add(barBg);

            expBar = new VisualElement();
            expBar.style.height = new StyleLength(StyleKeyword.Auto);
            expBar.style.backgroundColor = new StyleColor(new UnityEngine.Color(0.4f, 0.8f, 1f));
            expBar.style.borderBottomLeftRadius = expBar.style.borderBottomRightRadius =
                expBar.style.borderTopLeftRadius = expBar.style.borderTopRightRadius = 4;
            expBar.style.width = Length.Percent(0);
            expBar.style.height = 8;
            barBg.Add(expBar);

            expLabel = new Label("0/100");
            expLabel.style.fontSize = 10;
            expLabel.style.color = new StyleColor(new UnityEngine.Color(0.7f, 0.7f, 0.7f));
            container.Add(expLabel);
        }

        private void BuildSkillBar(VisualElement root)
        {
            var container = new VisualElement();
            container.style.position = Position.Absolute;
            container.style.bottom = 30;
            container.style.left = Length.Percent(50);
            container.style.translate = new Translate(Length.Percent(-50), 0);
            container.style.flexDirection = FlexDirection.Row;
            root.Add(container);

            string[] keys = { "Q", "W", "E", "R", "RMB", "Tap" };
            for (int i = 0; i < 6; i++)
            {
                var slot = new VisualElement();
                slot.style.width = 56; slot.style.height = 56;
                slot.style.marginRight = i < 5 ? 6 : 0;
                slot.style.backgroundColor = new StyleColor(new UnityEngine.Color(0.1f, 0.1f, 0.1f, 0.8f));
                slot.style.borderBottomWidth = slot.style.borderTopWidth =
                    slot.style.borderLeftWidth = slot.style.borderRightWidth = 1;
                slot.style.borderBottomColor = slot.style.borderTopColor =
                    slot.style.borderLeftColor = slot.style.borderRightColor =
                    new StyleColor(new UnityEngine.Color(0.4f, 0.4f, 0.4f));
                slot.style.borderBottomLeftRadius = slot.style.borderBottomRightRadius =
                    slot.style.borderTopLeftRadius = slot.style.borderTopRightRadius = 6;
                slot.style.alignItems = Align.Center;
                slot.style.justifyContent = Justify.Center;

                var keyLabel = new Label(keys[i]);
                keyLabel.style.fontSize = 10;
                keyLabel.style.color = new StyleColor(new UnityEngine.Color(0.6f, 0.6f, 0.6f));
                keyLabel.style.position = Position.Absolute;
                keyLabel.style.bottom = 2;
                slot.Add(keyLabel);

                var cdLabel = new Label("");
                cdLabel.style.fontSize = 14;
                cdLabel.style.color = new StyleColor(UnityEngine.Color.white);
                slot.Add(cdLabel);

                skillSlots[i] = slot;
                skillCooldowns[i] = cdLabel;
                container.Add(slot);
            }
        }

        private void BuildTargetInfo(VisualElement root)
        {
            targetPanel = new VisualElement();
            targetPanel.style.position = Position.Absolute;
            targetPanel.style.right = 20; targetPanel.style.top = Length.Percent(40);
            targetPanel.style.width = 200;
            targetPanel.style.display = DisplayStyle.None;
            root.Add(targetPanel);

            targetNameLabel = new Label("Enemy");
            targetNameLabel.style.fontSize = 14;
            targetNameLabel.style.color = new StyleColor(UnityEngine.Color.white);
            targetPanel.Add(targetNameLabel);

            var hpBg = new VisualElement();
            hpBg.style.height = 10;
            hpBg.style.backgroundColor = new StyleColor(new UnityEngine.Color(0.15f, 0.15f, 0.15f));
            hpBg.style.borderBottomLeftRadius = hpBg.style.borderBottomRightRadius =
                hpBg.style.borderTopLeftRadius = hpBg.style.borderTopRightRadius = 4;
            targetPanel.Add(hpBg);

            targetHPBar = new VisualElement();
            targetHPBar.style.height = 10;
            targetHPBar.style.width = Length.Percent(100);
            targetHPBar.style.backgroundColor = new StyleColor(new UnityEngine.Color(0.9f, 0.2f, 0.2f));
            targetHPBar.style.borderBottomLeftRadius = targetHPBar.style.borderBottomRightRadius =
                targetHPBar.style.borderTopLeftRadius = targetHPBar.style.borderTopRightRadius = 4;
            hpBg.Add(targetHPBar);
        }

        private void BuildComboDisplay(VisualElement root)
        {
            comboLabel = new Label("");
            comboLabel.style.position = Position.Absolute;
            comboLabel.style.left = Length.Percent(50);
            comboLabel.style.top = Length.Percent(35);
            comboLabel.style.translate = new Translate(Length.Percent(-50), 0);
            comboLabel.style.fontSize = 28;
            comboLabel.style.color = new StyleColor(new UnityEngine.Color(1f, 0.8f, 0.2f));
            comboLabel.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            comboLabel.style.display = DisplayStyle.None;
            root.Add(comboLabel);
        }

        private void BuildGoldDisplay(VisualElement root)
        {
            goldLabel = new Label("0 G");
            goldLabel.style.position = Position.Absolute;
            goldLabel.style.right = 20; goldLabel.style.top = 20;
            goldLabel.style.fontSize = 16;
            goldLabel.style.color = new StyleColor(new UnityEngine.Color(1f, 0.85f, 0.2f));
            root.Add(goldLabel);
        }

        // ═══════════════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════════════

        private VisualElement CreateBar(VisualElement parent, string label, StyleColor color, out Label valueLabel)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4;
            parent.Add(row);

            var nameLabel = new Label(label);
            nameLabel.style.width = 30;
            nameLabel.style.fontSize = 12;
            nameLabel.style.color = new StyleColor(UnityEngine.Color.white);
            row.Add(nameLabel);

            var bg = new VisualElement();
            bg.style.flexGrow = 1; bg.style.height = 14;
            bg.style.backgroundColor = new StyleColor(new UnityEngine.Color(0.15f, 0.15f, 0.15f));
            bg.style.borderBottomLeftRadius = bg.style.borderBottomRightRadius =
                bg.style.borderTopLeftRadius = bg.style.borderTopRightRadius = 4;
            row.Add(bg);

            var fill = new VisualElement();
            fill.style.height = 14;
            fill.style.width = Length.Percent(100);
            fill.style.backgroundColor = color;
            fill.style.borderBottomLeftRadius = fill.style.borderBottomRightRadius =
                fill.style.borderTopLeftRadius = fill.style.borderTopRightRadius = 4;
            bg.Add(fill);

            valueLabel = new Label("100/100");
            valueLabel.style.width = 80;
            valueLabel.style.fontSize = 11;
            valueLabel.style.color = new StyleColor(UnityEngine.Color.white);
            valueLabel.style.unityTextAlign = UnityEngine.TextAnchor.MiddleRight;
            row.Add(valueLabel);

            return fill;
        }

        // ═══════════════════════════════════════════════════════
        // Event binding
        // ═══════════════════════════════════════════════════════

        private void SubscribeEvents()
        {
            if (Game.Health != null)
                Game.Health.OnResourceChanged += OnResourceChanged;
            if (Game.Level != null)
            {
                Game.Level.OnLevelUp += OnLevelUp;
                Game.Level.OnExpGained += OnExpGained;
            }
            if (Game.Inv != null)
                Game.Inv.OnGoldChanged += OnGoldChanged;
            if (Game.Combo != null)
            {
                Game.Combo.OnComboCountChanged += OnComboChanged;
                Game.Combo.OnComboEnd += OnComboEnd;
            }
            if (Game.LockOn != null)
            {
                Game.LockOn.OnTargetLocked += OnTargetLocked;
                Game.LockOn.OnTargetLost += OnTargetLost;
            }
            if (Game.SkillBar != null)
                Game.SkillBar.OnCooldownUpdate += OnCooldownUpdate;
        }

        private void UnsubscribeEvents()
        {
            if (Game.Health != null)
                Game.Health.OnResourceChanged -= OnResourceChanged;
            if (Game.Level != null)
            {
                Game.Level.OnLevelUp -= OnLevelUp;
                Game.Level.OnExpGained -= OnExpGained;
            }
            if (Game.Inv != null)
                Game.Inv.OnGoldChanged -= OnGoldChanged;
            if (Game.Combo != null)
            {
                Game.Combo.OnComboCountChanged -= OnComboChanged;
                Game.Combo.OnComboEnd -= OnComboEnd;
            }
            if (Game.LockOn != null)
            {
                Game.LockOn.OnTargetLocked -= OnTargetLocked;
                Game.LockOn.OnTargetLost -= OnTargetLost;
            }
            if (Game.SkillBar != null)
                Game.SkillBar.OnCooldownUpdate -= OnCooldownUpdate;
        }

        // ═══════════════════════════════════════════════════════
        // Updates
        // ═══════════════════════════════════════════════════════

        private void RefreshAll()
        {
            if (Game.Health != null)
            {
                UpdateBar(hpBar, hpLabel, Game.Health.CurrentHP, Game.Health.MaxHP);
                UpdateBar(mpBar, mpLabel, Game.Health.CurrentMana, Game.Health.MaxMana);
                UpdateBar(chiBar, chiLabel, Game.Health.CurrentChi, Game.Health.MaxChi);
                UpdateBar(staminaBar, staminaLabel, Game.Health.CurrentStamina, Game.Health.MaxStamina);
            }
            if (Game.Level != null)
            {
                levelLabel.text = $"Lv.{Game.Level.Level}";
                UpdateBar(expBar, expLabel, Game.Level.CurrentExp, Game.Level.ExpToNextLevel);
            }
            goldLabel.text = $"{Game.Inv?.Gold ?? 0} G";
        }

        private void OnResourceChanged(ResourceType type, float oldVal, float newVal)
        {
            var h = Game.Health;
            if (h == null) return;
            switch (type)
            {
                case ResourceType.HP: UpdateBar(hpBar, hpLabel, h.CurrentHP, h.MaxHP); break;
                case ResourceType.Mana: UpdateBar(mpBar, mpLabel, h.CurrentMana, h.MaxMana); break;
                case ResourceType.Stamina: UpdateBar(staminaBar, staminaLabel, h.CurrentStamina, h.MaxStamina); break;
                case ResourceType.Chi: UpdateBar(chiBar, chiLabel, h.CurrentChi, h.MaxChi); break;
            }
        }

        private void UpdateBar(VisualElement bar, Label label, float current, float max)
        {
            float pct = max > 0 ? (current / max) * 100f : 0f;
            bar.style.width = Length.Percent(pct);
            label.text = $"{current:F0}/{max:F0}";
        }

        private void OnLevelUp(int newLevel) => levelLabel.text = $"Lv.{newLevel}";
        private void OnExpGained(float gained, float total) =>
            UpdateBar(expBar, expLabel, Game.Level.CurrentExp, Game.Level.ExpToNextLevel);
        private void OnGoldChanged(int gold) => goldLabel.text = $"{gold} G";

        private void OnComboChanged(int count)
        {
            comboLabel.text = $"COMBO x{count}!";
            comboLabel.style.display = DisplayStyle.Flex;
        }

        private void OnComboEnd()
        {
            comboLabel.style.display = DisplayStyle.None;
        }

        private void OnTargetLocked(ITargetLockable target)
        {
            targetPanel.style.display = DisplayStyle.Flex;
            targetNameLabel.text = "Enemy"; // TODO: get localized name from EnemyData
        }

        private void OnTargetLost()
        {
            targetPanel.style.display = DisplayStyle.None;
        }

        private void OnCooldownUpdate(int slot, float remaining)
        {
            if (slot < 0 || slot >= 6) return;
            skillCooldowns[slot].text = remaining > 0.1f ? $"{remaining:F1}" : "";
        }
    }
}
