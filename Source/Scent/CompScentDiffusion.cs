using RimWorld;
using System.Collections.Generic;
using System.Text;
using Verse;

namespace VVRace
{
    public class CompProperties_ScentDiffusion : CompProperties
    {
        public CompProperties_ScentDiffusion()
        {
            compClass = typeof(CompScentDiffusion);
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
        {
            foreach (var entry in base.SpecialDisplayStats(req))
            {
                yield return entry;
            }

            var hediff = ScentUtility.GetScentExtension(req.Def as ThingDef)?.scentHediff;
            if (hediff == null) { yield break; }

            yield return new StatDrawEntry(
                StatCategoryDefOf.Basics,
                LocalizeString_Scent.VV_StatsReport_FloralScent.Translate(),
                hediff.LabelCap,
                BuildScentReport(hediff),
                -22204,
                hyperlinks: Gen.YieldSingle(new Dialog_InfoCard.Hyperlink(hediff)));
        }

        public static string BuildScentReport(HediffDef hediff)
        {
            var sb = new StringBuilder();

            sb.Append(LocalizeString_Scent.VV_StatsReport_FloralScent_Desc.Translate());

            if (!hediff.description.NullOrEmpty())
            {
                sb.AppendLine();
                sb.AppendLine();
                sb.Append(hediff.description);
            }

            var stage = MaxStage(hediff);
            if (stage != null)
            {
                var lines = new StringBuilder();
                foreach (var entry in HediffStatsUtility.SpecialDisplayStats(stage, null))
                {
                    lines.AppendInNewLine("  - " + entry.LabelCap + ": " + entry.ValueString);
                }

                if (lines.Length > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine();
                    sb.Append(LocalizeString_Scent.VV_StatsReport_FloralScent_TierDesc.Translate());
                    sb.AppendLine();
                    sb.Append(lines);
                }
            }

            return sb.ToString();
        }

        public static HediffStage MaxStage(HediffDef hediff)
        {
            return hediff.stages != null && hediff.stages.Count > 0 ? hediff.stages[hediff.stages.Count - 1] : null;
        }
    }

    public class CompScentDiffusion : ThingComp
    {
    }
}
