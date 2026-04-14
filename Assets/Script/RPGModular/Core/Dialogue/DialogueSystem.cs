using System;
using UnityEngine;

namespace RPGModular
{
    public class DialogueSystem : MonoBehaviour
    {
        public static DialogueSystem Instance { get; private set; }

        public DialogueData CurrentDialogue { get; private set; }
        public DialogueNode CurrentNode { get; private set; }
        public bool IsActive => CurrentDialogue != null;

        public event Action<DialogueData> OnDialogueStart;
        public event Action<DialogueNode> OnNodeChanged;
        public event Action<int> OnChoiceSelected;
        public event Action<DialogueData> OnDialogueEnd;

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void StartDialogue(DialogueData data)
        {
            if (data == null || data.nodes == null || data.nodes.Length == 0) return;
            CurrentDialogue = data;
            SetNode(0);
            OnDialogueStart?.Invoke(data);
        }

        public void Advance()
        {
            if (CurrentNode == null) return;

            if (CurrentNode.type == DialogueNodeType.Text)
            {
                if (CurrentNode.nextNodeID < 0)
                    EndDialogue();
                else
                    SetNodeByID(CurrentNode.nextNodeID);
            }
        }

        public void SelectChoice(int choiceIndex)
        {
            if (CurrentNode?.type != DialogueNodeType.Choice) return;
            if (CurrentNode.choices == null || choiceIndex >= CurrentNode.choices.Length) return;

            OnChoiceSelected?.Invoke(choiceIndex);
            SetNodeByID(CurrentNode.choices[choiceIndex].targetNodeID);
        }

        public void EndDialogue()
        {
            var data = CurrentDialogue;
            CurrentDialogue = null;
            CurrentNode = null;
            OnDialogueEnd?.Invoke(data);
        }

        private void SetNode(int arrayIndex)
        {
            if (CurrentDialogue?.nodes == null || arrayIndex >= CurrentDialogue.nodes.Length)
            {
                EndDialogue();
                return;
            }
            CurrentNode = CurrentDialogue.nodes[arrayIndex];
            ProcessNode();
        }

        private void SetNodeByID(int nodeID)
        {
            if (CurrentDialogue?.nodes == null) { EndDialogue(); return; }

            for (int i = 0; i < CurrentDialogue.nodes.Length; i++)
            {
                if (CurrentDialogue.nodes[i].nodeID == nodeID)
                {
                    CurrentNode = CurrentDialogue.nodes[i];
                    ProcessNode();
                    return;
                }
            }
            EndDialogue();
        }

        private void ProcessNode()
        {
            if (CurrentNode == null) return;

            switch (CurrentNode.type)
            {
                case DialogueNodeType.Event:
                    HandleEventNode();
                    SetNodeByID(CurrentNode.afterEventNodeID);
                    return;

                case DialogueNodeType.Condition:
                    bool result = EvaluateCondition(CurrentNode.conditionField);
                    SetNodeByID(result ? CurrentNode.trueNodeID : CurrentNode.falseNodeID);
                    return;
            }

            OnNodeChanged?.Invoke(CurrentNode);
        }

        private void HandleEventNode()
        {
            // Extensible event handling
            // "give_quest", "open_shop", "give_item", etc.
        }

        private bool EvaluateCondition(string condition)
        {
            // Simple condition evaluation - can be extended
            return false;
        }
    }
}
