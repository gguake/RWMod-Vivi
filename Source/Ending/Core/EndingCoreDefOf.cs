using RimWorld;
using Verse;

namespace VVRace
{
    [DefOf]
    public static class EndingCoreDefOf
    {
        public static QuestScriptDef VV_EndGame_ViviMissive;
        public static QuestScriptDef VV_EndGame_ViviBloom;

        public static GameConditionDef VV_OminousSilence;

        public static ThoughtDef VV_EverflowerFullBloom;

        public static EffecterDef VV_Effecter_EndingGoldenWave;
        public static FleckDef VV_Fleck_GoldenWaveRing;
        public static FleckDef VV_Fleck_EndingPetal;

        public static EffecterDef VV_Effecter_AltarEverflowerPulse_5;
        public static EffecterDef VV_Effecter_AltarEverflowerPulse_6;

        public static GameConditionDef VV_GoldenBloom;
        public static WeatherDef VV_GoldenBloomMist;
        public static FleckDef VV_Fleck_GoldenBloomPetal;

        public static ThingDef VV_DreamumAltar;
        public static ThingDef VV_AncientAmberCore;

        public static FactionDef VV_ViviFederation;
        public static FactionDef VV_PlayerExiledVivi;
        public static FactionDef VV_PlayerViviRefugee;

        // 여제·지도자 유형은 사망 시 연방이 적대화되므로 지원군으로 사용하지 않는다.
        public static PawnKindDef VV_ViviGuard;
        public static PawnKindDef VV_ViviFirefighter;
        public static PawnKindDef VV_ViviSniper;
    }
}
