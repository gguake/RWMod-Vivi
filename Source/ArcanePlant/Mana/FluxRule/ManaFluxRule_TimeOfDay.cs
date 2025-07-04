using RimWorld;
using System;
using Verse;

namespace VVRace
{
    public class ManaFluxRule_TimeOfDay : ManaFluxRule
    {
        public int mana;
        public TimeOfDay timeOfDay;

        public override IntRange FluxRangeForDisplay => new IntRange(0, mana);

        public override string GetRuleString()
        {
            switch (timeOfDay)
            {
                case TimeOfDay.Day:
                    return LocalizeString_Stat.VV_StatsReport_ManaFluxRule_TimeOfDay_Day_Desc.Translate(mana.ToString("+0;-#"));

                case TimeOfDay.Night:
                    return LocalizeString_Stat.VV_StatsReport_ManaFluxRule_TimeOfDay_Night_Desc.Translate(mana.ToString("+0;-#"));
            }

            return LocalizeString_Stat.VV_StatsReport_ManaFluxRule_Constant_Desc.Translate(mana.ToString("+0;-#"));
        }
            

        public override int CalcManaFlux(Thing thing)
        {
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
