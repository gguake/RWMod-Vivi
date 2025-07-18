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
            var map = reservation.flower.Map;
            if (!map.Biome.inVacuum)
            {
                var gameCond = GameConditionMaker.MakeCondition(VVGameConditionDefOf.VV_EverflowerWeatherControl, Rand.Range(60000, 120000)) as GameCondition_ForceWeather;
                gameCond.conditionCauser = reservation.flower;
                gameCond.hideSource = true;
                gameCond.weather = map.weatherDecider.WeatherCommonalities.Where(v => v.weather != map.weatherManager.curWeather).RandomElementByWeight(v => v.commonality).weather;

                map.GameConditionManager.RegisterCondition(gameCond);
            }

            if (reservation.ritualDef.effectOnComplete != null)
            {
                reservation.ritualDef.effectOnComplete.SpawnMaintained(reservation.flower.Position, map);
            }

            base.Complete(reservation);
        }
    }
}
