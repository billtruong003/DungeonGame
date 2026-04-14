using System;
using UnityEngine;
using BillInspector;

namespace RPGModular
{
    [CreateAssetMenu(menuName = "Game/Dialogue Data")]
    [BillTitle("Dialogue", "Conversation data")]
    public class DialogueData : ScriptableObject
    {
        public string dialogueID;
        [BillTableList]
        public DialogueNode[] nodes;
    }

    [Serializable]
    public class DialogueNode
    {
        public int nodeID;
        [BillEnumToggleButtons]
        public DialogueNodeType type;

        // Text
        [BillShowIf("type", DialogueNodeType.Text)]
        public string speakerNameKey;
        [BillShowIf("type", DialogueNodeType.Text)]
        public Sprite speakerPortrait;
        [BillShowIf("type", DialogueNodeType.Text)]
        public string textKey;
        [BillShowIf("type", DialogueNodeType.Text)]
        public int nextNodeID = -1;

        // Choice
        [BillShowIf("type", DialogueNodeType.Choice)]
        [BillTableList]
        public DialogueChoice[] choices;

        // Condition
        [BillShowIf("type", DialogueNodeType.Condition)]
        public string conditionField;
        [BillShowIf("type", DialogueNodeType.Condition)]
        public int trueNodeID;
        [BillShowIf("type", DialogueNodeType.Condition)]
        public int falseNodeID;

        // Event
        [BillShowIf("type", DialogueNodeType.Event)]
        public string eventName;
        [BillShowIf("type", DialogueNodeType.Event)]
        public string eventParam;
        [BillShowIf("type", DialogueNodeType.Event)]
        public int afterEventNodeID;
    }

    [Serializable]
    public class DialogueChoice
    {
        public string textKey;
        public int targetNodeID;
        public string condition;
    }
}
