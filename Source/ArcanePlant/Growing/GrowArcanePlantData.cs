using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public enum GrowArcanePlantSensitivity
    {
        None,
        Low,
        Medium, 
        High
    }

    public class GrowArcanePlantData : DefModExtension
    {
        public GrowArcanePlantSensitivity ManaSensitivity
        {
            get
            {
                if (badManaDamageByDayCurve == null) { return GrowArcanePlantSensitivity.None; }
                return GrowArcanePlantSensitivity.High;
            }
        }

        public GrowArcanePlantSensitivity TemperatureSensitivity
        {
            get
            {
                if (badTemperatureDamageByDayCurve == null) { return GrowArcanePlantSensitivity.None; }
                return GrowArcanePlantSensitivity.High;
            }
        }

        public GrowArcanePlantSensitivity GlowSensitivity
        {
            get
            {
                if (badGlowDamageByDayCurve == null) { return GrowArcanePlantSensitivity.None; }
                return GrowArcanePlantSensitivity.High;
            }
        }

        public GrowArcanePlantSensitivity ManageSensitivity
        {
            get
            {
                if (badManageDamageByDayCurve == null) { return GrowArcanePlantSensitivity.None; }
                return GrowArcanePlantSensitivity.High;
            }
        }

        public List<ThingDefCountClass> ingredients;

        public float totalGrowDays = 5;
        public float baseAmount = 3;

        public int maxHealth = 100;
        public int maxMana = 200;
        public float healthRegenNoDamagedByDays = 10;
        public float consumedManaByDay = 100;

        public SimpleCurve successChanceCurve;
        public SimpleCurve healthBonusAmountCurve;
        public SimpleCurve cleanlinessBonusAmountCurve;

        public FloatRange optimalTemperatureRange = new FloatRange(-9999f, 9999f);
        public float badTemperatureThresholdDay = 0.5f;
        public SimpleCurve badTemperatureDamageByDayCurve;

        public FloatRange optimalGlowRange = new FloatRange(0f, 1f);
        public float badGlowThresholdDay = 0.5f;
        public SimpleCurve badGlowDamageByDayCurve;

        public int manageIntervalTicks;
        public float badManageThresholdDay = 1;
        public SimpleCurve badManageDamageByDayCurve;

        public int requiredMinMana = 50;
        public float badManaThresholdDay = 0.5f;
        public SimpleCurve badManaDamageByDayCurve;
    }
}
