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
                // 의식 UI에서 선택한 날씨를 적용한다.
                var selectedWeather = everflower.ConsumeSelectedRitualWeather();

                // 엔딩 직전에 시작한 의식이 평화 기간에 끝났다면, 엔딩이 건 황금빛 꽃안개가 날씨를 붙들고 있으므로 바꾸지 않는다.
                // 아무 효과가 없으므로 다른 거부된 의식(요정화 해제)처럼 공유 재사용 대기도 걸지 않는다.
                if (GoldenBloomUtility.IsHeldByEnding(map))
                {
                    Messages.Message(LocalizeString_EndingCore.VV_FailReason_GoldenBloomHoldsWeather.Translate(), everflower, MessageTypeDefOf.RejectInput, false);
                    return;
                }

                // 앞서 의식으로 바꾼 날씨(황금빛 꽃안개 포함)는 걷어 낸다.
                var existingConditions = map.GameConditionManager.ActiveConditions
                    .Where(c => c.def == VVGameConditionDefOf.VV_EverflowerWeatherControl || (c is GameCondition_GoldenBloom golden && !golden.fromEnding))
                    .ToList();

                // 새 날씨를 먼저 건 뒤 앞선 날씨를 걷는다. 꽃안개가 끝나며 고른 날씨 대신 엉뚱한 날씨로 넘어가거나, 꽃안개를 다시 부를 때 깜빡이지 않게 한다.
                map.GameConditionManager.RegisterCondition(MakeWeatherCondition(everflower, map, selectedWeather));

                foreach (var existingCondition in existingConditions)
                {
                    existingCondition.End();
                }
            }

            FinishRitual(everflower, map, quality);
        }

        private static GameCondition MakeWeatherCondition(ArcanePlant_Everflower everflower, Map map, EverflowerWeatherChoice? selectedWeather)
        {
            // 황금빛 꽃안개: 날씨와 함께 마법 식물 마나 무제한 효과를 거는 전용 컨디션으로 부른다.
            if (selectedWeather == EverflowerWeatherChoice.GoldenBloom)
            {
                var goldenBloom = GameConditionMaker.MakeCondition(EndingCoreDefOf.VV_GoldenBloom, Rand.Range(60000, 120000));
                goldenBloom.conditionCauser = everflower;
                return goldenBloom;
            }

            var gameCond = GameConditionMaker.MakeCondition(VVGameConditionDefOf.VV_EverflowerWeatherControl, Rand.Range(60000, 120000)) as GameCondition_ForceWeather;
            gameCond.conditionCauser = everflower;

            var weather = selectedWeather.HasValue ? EverflowerWeatherChoiceUtility.Resolve(selectedWeather.Value, map) : null;

            // 구버전 세이브 또는 선택 없이 시작된 경우: 기존 동작대로 랜덤하게 결정한다.
            if (weather == null)
            {
                weather = map.weatherDecider.WeatherCommonalities.Where(v => v.weather != map.weatherManager.curWeather && v.weather.canOccurAsRandomForcedEvent).RandomElementByWeight(v => v.commonality).weather;
            }

            gameCond.weather = weather;
            return gameCond;
        }

        private void FinishRitual(ArcanePlant_Everflower everflower, Map map, float quality)
        {
            def.effecter.Spawn(everflower, map, everflower.EverflowerComp.RitualEffecterOffset).Cleanup();

            everflower.Notify_RitualComplete(quality);
        }
    }
}
