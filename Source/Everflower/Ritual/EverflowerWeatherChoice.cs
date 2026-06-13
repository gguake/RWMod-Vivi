using RimWorld;
using Verse;

namespace VVRace
{
    public enum EverflowerWeatherChoice : byte
    {
        Clear = 0,
        Fog = 1,
        Rain = 2,
    }

    public static class EverflowerWeatherChoiceUtility
    {
        public static readonly EverflowerWeatherChoice[] AllChoices = new[]
        {
            EverflowerWeatherChoice.Clear,
            EverflowerWeatherChoice.Fog,
            EverflowerWeatherChoice.Rain,
        };

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
                default:
                    return string.Empty;
            }
        }
    }
}
