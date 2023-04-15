using Verse;

namespace VVRace
{
    public class HediffDef_Specialization : HediffDef
    {
        public SimpleCurve severityByTicks;
    }

    public class Hediff_Specialization : Hediff
    {
        public int specializedTicks = 1;

        public override float Severity
        {
            get
            {
                if (def is HediffDef_Specialization hediffDef_Specialization)
                {
                    return hediffDef_Specialization.severityByTicks.Evaluate(specializedTicks);
                }

                return 0.1f;
            }
            set { }
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref specializedTicks, "specializedTicks");
        }

        public override void Tick()
        {
            if (!pawn.Spawned || pawn.Downed || pawn.Dead || !pawn.IsColonistPlayerControlled) { return; }
            if (pawn.DevelopmentalStage.Newborn() || pawn.DevelopmentalStage.Baby()) { return; }

            specializedTicks++;
        }
    }
}
