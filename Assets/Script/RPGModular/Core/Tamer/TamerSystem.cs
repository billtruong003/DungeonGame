using System;
using System.Collections.Generic;
using UnityEngine;
using BillInspector;

namespace RPGModular
{
    [Serializable]
    public class PetInstance
    {
        public PetData Data;
        public string nickname;
        public int level = 1;
        public float currentExp;
        public int bond;
        public PetState state = PetState.Stored;
        public float currentHP;

        public float MaxHP => Data != null ? Data.baseHP + Data.hpPerLevel * (level - 1) : 1;
        public float Damage => Data != null ? Data.baseDamage + Data.damagePerLevel * (level - 1) : 0;
    }

    [BillTitle("Tamer System", "Pet capture, raise, fight, fuse")]
    public class TamerSystem : MonoBehaviour
    {
        [BillBoxGroup("Config")]
        [BillSlider(5, 50)]
        [SerializeField] private int maxStorageCapacity = 10;
        [BillBoxGroup("Config")]
        [BillSlider(10f, 60f), BillSuffix("s")]
        [SerializeField] private float fuseDuration = 30f;

        private List<PetInstance> storedPets = new List<PetInstance>();
        private PetInstance activePet;
        private GameObject activePetGO;
        private float fuseTimer;
        private List<StatModifier> fuseModifiers = new List<StatModifier>();

        public PetInstance ActivePet => activePet;
        public IReadOnlyList<PetInstance> StoredPets => storedPets;
        public int StorageCapacity => maxStorageCapacity;
        public bool IsFusing => fuseTimer > 0;

        public event Action<PetInstance> OnPetCaptured;
        public event Action OnCaptureFailed;
        public event Action<PetInstance> OnPetSummoned;
        public event Action<PetInstance> OnPetRecalled;
        public event Action<PetInstance, int> OnPetLevelUp;
        public event Action<PetInstance> OnPetFuseStart;
        public event Action<PetInstance> OnPetFuseEnd;
        public event Action<PetInstance, int> OnBondChanged;

        private void Update()
        {
            if (fuseTimer > 0)
            {
                fuseTimer -= Time.deltaTime;
                if (fuseTimer <= 0)
                    EndFuse();
            }
        }

        public bool TryCapture(EnemyData enemyData, float enemyHPPercent)
        {
            if (enemyData == null || !enemyData.isCapturable) return false;
            if (storedPets.Count >= maxStorageCapacity) return false;

            float rate = enemyData.baseCaptureRate * (1f - enemyHPPercent);
            // Tamer skill bonus would be applied here

            if (UnityEngine.Random.value <= rate)
            {
                var pet = new PetInstance
                {
                    Data = null, // Would need PetData reference from EnemyData
                    nickname = Loc.Get(enemyData.nameKey),
                    level = 1,
                    currentHP = 50
                };
                storedPets.Add(pet);
                OnPetCaptured?.Invoke(pet);
                return true;
            }

            OnCaptureFailed?.Invoke();
            return false;
        }

        public bool Summon(int petIndex)
        {
            if (petIndex < 0 || petIndex >= storedPets.Count) return false;
            if (activePet != null) Recall();

            activePet = storedPets[petIndex];
            activePet.state = PetState.Following;
            activePet.currentHP = activePet.MaxHP;

            // Spawn pet visual
            if (activePet.Data?.vatPrefab != null && Game.Player != null)
            {
                Vector3 spawnPos = Game.Player.transform.position + Game.Player.transform.right * 2f;
                activePetGO = Instantiate(activePet.Data.vatPrefab, spawnPos, Quaternion.identity);
            }

            OnPetSummoned?.Invoke(activePet);
            return true;
        }

        public void Recall()
        {
            if (activePet == null) return;
            activePet.state = PetState.Stored;
            var pet = activePet;
            activePet = null;

            if (activePetGO != null)
            {
                Destroy(activePetGO);
                activePetGO = null;
            }

            OnPetRecalled?.Invoke(pet);
        }

        public void Fuse()
        {
            if (activePet == null || IsFusing) return;
            if (activePet.Data?.fuseBonuses == null) return;

            fuseTimer = fuseDuration;

            // Apply pet stat bonuses to player
            foreach (var bonus in activePet.Data.fuseBonuses)
            {
                var mod = new StatModifier(bonus.stat, bonus.modType, bonus.value, 0, this);
                Game.Stats?.AddModifier(mod);
                fuseModifiers.Add(mod);
            }

            // Hide pet visual
            if (activePetGO != null) activePetGO.SetActive(false);

            OnPetFuseStart?.Invoke(activePet);
        }

        private void EndFuse()
        {
            // Remove fuse modifiers
            foreach (var mod in fuseModifiers)
                Game.Stats?.RemoveModifier(mod);
            fuseModifiers.Clear();

            // Show pet visual again
            if (activePetGO != null) activePetGO.SetActive(true);

            if (activePet != null)
                OnPetFuseEnd?.Invoke(activePet);
        }

        public void Feed(int petIndex, ItemData foodItem)
        {
            if (petIndex < 0 || petIndex >= storedPets.Count) return;
            if (foodItem == null || foodItem.type != ItemType.PetFood) return;
            if (Game.Inv == null || !Game.Inv.HasItem(foodItem)) return;

            Game.Inv.RemoveItem(foodItem, 1);
            var pet = storedPets[petIndex];
            pet.bond = Mathf.Min(pet.bond + 5, 100);
            OnBondChanged?.Invoke(pet, pet.bond);
        }

        public PetInstance Release(int petIndex)
        {
            if (petIndex < 0 || petIndex >= storedPets.Count) return null;
            var pet = storedPets[petIndex];
            if (activePet == pet) Recall();
            storedPets.RemoveAt(petIndex);
            return pet;
        }
    }
}
