using Verse;

namespace VVRace
{
    public class EverflowerRitualWorker_UndoFairy : EverflowerRitualWorker_SimpleJob
    {
        public EverflowerRitualWorker_UndoFairy(EverflowerRitualDef def) : base(def)
        {
        }

        public override AcceptanceReport CanRitual(ArcanePlant_Everflower everflower, Pawn caster)
        {
            if (!caster.TryGetComp<CompViviHolder>(out var compViviHolder) || compViviHolder.InnerViviCount == 0)
            {
                return LocalizeString_Etc.VV_FailReason_HasNoFairyVivi.Translate();
            }

            return base.CanRitual(everflower, caster);
        }

        public override void Complete(EverflowerRitualReservation reservation)
        {
            if (reservation.casterPawn.TryGetComp<CompViviHolder>(out var compViviHolder))
            {
                var vivi = compViviHolder.DetachVivi();
                if (vivi != null)
                {
                    vivi.health.AddHediff(VVHediffDefOf.VV_FairyficationSickness);

                    if (reservation.ritualDef.effectOnComplete != null)
                    {
                        reservation.ritualDef.effectOnComplete.Spawn(vivi.Position, vivi.Map);
                    }
                }
            }

            base.Complete(reservation);
        }
    }
}
