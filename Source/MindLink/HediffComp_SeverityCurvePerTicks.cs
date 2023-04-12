using RimWorld;
using Verse;

namespace VVRace
{
    public class HediffCompProperties_SeverityCurvePerTicks : HediffCompProperties
    {
        public SimpleCurve severityCurve;

        public HediffCompProperties_SeverityCurvePerTicks()
        {
            compClass = typeof(HediffComp_SeverityCurvePerTicks);
        }
    }

    public class HediffComp_SeverityCurvePerTicks : HediffComp
    {
        private HediffCompProperties_SeverityCurvePerTicks Props => (HediffCompProperties_SeverityCurvePerTicks)props;
        private const int updateInterval = 2500;

        public int tickElapsed = 0;

        public override void CompExposeData()
        {
            base.CompExposeData();

            Scribe_Values.Look(ref tickElapsed, "tickElapsed");
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            if (Pawn.IsHashIntervalTick(updateInterval))
            {
                if (Pawn.IsColonistPlayerControlled && !Pawn.Downed && !Pawn.Dead)
                {
                    tickElapsed += updateInterval;
                }

                var severityExpected = Props.severityCurve.Evaluate(tickElapsed);
                severityAdjustment = parent.Severity - severityExpected;
            }
        }
    }
}
