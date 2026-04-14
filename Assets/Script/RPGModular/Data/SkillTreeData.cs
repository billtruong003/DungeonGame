using UnityEngine;
using BillInspector;

namespace RPGModular
{
    [CreateAssetMenu(menuName = "Game/Skill Tree Data")]
    [BillTitle("Skill Tree", "One branch of the skill system")]
    public class SkillTreeData : ScriptableObject
    {
        [BillBoxGroup("Identity")]
        public SkillTreeType treeType;
        [BillBoxGroup("Identity"), BillLabelText("Name Key (Loc)")]
        public string nameKey;
        [BillBoxGroup("Identity"), BillLabelText("Desc Key (Loc)")]
        public string descKey;
        [BillBoxGroup("Identity"), BillPreviewField]
        public Sprite icon;

        [BillBoxGroup("Requirements")]
        public WeaponType[] compatibleWeapons;

        [BillBoxGroup("Skills")]
        [BillTableList]
        public SkillData[] skills;

        [BillBoxGroup("Tier Unlock")]
        [BillInfoBox("Number of skills learned at tier N to unlock tier N+1")]
        public int[] tierUnlockRequirements = { 0, 0, 2, 3, 4 };
    }
}
