using UnityEngine;
using BillInspector;

namespace RPGModular
{
    public enum StackBehavior { Refresh, AddDuration, StackIntensity, StackSeparate }

    [CreateAssetMenu(menuName = "Game/Status Effect Data")]
    [BillTitle("Status Effect", "Buff/Debuff definition")]
    public class StatusEffectData : ScriptableObject
    {
        [BillBoxGroup("Identity")]
        public string effectID;
        [BillBoxGroup("Identity"), BillLabelText("Name Key (Loc)")]
        public string nameKey;
        [BillBoxGroup("Identity"), BillLabelText("Desc Key (Loc)")]
        public string descKey;
        [BillBoxGroup("Identity"), BillPreviewField]
        public Sprite icon;

        [BillBoxGroup("Type")]
        public bool isDebuff;
        public bool isPermanent;

        [BillBoxGroup("Duration")]
        [BillShowIf("@!isPermanent")]
        [BillSlider(0f, 300f), BillSuffix("s")]
        public float baseDuration = 10f;

        [BillBoxGroup("Stacking")]
        [BillEnumToggleButtons]
        public StackBehavior stackBehavior;
        [BillShowIf("stackBehavior", StackBehavior.StackIntensity)]
        public int maxStacks = 5;

        [BillBoxGroup("Tick Effect")]
        [BillInfoBox("Negative = damage (DoT), positive = heal (HoT)")]
        public float tickValue;
        [BillSlider(0.5f, 5f), BillSuffix("s")]
        public float tickInterval = 1f;
        public DamageType tickDamageType;

        [BillBoxGroup("Stat Modifiers")]
        [BillTableList]
        public StatBonus[] statModifiers;

        [BillBoxGroup("Movement")]
        [BillSlider(0f, 2f)]
        public float moveSpeedMultiplier = 1f;

        [BillBoxGroup("Visual")]
        public string vfxPrefabId;
        public Color tintColor = Color.white;
    }
}
