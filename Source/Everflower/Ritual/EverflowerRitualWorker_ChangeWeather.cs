using RimWorld;
using System.Linq;
using Verse;

namespace VVRace
{
    public class EverflowerRitualWorker_ChangeWeather : EverflowerRitualWorker_SimpleJob
    {
        public EverflowerRitualWorker_ChangeWeather(EverflowerRitualDef def) : base(def)
        {
        }

        public override void Complete(EverflowerRitualReservation reservation)
        {
            var weather = DefDatabase<WeatherDef>.AllDefs.RandomElement();
            reservation.flower.Map.weatherManager.TransitionTo(weather);

            if (reservation.ritualDef.effectOnComplete != null)
            {
                reservation.ritualDef.effectOnComplete.SpawnMaintained(reservation.flower.Position, reservation.flower.Map);
            }

            base.Complete(reservation);
        }
    }
}
