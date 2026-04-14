using System;
using UnityEngine;
using BillGameCore;

namespace RPGModular
{
    public class SaveLoadSystem : MonoBehaviour
    {
        public static SaveLoadSystem Instance { get; private set; }

        private const int MaxSlots = 4; // 0=auto, 1-3=manual
        private float playTimeAccumulator;

        public event Action OnSaveCompleted;
        public event Action OnLoadCompleted;

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Update()
        {
            playTimeAccumulator += Time.deltaTime;
        }

        public void Save(int slotIndex = 0)
        {
            var data = BuildSaveData();
            string json = JsonUtility.ToJson(data, true);
            string key = $"save_slot_{slotIndex}";

            if (Bill.Save != null)
                Bill.Save.Set(key, json);
            else
                PlayerPrefs.SetString(key, json);

            OnSaveCompleted?.Invoke();
        }

        public void Load(int slotIndex = 0)
        {
            string key = $"save_slot_{slotIndex}";
            string json = null;

            if (Bill.Save != null)
                json = Bill.Save.GetString(key);
            else
                json = PlayerPrefs.GetString(key, null);

            if (string.IsNullOrEmpty(json)) return;

            var data = JsonUtility.FromJson<SaveData>(json);
            ApplySaveData(data);

            OnLoadCompleted?.Invoke();
        }

        public bool HasSave(int slotIndex)
        {
            string key = $"save_slot_{slotIndex}";
            return PlayerPrefs.HasKey(key);
        }

        public void AutoSave() => Save(0);

        public void DeleteSave(int slotIndex)
        {
            string key = $"save_slot_{slotIndex}";
            PlayerPrefs.DeleteKey(key);
        }

        private SaveData BuildSaveData()
        {
            var data = new SaveData
            {
                lastSaveTime = DateTime.Now.ToString("o"),
                playTime = playTimeAccumulator,
                level = Game.Level?.Level ?? 1,
                currentExp = Game.Level?.CurrentExp ?? 0,
                unspentStatPoints = Game.Level?.UnspentStatPoints ?? 0,
                unspentSkillPoints = Game.Level?.UnspentSkillPoints ?? 0,
                currentHP = Game.Health?.CurrentHP ?? 100,
                currentMana = Game.Health?.CurrentMana ?? 50,
                currentStamina = Game.Health?.CurrentStamina ?? 100,
                currentChi = Game.Health?.CurrentChi ?? 0,
                gold = Game.Inv?.Gold ?? 0,
                language = LocalizationService.Instance?.CurrentLanguage ?? "vi",
            };

            // Save position
            if (Game.Player != null)
            {
                var pos = Game.Player.transform.position;
                data.playerPosition = new[] { pos.x, pos.y, pos.z };
                var rot = Game.Player.transform.eulerAngles;
                data.playerRotation = new[] { rot.x, rot.y, rot.z };
            }

            return data;
        }

        private void ApplySaveData(SaveData data)
        {
            if (data == null) return;

            playTimeAccumulator = data.playTime;

            // Position
            if (data.playerPosition != null && data.playerPosition.Length >= 3 && Game.Player != null)
            {
                Game.Player.transform.position = new Vector3(
                    data.playerPosition[0], data.playerPosition[1], data.playerPosition[2]);
            }

            // Language
            if (!string.IsNullOrEmpty(data.language))
                LocalizationService.Instance?.SetLanguage(data.language);
        }
    }
}
