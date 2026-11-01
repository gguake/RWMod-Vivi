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
        GoldenBloom = 3,
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
