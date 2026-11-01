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
                var selectedWeather = everflower.ConsumeSelectedRitualWeather();

                // 의식 도중 평화 기간에 진입하면 엔딩의 황금빛 꽃안개가 날씨를 고정하므로 변경을 취소한다.
                // 효과가 적용되지 않았으므로 공유 재사용 대기도 시작하지 않는다.
                if (GoldenBloomUtility.IsHeldByEnding(map))
                {
                    Messages.Message(LocalizeString_EndingCore.VV_FailReason_GoldenBloomHoldsWeather.Translate(), everflower, MessageTypeDefOf.RejectInput, false);
                    return;
                }

                var existingConditions = map.GameConditionManager.ActiveConditions
                    .Where(c => c.def == VVGameConditionDefOf.VV_EverflowerWeatherControl || (c is GameCondition_GoldenBloom golden && !golden.fromEnding))
                    .ToList();

                // 날씨가 엉뚱하게 바뀌거나 꽃안개가 깜빡이지 않도록 새 날씨를 적용한 뒤 기존 날씨를 해제한다.
                map.GameConditionManager.RegisterCondition(MakeWeatherCondition(everflower, map, selectedWeather));

                foreach (var existingCondition in existingConditions)
                {
                    existingCondition.End();
                }
            }

            FinishRitual(everflower, map, quality);
            ViviEndingQuest.Current?.Notify_EverflowerRitualEcho(everflower, jobRitual.PawnWithRole("resonator"));
        }

        private static GameCondition MakeWeatherCondition(ArcanePlant_Everflower everflower, Map map, EverflowerWeatherChoice? selectedWeather)
        {
            if (selectedWeather == EverflowerWeatherChoice.GoldenBloom)
            {
                var goldenBloom = GameConditionMaker.MakeCondition(EndingCoreDefOf.VV_GoldenBloom, Rand.Range(60000, 120000));
                goldenBloom.conditionCauser = everflower;
                return goldenBloom;
            }

            var gameCond = GameConditionMaker.MakeCondition(VVGameConditionDefOf.VV_EverflowerWeatherControl, Rand.Range(60000, 120000)) as GameCondition_ForceWeather;
            gameCond.conditionCauser = everflower;

            var weather = selectedWeather.HasValue ? EverflowerWeatherChoiceUtility.Resolve(selectedWeather.Value, map) : null;

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
