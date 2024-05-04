using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class GrowingArcanePlantData : DefModExtension
    {
        public List<ThingDefCountClass> ingredients;

        public float totalGrowDays = 5;
        public float baseAmount = 2.5f;

        public int maxHealth = 100;
        public int maxMana = 200;
        public float healthRegenNoDamagedByDays = 10;
        public float consumedManaByDay = 100;

        public SimpleCurve successChanceCurve;
        public SimpleCurve healthBonusAmountCurve;
        public SimpleCurve cleanlinessBonusAmountCurve;

        public FloatRange optimalTemperatureRange = new FloatRange(-9999f, 9999f);
        public GrowingArcanePlantSensitivity temperatureSensitivity;

        public FloatRange optimalGlowRange = new FloatRange(0f, 1f);
        public GrowingArcanePlantSensitivity glowSensitivity;

        public bool canGrow = true;

        public int RealManageIntervalTicks => 
            manageSensitivity > GrowingArcanePlantSensitivity.None ? 
            Mathf.CeilToInt(manageIntervalTicks / manageSensitivity.CalcThreshold().danger) : 
            0;
        public int manageIntervalTicks;
        public GrowingArcanePlantSensitivity manageSensitivity;

        public GrowingArcanePlantSensitivity manaSensitivity;
    }
}
