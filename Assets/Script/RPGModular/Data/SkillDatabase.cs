using System.Collections.Generic;
using UnityEngine;
using BillInspector;

namespace RPGModular
{
    /// <summary>
    /// Registry of all skills. ID-based lookup for Save/Load and SpacetimeDB.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Skill Database")]
    [BillTitle("Skill Database", "Registry of all game skills")]
    public class SkillDatabase : ScriptableObject
    {
        [BillTableList]
        public SkillData[] allSkills;
        [BillTableList]
        public SkillTreeData[] allTrees;

        private Dictionary<string, SkillData> _skillLookup;

        public SkillData GetSkillByID(string skillID)
        {
            if (_skillLookup == null) BuildLookup();
            return _skillLookup.TryGetValue(skillID, out var skill) ? skill : null;
        }

        private void BuildLookup()
        {
            _skillLookup = new Dictionary<string, SkillData>();
            if (allSkills != null)
                foreach (var s in allSkills)
                    if (s != null && !string.IsNullOrEmpty(s.skillID))
                        _skillLookup[s.skillID] = s;
        }

        private void OnEnable() => _skillLookup = null;
    }
}
