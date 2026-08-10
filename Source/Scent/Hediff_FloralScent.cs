using Verse;

namespace VVRace
{
    public class Hediff_FloralScent : HediffWithComps
    {
        public bool fromPerfume;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref fromPerfume, "fromPerfume");
        }
    }
}
