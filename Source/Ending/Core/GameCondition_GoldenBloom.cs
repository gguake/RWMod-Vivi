using RimWorld;
using Verse;

namespace VVRace
{
    public class GameCondition_GoldenBloom : GameCondition_ForceWeather
    {
        public bool fromEnding;

        public override void Init()
        {
            base.Init();

            // 강제 날씨 변경은 기존 날씨가 4000틱 유지된 뒤에 적용되므로 전환을 직접 시작한다.
            var map = SingleMap;
            if (map != null && weather != null && map.weatherManager.curWeather != weather)
            {
                map.weatherDecider.StartNextWeather();
            }
        }

        public override void End()
        {
            var map = SingleMap;
            base.End();

            // 종료 후 꽃안개가 기존 날씨 지속 시간만큼 남지 않도록 다음 날씨로 즉시 전환한다.
            // 다른 꽃안개 컨디션이 같은 날씨를 강제 중이면 유지한다.
            if (map != null && weather != null && map.weatherManager.curWeather == weather && map.weatherDecider.ForcedWeather != weather)
            {
                map.weatherDecider.StartNextWeather();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref fromEnding, "fromEnding", false);
        }
    }

    public static class GoldenBloomUtility
    {
        public static GameCondition_GoldenBloom GetActiveCondition(Map map)
        {
            return map?.gameConditionManager.GetActiveCondition(VVGameConditionDefOf.VV_GoldenBloom) as GameCondition_GoldenBloom;
        }

        public static bool IsActive(Map map)
        {
            return map != null && map.gameConditionManager.ConditionIsActive(VVGameConditionDefOf.VV_GoldenBloom);
        }

        public static bool IsManaUnlimited(Thing thing)
        {
            return thing is ArcanePlant && IsActive(thing.MapHeld);
        }

        public static bool IsHeldByEnding(Map map)
        {
            var quest = ViviEndingQuest.Current;
            return quest != null && quest.IsEndingActive && GetActiveCondition(map)?.fromEnding == true;
        }
    }
}
