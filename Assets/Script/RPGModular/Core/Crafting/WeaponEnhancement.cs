using System;
using UnityEngine;

namespace RPGModular
{
    [Serializable]
    public class WeaponInstance
    {
        public WeaponData BaseData;
        public int enhanceLevel;

        public float EffectiveDamage => BaseData != null
            ? BaseData.BaseDamage * (1f + 0.05f * enhanceLevel)
            : 0f;

        public string DisplayName
        {
            get
            {
                string baseName = BaseData != null ? Loc.Get(BaseData.nameKey) : "???";
                return enhanceLevel > 0 ? $"{baseName} +{enhanceLevel}" : baseName;
            }
        }
    }

    public class WeaponEnhancement : MonoBehaviour
    {
        public static WeaponEnhancement Instance { get; private set; }

        public event Action<WeaponInstance, EnhanceResult> OnEnhanceResult;

        private static readonly float[] SuccessRates = { 1f, 1f, 1f, 0.8f, 0.8f, 0.6f, 0.4f, 0.3f, 0.2f, 0.1f };

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public float GetSuccessRate(WeaponInstance weapon)
        {
            if (weapon == null) return 0f;
            int level = weapon.enhanceLevel;
            if (level >= SuccessRates.Length) return 0f;
            return SuccessRates[level];
        }

        public EnhanceResult TryEnhance(WeaponInstance weapon, ItemData enhancementStone)
        {
            if (weapon == null || enhancementStone == null) return EnhanceResult.Fail;
            if (weapon.enhanceLevel >= 10) return EnhanceResult.Fail;

            // Consume stone
            if (Game.Inv != null)
                Game.Inv.RemoveItem(enhancementStone, 1);

            float rate = GetSuccessRate(weapon);
            bool success = UnityEngine.Random.value <= rate;

            EnhanceResult result;
            if (success)
            {
                weapon.enhanceLevel++;
                result = EnhanceResult.Success;
            }
            else if (weapon.enhanceLevel >= 7)
            {
                weapon.enhanceLevel--;
                result = EnhanceResult.Downgrade;
            }
            else
            {
                result = EnhanceResult.Fail;
            }

            OnEnhanceResult?.Invoke(weapon, result);
            return result;
        }
    }
}
