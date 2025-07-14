using RimWorld;
using System;
using System.Text;
using Verse;

namespace VVRace
{
    public class ManaFluxRule_TimeOfDay : ManaFluxRule
    {
        public int mana;
        public TimeOfDay timeOfDay;
        public bool alwaysIfVaccumBiome;

        public override IntRange FluxRangeForDisplay => new IntRange(0, mana);

        public override string GetRuleString()
        {
            if (timeOfDay == TimeOfDay.Any)
            {
                return LocalizeString_Stat.VV_StatsReport_ManaFluxRule_Constant_Desc.Translate(mana.ToString("+0;-#"));
            }

            var sb = new StringBuilder();
            switch (timeOfDay)
            {
                case TimeOfDay.Day:
                    sb.Append(LocalizeString_Stat.VV_StatsReport_ManaFluxRule_TimeOfDay_Day_Desc.Translate(mana.ToString("+0;-#")));
                    break;

                case TimeOfDay.Night:
                    sb.Append(LocalizeString_Stat.VV_StatsReport_ManaFluxRule_TimeOfDay_Night_Desc.Translate(mana.ToString("+0;-#")));
                    break;
            }

            if (alwaysIfVaccumBiome)
            {
                sb.Append(" ");
                sb.Append(LocalizeString_Stat.VV_StatsReport_ManaFluxRule_TimeOfDay_VaccumBiome_Desc.Translate());
            }

            return sb.ToString();
        }
            

        public override int CalcManaFlux(Thing thing)
        {
            if (thing.Map.Biome.inVacuum && alwaysIfVaccumBiome)
            {
                return mana;
            }

            var dayPct = GenLocalDate.DayPercent(thing.Map);
            switch (timeOfDay)
            {
                case TimeOfDay.Day:
                    return 0.2f < dayPct && dayPct < 0.7f ? mana : 0;

                case TimeOfDay.Night:
                    return dayPct < 0.2f || dayPct > 0.7f ? mana : 0;
            }

            return mana;
        }
    }
}
