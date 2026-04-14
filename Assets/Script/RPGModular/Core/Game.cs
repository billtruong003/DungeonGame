using UnityEngine;

namespace RPGModular
{
    /// <summary>
    /// Static facade for all game systems. Access via Game.Stats, Game.Health, etc.
    /// Pattern mirrors Bill.* from BillGameCore.
    /// </summary>
    public static class Game
    {
        private static PlayerCore _player;

        public static PlayerCore Player
        {
            get
            {
                if (_player == null)
                    _player = Object.FindFirstObjectByType<PlayerCore>();
                return _player;
            }
        }

        // --- Player Systems ---
        public static CharacterStats Stats => Player?.Stats;
        public static HealthSystem Health => Player?.Health;
        public static Inventory Inv => Player?.Inventory;
        public static EquipmentSystem Equip => Player?.Equipment;
        public static LevelSystem Level => Player?.Level;
        public static PlayerSkillBook SkillBook => Player?.SkillBook;
        public static SkillBar SkillBar => Player?.SkillBar;
        public static SkillCaster Skill => Player?.SkillCaster;
        public static ComboTracker Combo => Player?.Combo;
        public static StatusEffectSystem Status => Player?.StatusEffects;
        public static CombatStateMachine Combat => Player?.CombatSM;
        public static LockOnSystem LockOn => Player?.LockOn;
        public static WeaponHandler Weapon => Player?.Weapon;
        public static AutoAttackSystem AutoAttack => Player?.AutoAttack;
        public static FocusGauge Focus => Player?.Focus;
        public static PlayerDamageHandler DamageHandler => Player?.DamageHandler;

        // --- Singletons (không trên player) ---
        public static LocalizationService Loc => LocalizationService.Instance;

        // --- Reset khi scene change ---
        public static void ClearCache() => _player = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void DomainReload() => _player = null;
    }
}
