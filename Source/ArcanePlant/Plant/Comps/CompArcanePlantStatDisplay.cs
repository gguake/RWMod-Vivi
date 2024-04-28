using RimWorld;
using System.Collections.Generic;
using System.Text;
using Verse;

namespace VVRace
{
    public class CompProperties_ArcanePlantStatsDisplay : CompProperties
    {
        public CompProperties_ArcanePlantStatsDisplay()
        {
            compClass = typeof(CompArcanePlantStatDisplay);
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
        {
            var manaExtension = req.Def?.GetModExtension<ManaExtension>();
            if (manaExtension != null)
            {
                yield return new StatDrawEntry(
                    StatCategoryDefOf.Basics,
                    LocalizeString_Stat.VV_StatsReport_Mana.Translate(),
                    manaExtension.manaCapacity.ToString(),
                    LocalizeString_Stat.VV_StatsReport_Mana_Desc.Translate(),
                    -22200);

                var sbManaFluxDesc = new StringBuilder(LocalizeString_Stat.VV_StatsReport_ManaFlux_Desc.Translate());
                var approximateManaRange = new IntRange(0, 0);
                if (manaExtension.manaGenerateRule != null)
                {
                    var genRange = manaExtension.manaGenerateRule.ApproximateManaFlux;
                    approximateManaRange.min += genRange.min;
                    approximateManaRange.max += genRange.max;

                    sbManaFluxDesc.AppendLine();
                    sbManaFluxDesc.AppendInNewLine(manaExtension.manaGenerateRule.GetRuleString(false));
                }

                if (manaExtension.manaConsumeRule != null)
                {
                    var conRange = manaExtension.manaConsumeRule.ApproximateManaFlux;
                    approximateManaRange.min -= conRange.max;
                    approximateManaRange.max -= conRange.min;

                    sbManaFluxDesc.AppendLine();
                    sbManaFluxDesc.AppendInNewLine(manaExtension.manaConsumeRule.GetRuleString(true));
                }

                yield return new StatDrawEntry(
                    StatCategoryDefOf.Basics,
                    LocalizeString_Stat.VV_StatsReport_ManaFlux.Translate(),
                    approximateManaRange.min == approximateManaRange.max ? approximateManaRange.min.ToString() : approximateManaRange.ToString(),
                    sbManaFluxDesc.ToString(),
                    -22201);

            }

            var plantExtension = req.Def?.GetModExtension<ArcanePlantExtension>();
            if (plantExtension != null && plantExtension.consumeManaPerVerbShoot > 0)
            {
                yield return new StatDrawEntry(
                    StatCategoryDefOf.Basics,
                    LocalizeString_Stat.VV_StatsReport_ManaPerShoot.Translate(),
                    plantExtension.consumeManaPerVerbShoot.ToString(),
                    LocalizeString_Stat.VV_StatsReport_ManaPerShootDesc.Translate(),
                    -22202);
            }
        }
    }

    public class CompArcanePlantStatDisplay : ThingComp
    {
    }
}
