using RimWorld;
using Verse;

namespace VVRace
{
    // 〈황금빛 꽃안개〉: 옅은 금빛 안개와 영원꽃 꽃잎이 흩날리는 날씨(VV_GoldenBloomMist)를 강제하고, 이 맵의 모든 마법 식물의 마나를 무제한으로 만든다.
    // 엔딩 퀘스트가 평화 기간부터 엔딩까지 걸고(fromEnding), 엔딩 이후에는 영원꽃의 날씨 변경 의식으로 부를 수 있다.
    public class GameCondition_GoldenBloom : GameCondition_ForceWeather
    {
        // 엔딩 퀘스트가 건 꽃안개인지. 엔딩이 진행되는 동안에는 날씨 변경 의식이 이 꽃안개를 걷어 내지 못한다.
        public bool fromEnding;

        public override void Init()
        {
            base.Init();

            // 강제 날씨는 지금 날씨가 한참(4000틱) 이어진 뒤에야 바뀌므로 곧바로 전환을 시작한다.
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

            // 끝난 뒤에도 꽃안개가 날씨 지속 시간만큼 남아 있지 않도록 곧바로 다음 날씨로 넘긴다.
            // (다른 꽃안개가 같은 날씨를 이어서 강제하고 있으면 그대로 둔다)
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
            return map?.gameConditionManager.GetActiveCondition(EndingCoreDefOf.VV_GoldenBloom) as GameCondition_GoldenBloom;
        }

        public static bool IsActive(Map map)
        {
            return map != null && map.gameConditionManager.ConditionIsActive(EndingCoreDefOf.VV_GoldenBloom);
        }

        // 〈황금빛 꽃안개〉가 걸린 맵의 마법 식물은 마나가 줄지 않는다(CompMana).
        public static bool IsManaUnlimited(Thing thing)
        {
            return thing is ArcanePlant && IsActive(thing.MapHeld);
        }

        // 엔딩 퀘스트가 건 꽃안개가 엔딩 진행 중에 이 맵을 덮고 있는지(날씨 변경 의식을 막는다).
        public static bool IsHeldByEnding(Map map)
        {
            var quest = ViviEndingQuest.Current;
            return quest != null && quest.IsEndingActive && GetActiveCondition(map)?.fromEnding == true;
        }
    }
}
