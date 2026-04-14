using UnityEngine;
using BillInspector;

namespace RPGModular
{
    [CreateAssetMenu(menuName = "Game/NPC Data")]
    [BillTitle("NPC Data", "Non-player character")]
    public class NPCData : ScriptableObject
    {
        [BillBoxGroup("Identity")]
        public string npcID;
        [BillBoxGroup("Identity"), BillLabelText("Name Key (Loc)")]
        public string nameKey;
        [BillBoxGroup("Identity"), BillPreviewField]
        public Sprite portrait;

        [BillBoxGroup("Interaction")]
        [BillEnumToggleButtons]
        public NPCRole role;
        [BillShowIf("role", NPCRole.Merchant)]
        [BillInlineEditor]
        public ShopData shopData;
        [BillShowIf("role", NPCRole.QuestGiver)]
        public QuestData[] availableQuests;

        [BillBoxGroup("Dialogue")]
        [BillInlineEditor]
        public DialogueData greetingDialogue;
    }
}
