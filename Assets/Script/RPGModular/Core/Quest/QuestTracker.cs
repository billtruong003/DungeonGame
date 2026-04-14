using System;
using System.Collections.Generic;
using UnityEngine;
using BillInspector;

namespace RPGModular
{
    [BillTitle("Quest Tracker", "Track active quests and objectives")]
    public class QuestTracker : MonoBehaviour
    {
        private List<QuestInstance> activeQuests = new List<QuestInstance>();
        private List<QuestInstance> completedQuests = new List<QuestInstance>();

        public IReadOnlyList<QuestInstance> ActiveQuests => activeQuests;
        public IReadOnlyList<QuestInstance> CompletedQuests => completedQuests;

        public event Action<QuestData> OnQuestAccepted;
        public event Action<QuestData, int, int, int> OnObjectiveProgress;
        public event Action<QuestData> OnQuestCompleted;
        public event Action<QuestData> OnQuestTurnedIn;

        public bool AcceptQuest(QuestData data)
        {
            if (data == null) return false;
            if (GetQuestState(data) != QuestState.Available) return false;

            var instance = new QuestInstance
            {
                Data = data,
                State = QuestState.Active,
                objectiveProgress = new int[data.objectives != null ? data.objectives.Length : 0],
                acceptedTime = Time.time
            };

            activeQuests.Add(instance);
            OnQuestAccepted?.Invoke(data);
            return true;
        }

        public void AbandonQuest(QuestData data)
        {
            activeQuests.RemoveAll(q => q.Data == data);
        }

        public QuestState GetQuestState(QuestData data)
        {
            if (completedQuests.Exists(q => q.Data == data)) return QuestState.TurnedIn;
            var active = activeQuests.Find(q => q.Data == data);
            if (active != null)
            {
                return AllObjectivesComplete(active) ? QuestState.Completed : QuestState.Active;
            }
            return QuestState.Available;
        }

        public bool TurnIn(QuestData data)
        {
            var instance = activeQuests.Find(q => q.Data == data);
            if (instance == null || !AllObjectivesComplete(instance)) return false;

            // Grant rewards
            if (data.expReward > 0) Game.Level?.AddExp(data.expReward);
            if (data.goldReward > 0) Game.Inv?.AddGold(data.goldReward);
            if (data.itemRewards != null)
                foreach (var r in data.itemRewards)
                    Game.Inv?.AddItem(r.item, r.quantity);

            activeQuests.Remove(instance);
            instance.State = QuestState.TurnedIn;
            completedQuests.Add(instance);

            OnQuestTurnedIn?.Invoke(data);
            return true;
        }

        /// <summary>Report progress. Called by game systems.</summary>
        public void ReportKill(EnemyData enemy)
        {
            ReportProgress(ObjectiveType.Kill, enemy.enemyID);
        }

        public void ReportCollect(ItemData item)
        {
            ReportProgress(ObjectiveType.Collect, item.itemID);
        }

        public void ReportProgress(ObjectiveType type, string targetID)
        {
            foreach (var quest in activeQuests)
            {
                if (quest.Data.objectives == null) continue;
                for (int i = 0; i < quest.Data.objectives.Length; i++)
                {
                    var obj = quest.Data.objectives[i];
                    if (obj.type != type) continue;

                    string objTargetID = type switch
                    {
                        ObjectiveType.Kill => obj.targetEnemy?.enemyID,
                        ObjectiveType.Collect => obj.targetItem?.itemID,
                        _ => null
                    };

                    if (objTargetID != targetID) continue;

                    if (quest.objectiveProgress[i] < obj.requiredCount)
                    {
                        quest.objectiveProgress[i]++;
                        OnObjectiveProgress?.Invoke(quest.Data, i,
                            quest.objectiveProgress[i], obj.requiredCount);

                        if (AllObjectivesComplete(quest))
                            OnQuestCompleted?.Invoke(quest.Data);
                    }
                }
            }
        }

        private bool AllObjectivesComplete(QuestInstance quest)
        {
            if (quest.Data.objectives == null) return true;
            for (int i = 0; i < quest.Data.objectives.Length; i++)
            {
                if (quest.objectiveProgress[i] < quest.Data.objectives[i].requiredCount)
                    return false;
            }
            return true;
        }
    }

    [Serializable]
    public class QuestInstance
    {
        public QuestData Data;
        public QuestState State;
        public int[] objectiveProgress;
        public float acceptedTime;
    }
}
