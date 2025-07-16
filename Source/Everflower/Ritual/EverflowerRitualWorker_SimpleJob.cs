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
            onStartCallback(new EverflowerRitualReservation()
            {
                ritualDef = _def,
                casterPawn = caster,
            });
        }

        public override Job TryGiveJob(ArcanePlant_Everflower flower)
        {
            if (flower.CurReservationInfo == null || flower.CurReservedRitual.Worker != this) { return null; }

            if (!flower.CurReservationInfo.casterPawn.CanReserveAndReach(flower, PathEndMode.Touch, Danger.Deadly)) { return null; }

            var job = JobMaker.MakeJob(_def.job, flower);
            return job;
        }
    }
}
