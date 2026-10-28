using RimWorld;
using Verse;

namespace VVRace
{
    // 엔딩 의식·비비 보상(Task B) 전용 DefOf. 병렬 작업 충돌을 피하려고 공용 VVDefOf.cs에는 넣지 않는다.
    [DefOf]
    public static class EndingRitualDefOf
    {
        // 2페이즈 동안 앵커 영원꽃에서 유일하게 허용되는 의식(공명 파동)
        public static PreceptDef VV_Attunement;

        // 아포크리파 토벌 뒤 엔딩을 끝내는 의식(재사용 대기 무시)
        public static PreceptDef VV_QueensCoronation;

        public static HediffDef VV_EverflowerBlessing;
        public static HediffDef VV_GeneticUnstability;

        public static EffecterDef VV_Effecter_ResonanceWave;
        public static EffecterDef VV_Effecter_ResonanceWaveImpact;

        public static FleckDef VV_Fleck_ResonanceBeam;
    }
}
