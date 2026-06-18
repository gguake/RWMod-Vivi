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
                var existingConditions = map.GameConditionManager.ActiveConditions
                    .Where(c => c.def == VVGameConditionDefOf.VV_EverflowerWeatherControl)
                    .ToList();

                foreach (var existingCondition in existingConditions)
                {
                    existingCondition.End();
                }

                var gameCond = GameConditionMaker.MakeCondition(VVGameConditionDefOf.VV_EverflowerWeatherControl, Rand.Range(60000, 120000)) as GameCondition_ForceWeather;
                gameCond.conditionCauser = everflower;

                // 의식 UI에서 선택한 날씨를 적용한다.
                var selectedWeather = everflower.ConsumeSelectedRitualWeather();
                var weather = selectedWeather.HasValue ? EverflowerWeatherChoiceUtility.Resolve(selectedWeather.Value, map) : null;

                // 구버전 세이브 또는 선택 없이 시작된 경우: 기존 동작대로 랜덤하게 결정한다.
                if (weather == null)
                {
                    weather = map.weatherDecider.WeatherCommonalities.Where(v => v.weather != map.weatherManager.curWeather && v.weather.canOccurAsRandomForcedEvent).RandomElementByWeight(v => v.commonality).weather;
                }

                gameCond.weather = weather;
                map.GameConditionManager.RegisterCondition(gameCond);
            }

            def.effecter.Spawn(everflower, map, everflower.EverflowerComp.RitualEffecterOffset).Cleanup();

            everflower.Notify_RitualComplete(quality);
        }
    }
}
