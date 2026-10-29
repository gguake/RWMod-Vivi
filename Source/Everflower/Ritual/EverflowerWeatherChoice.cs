using RimWorld;
using System.Collections.Generic;
using Verse;

namespace VVRace
{
    public enum EverflowerWeatherChoice : byte
    {
        Clear = 0,
        Fog = 1,
        Rain = 2,
        GoldenBloom = 3,    // 엔딩 이후 6단계 영원꽃에서만 부를 수 있는 〈황금빛 꽃안개〉
    }

    public static class EverflowerWeatherChoiceUtility
    {
        public static readonly EverflowerWeatherChoice[] AllChoices = new[]
        {
            EverflowerWeatherChoice.Clear,
            EverflowerWeatherChoice.Fog,
            EverflowerWeatherChoice.Rain,
            EverflowerWeatherChoice.GoldenBloom,
        };

        // 이 영원꽃의 날씨 변경 의식에서 고를 수 있는 날씨. 황금빛 꽃안개는 엔딩을 마친 뒤 6단계 영원꽃에서만 고를 수 있다.
        public static IEnumerable<EverflowerWeatherChoice> AvailableChoices(ArcanePlant_Everflower everflower)
        {
            foreach (var choice in AllChoices)
            {
                if (choice != EverflowerWeatherChoice.GoldenBloom || CanCallGoldenBloom(everflower))
                {
                    yield return choice;
                }
            }
        }

        public static bool CanCallGoldenBloom(ArcanePlant_Everflower everflower)
        {
            var quest = ViviEndingQuest.Current;
            return everflower != null && quest != null && quest.IsCompleted && everflower.AttunementLevel >= CompEverflower.MaxAttunementLevel;
        }

        /// <summary>
        /// 선택한 날씨를 실제 적용할 WeatherDef로 변환한다.
        /// 비(Rain)는 기온이 영하인 경우 눈(SnowGentle)으로 대체된다.
        /// </summary>
        public static WeatherDef Resolve(EverflowerWeatherChoice choice, Map map)
        {
            switch (choice)
            {
                case EverflowerWeatherChoice.Clear:
                    return VVWeatherDefOf.Clear;
                case EverflowerWeatherChoice.Fog:
                    return VVWeatherDefOf.Fog;
                case EverflowerWeatherChoice.Rain:
                    return map.mapTemperature.OutdoorTemp < 0f ? VVWeatherDefOf.SnowGentle : VVWeatherDefOf.Rain;
                case EverflowerWeatherChoice.GoldenBloom:
                    return EndingCoreDefOf.VV_GoldenBloomMist;
                default:
                    return null;
            }
        }

        public static string Label(EverflowerWeatherChoice choice)
        {
            switch (choice)
            {
                case EverflowerWeatherChoice.Clear:
                    return VVWeatherDefOf.Clear.LabelCap;
                case EverflowerWeatherChoice.Fog:
                    return VVWeatherDefOf.Fog.LabelCap;
                case EverflowerWeatherChoice.Rain:
                    return VVWeatherDefOf.Rain.LabelCap;
                case EverflowerWeatherChoice.GoldenBloom:
                    return EndingCoreDefOf.VV_GoldenBloomMist.LabelCap;
                default:
                    return string.Empty;
            }
        }
    }
}
