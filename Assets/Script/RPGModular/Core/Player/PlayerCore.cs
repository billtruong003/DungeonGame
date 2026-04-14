using UnityEngine;
using BillInspector;

namespace RPGModular
{
    /// <summary>
    /// Hub component — expose tất cả sub-systems.
    /// Attach lên Player root GO. Game.cs resolve PlayerCore → access bất kỳ system nào.
    /// </summary>
    [BillTitle("Player Core", "Hub component for all player sub-systems")]
    public class PlayerCore : MonoBehaviour
    {
        // All auto-found in Awake
        [BillReadOnly, BillShowInInspector, BillBoxGroup("Core")]
        public CharacterStats Stats { get; private set; }
        [BillReadOnly, BillShowInInspector, BillBoxGroup("Core")]
        public HealthSystem Health { get; private set; }
        [BillReadOnly, BillShowInInspector, BillBoxGroup("Core")]
        public PlayerInputHandler Input { get; private set; }
        [BillReadOnly, BillShowInInspector, BillBoxGroup("Core")]
        public PlayerController Controller { get; private set; }

        [BillReadOnly, BillShowInInspector, BillBoxGroup("Combat")]
        public CombatStateMachine CombatSM { get; private set; }
        [BillReadOnly, BillShowInInspector, BillBoxGroup("Combat")]
        public LocomotionStateMachine LocoSM { get; private set; }
        [BillReadOnly, BillShowInInspector, BillBoxGroup("Combat")]
        public LockOnSystem LockOn { get; private set; }
        [BillReadOnly, BillShowInInspector, BillBoxGroup("Combat")]
        public WeaponHandler Weapon { get; private set; }
        [BillReadOnly, BillShowInInspector, BillBoxGroup("Combat")]
        public AutoAttackSystem AutoAttack { get; private set; }
        [BillReadOnly, BillShowInInspector, BillBoxGroup("Combat")]
        public PlayerDamageHandler DamageHandler { get; private set; }
        [BillReadOnly, BillShowInInspector, BillBoxGroup("Combat")]
        public FocusGauge Focus { get; private set; }

        [BillReadOnly, BillShowInInspector, BillBoxGroup("Progression")]
        public Inventory Inventory { get; private set; }
        [BillReadOnly, BillShowInInspector, BillBoxGroup("Progression")]
        public EquipmentSystem Equipment { get; private set; }
        [BillReadOnly, BillShowInInspector, BillBoxGroup("Progression")]
        public LevelSystem Level { get; private set; }
        [BillReadOnly, BillShowInInspector, BillBoxGroup("Progression")]
        public StatusEffectSystem StatusEffects { get; private set; }

        [BillReadOnly, BillShowInInspector, BillBoxGroup("Skill")]
        public PlayerSkillBook SkillBook { get; private set; }
        [BillReadOnly, BillShowInInspector, BillBoxGroup("Skill")]
        public SkillBar SkillBar { get; private set; }
        [BillReadOnly, BillShowInInspector, BillBoxGroup("Skill")]
        public SkillCaster SkillCaster { get; private set; }
        [BillReadOnly, BillShowInInspector, BillBoxGroup("Skill")]
        public ComboTracker Combo { get; private set; }

        private void Awake()
        {
            Stats = GetComponent<CharacterStats>();
            Health = GetComponent<HealthSystem>();
            Input = GetComponent<PlayerInputHandler>();
            Controller = GetComponent<PlayerController>();
            CombatSM = GetComponent<CombatStateMachine>();
            LocoSM = GetComponent<LocomotionStateMachine>();
            LockOn = GetComponent<LockOnSystem>();
            Weapon = GetComponent<WeaponHandler>();
            AutoAttack = GetComponent<AutoAttackSystem>();
            DamageHandler = GetComponent<PlayerDamageHandler>();
            Focus = GetComponent<FocusGauge>();
            Inventory = GetComponent<Inventory>();
            Equipment = GetComponent<EquipmentSystem>();
            Level = GetComponent<LevelSystem>();
            StatusEffects = GetComponent<StatusEffectSystem>();
            SkillBook = GetComponent<PlayerSkillBook>();
            SkillBar = GetComponent<SkillBar>();
            SkillCaster = GetComponent<SkillCaster>();
            Combo = GetComponent<ComboTracker>();
        }

#if UNITY_EDITOR
        [BillButton("Log All Systems")]
        private void DebugLogSystems()
        {
            Debug.Log($"=== PlayerCore Systems ===");
            Debug.Log($"Stats: {(Stats != null ? "OK" : "MISSING")}");
            Debug.Log($"Health: {(Health != null ? "OK" : "MISSING")}");
            Debug.Log($"CombatSM: {(CombatSM != null ? "OK" : "MISSING")}");
            Debug.Log($"LocoSM: {(LocoSM != null ? "OK" : "MISSING")}");
            Debug.Log($"LockOn: {(LockOn != null ? "OK" : "MISSING")}");
            Debug.Log($"Weapon: {(Weapon != null ? "OK" : "MISSING")}");
            Debug.Log($"Inventory: {(Inventory != null ? "OK" : "MISSING")}");
            Debug.Log($"Equipment: {(Equipment != null ? "OK" : "MISSING")}");
            Debug.Log($"Level: {(Level != null ? "OK" : "MISSING")}");
            Debug.Log($"SkillBook: {(SkillBook != null ? "OK" : "MISSING")}");
            Debug.Log($"SkillBar: {(SkillBar != null ? "OK" : "MISSING")}");
            Debug.Log($"SkillCaster: {(SkillCaster != null ? "OK" : "MISSING")}");
            Debug.Log($"Combo: {(Combo != null ? "OK" : "MISSING")}");
            Debug.Log($"StatusEffects: {(StatusEffects != null ? "OK" : "MISSING")}");
            Debug.Log($"DamageHandler: {(DamageHandler != null ? "OK" : "MISSING")}");
            Debug.Log($"Focus: {(Focus != null ? "OK" : "MISSING")}");
            Debug.Log($"Input: {(Input != null ? "OK" : "MISSING")}");
            Debug.Log($"Controller: {(Controller != null ? "OK" : "MISSING")}");
        }
#endif
    }
}
