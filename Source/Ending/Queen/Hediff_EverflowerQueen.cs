using Verse;

namespace VVRace
{
    public class Hediff_EverflowerQueen : HediffWithComps
    {
        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);
            ViviEndingQuest.Current?.Notify_QueenDied(pawn);
        }
    }
}
