using Verse;

namespace VVRace
{
    public class EverflowerRitualWorker_Attunement : EverflowerRitualWorker_SimpleJob
    {
        public EverflowerRitualWorker_Attunement(EverflowerRitualDef def) : base(def)
        {
        }

        public override void Complete(EverflowerRitualReservation reservation)
        {
            reservation.flower.EverflowerComp.LinkAttunement(reservation.casterPawn);

            base.Complete(reservation);
        }
    }
}
