using System;
using UnityEngine;

namespace RPGModular
{
    public enum RespawnOption { Town, InPlace }

    public class DeathSystem : MonoBehaviour
    {
        public static DeathSystem Instance { get; private set; }

        [SerializeField] private float goldPenaltyPercent = 0.1f;
        [SerializeField] private int inPlaceGoldCostMin = 100;
        [SerializeField] private float inPlaceGoldCostPercent = 0.05f;

        public int GoldPenalty => Game.Inv != null ? Mathf.RoundToInt(Game.Inv.Gold * goldPenaltyPercent) : 0;

        public event Action OnPlayerDied;
        public event Action<RespawnOption> OnPlayerRespawned;

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void Die()
        {
            // Apply gold penalty
            int penalty = GoldPenalty;
            if (penalty > 0) Game.Inv?.SpendGold(penalty);

            OnPlayerDied?.Invoke();
        }

        public void Respawn(RespawnOption option)
        {
            switch (option)
            {
                case RespawnOption.Town:
                    // Respawn at last town, full HP
                    Game.Health?.Revive(1f);
                    break;

                case RespawnOption.InPlace:
                    int cost = Mathf.Max(inPlaceGoldCostMin,
                        Mathf.RoundToInt((Game.Inv?.Gold ?? 0) * inPlaceGoldCostPercent));
                    Game.Inv?.SpendGold(cost);
                    Game.Health?.Revive(0.5f);
                    break;
            }

            OnPlayerRespawned?.Invoke(option);
        }
    }
}
