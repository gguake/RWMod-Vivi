using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public class RitualOutcomeEffectWorker_ChangeWeather : RitualOutcomeEffectWorker_FromQuality
    {
        public RitualOutcomeEffectWorker_ChangeWeather() { }
        public RitualOutcomeEffectWorker_ChangeWeather(RitualOutcomeEffectDef def) : base(def) { }

        public override void Apply(float progress, Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual)
        {
            var quality = GetQuality(jobRitual, progress);
            var everflower = jobRitual.selectedTarget.Thing as ArcanePlant_Everflower;
            var map = everflower.Map;
            if (map.Tile.LayerDef == PlanetLayerDefOf.Surface)
            {
                var gameCond = GameConditionMaker.MakeCondition(VVGameConditionDefOf.VV_EverflowerWeatherControl, Rand.Range(60000, 120000)) as GameCondition_ForceWeather;
                gameCond.conditionCauser = everflower;
                gameCond.weather = map.weatherDecider.WeatherCommonalities.Where(v => v.weather != map.weatherManager.curWeather && v.weather.canOccurAsRandomForcedEvent).RandomElementByWeight(v => v.commonality).weather;

                map.GameConditionManager.RegisterCondition(gameCond);
            }

            def.effecter.Spawn(everflower, map).Cleanup();

            everflower.Notify_RitualComplete(quality);
        }
    }
}
