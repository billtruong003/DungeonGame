using System;
using System.Collections.Generic;
using UnityEngine;
using BillInspector;

namespace RPGModular
{
    [BillTitle("Loot System", "Handles enemy death rewards")]
    public class LootSystem : MonoBehaviour
    {
        public static LootSystem Instance { get; private set; }

        public event Action<ItemData, int> OnLootDropped;
        public event Action<float> OnExpGranted;
        public event Action<int> OnGoldGranted;

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>
        /// Call when enemy dies. Rolls loot, grants EXP + Gold.
        /// </summary>
        public void ProcessEnemyDeath(EnemyData enemyData, LootTable lootTable, Vector3 deathPosition)
        {
            if (enemyData == null) return;

            // Grant EXP
            if (enemyData.expReward > 0 && Game.Level != null)
            {
                Game.Level.AddExp(enemyData.expReward);
                OnExpGranted?.Invoke(enemyData.expReward);
            }

            // Grant Gold
            if (enemyData.goldReward > 0 && Game.Inv != null)
            {
                Game.Inv.AddGold(enemyData.goldReward);
                OnGoldGranted?.Invoke(enemyData.goldReward);
            }

            // Roll loot
            if (lootTable != null)
            {
                var drops = lootTable.Roll();
                foreach (var (item, qty) in drops)
                {
                    if (Game.Inv != null)
                    {
                        int overflow = Game.Inv.AddItem(item, qty);
                        if (overflow > 0)
                        {
                            // Could spawn world pickup for overflow
                        }
                    }
                    OnLootDropped?.Invoke(item, qty);
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
