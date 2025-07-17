using System;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class EverflowerRitualWorker_SimpleJob : EverflowerRitualWorker
    {
        public EverflowerRitualWorker_SimpleJob(EverflowerRitualDef def) : base(def)
        {
        }

        public override void StartRitual(ArcanePlant_Everflower flower, Pawn caster, Action<EverflowerRitualReservation> onStartCallback)
        {
            onStartCallback(new EverflowerRitualReservation(flower)
            {
                ritualDef = _def,
                casterPawn = caster,
            });
        }

        public override Job TryGiveJob(EverflowerRitualReservation reservation)
        {
            if (reservation.ritualDef == null || reservation.ritualDef.Worker != this) { return null; }

            if (!reservation.flower.CurReservationInfo.casterPawn.CanReserveAndReach(reservation.flower, PathEndMode.Touch, Danger.Deadly)) { return null; }

            var job = JobMaker.MakeJob(_def.job, reservation.flower);
            return job;
        }
    }
}
