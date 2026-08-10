using RimWorld;
using System.Collections.Generic;
using System.Text;
using Verse;

namespace VVRace
{
    public class CompProperties_ScentDiffusion : CompProperties
    {
        public int intervalTicks = 250;

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

            var stage = hediff.stages != null && hediff.stages.Count > 0 ? hediff.stages[0] : null;
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
                    sb.Append(lines);
                }
            }

            return sb.ToString();
        }
    }

    // 마법 꽃이 자신이 있는 방 전체에 향을 퍼뜨린다. 마나 상태와 무관하게 동작하며
    // 야외(PsychologicallyOutdoors)에서는 확산하지 않는다.
    public class CompScentDiffusion : ThingComp
    {
        private int _nextDiffuseTick;

        public CompProperties_ScentDiffusion Props => (CompProperties_ScentDiffusion)props;

        public override void CompTick()
        {
            TryDiffuse();
        }

        public override void CompTickInterval(int delta)
        {
            TryDiffuse();
        }

        private void TryDiffuse()
        {
            if (GenTicks.TicksGame < _nextDiffuseTick) { return; }
            _nextDiffuseTick = GenTicks.TicksGame + Props.intervalTicks;

            if (!parent.Spawned) { return; }

            var flowerDef = parent.def;
            if (!ScentUtility.IsScentFlower(flowerDef)) { return; }

            var room = parent.Position.GetRoom(parent.Map);
            if (room == null || room.PsychologicallyOutdoors) { return; }

            foreach (var region in room.Regions)
            {
                foreach (var thing in region.ListerThings.ThingsInGroup(ThingRequestGroup.Pawn))
                {
                    if (thing is Pawn pawn)
                    {
                        ScentUtility.ApplyScent(pawn, flowerDef);
                    }
                }
            }
        }
    }
}
