using Verse;

namespace VVRace
{
    public class Hediff_EverflowerQueen : HediffWithComps
    {
        public ArcanePlant_Everflower everflower;

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);

            if (everflower != null && !everflower.Destroyed)
            {
                everflower.PawnHolder.Notify_QueenDied(pawn);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref everflower, "everflower");
        }
    }
}
