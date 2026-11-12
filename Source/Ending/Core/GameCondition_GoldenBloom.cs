using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VVRace
{
    public class GameCondition_GoldenBloom : GameCondition_ForceWeather
    {
        public bool fromEnding;

        public override void Init()
        {
            base.Init();

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
        public static IEnumerable<GameCondition_GoldenBloom> EndingConditions(Map map)
        {
            var conditions = map?.gameConditionManager.ActiveConditions;
            if (conditions == null) { yield break; }

            for (int i = 0; i < conditions.Count; ++i)
            {
                if (conditions[i] is GameCondition_GoldenBloom condition && condition.fromEnding)
                {
                    yield return condition;
                }
            }
        }

        public static bool IsHeldByEnding(Map map)
        {
            var quest = ViviEndingQuest.Current;
            return quest != null && quest.IsEndingActive && EndingConditions(map).Any();
        }
    }
}
