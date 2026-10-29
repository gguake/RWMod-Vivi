using RimWorld;
using Verse;

namespace VVRace
{
    // 엔딩 퀘스트 코어(퀘스트 컨트롤러·공성·엔딩) 전용 DefOf. 병렬 작업 충돌을 피하려고 공용 VVDefOf.cs 대신 모듈 폴더에 둔다.
    [DefOf]
    public static class EndingCoreDefOf
    {
        // 평화 기간 〈불길한 정적〉 표시용 게임 컨디션
        public static GameConditionDef VV_OminousSilence;

        // 엔딩 연출
        public static EffecterDef VV_Effecter_EndingGoldenWave;
        public static FleckDef VV_Fleck_GoldenWaveRing;
        public static FleckDef VV_Fleck_EndingPetal;

        // 엔딩 동안 제단 영원꽃의 주기적 발광(제단 위 5/6단계 그래픽 기준)
        public static EffecterDef VV_Effecter_AltarEverflowerPulse_5;
        public static EffecterDef VV_Effecter_AltarEverflowerPulse_6;

        // 〈황금빛 꽃안개〉: 평화 기간~엔딩, 엔딩 이후 날씨 변경 의식
        public static GameConditionDef VV_GoldenBloom;
        public static WeatherDef VV_GoldenBloomMist;
        public static FleckDef VV_Fleck_GoldenBloomPetal;

        // 제단 모듈 소유 def (이름은 계획서에 고정)
        public static ThingDef VV_DreamumAltar;
        public static ThingDef VV_AncientAmberCore;
        public static ResearchProjectDef VV_AncientAltarRestoration;

        // 비비 연방과 시나리오별 플레이어 세력
        public static FactionDef VV_ViviFederation;
        public static FactionDef VV_PlayerExiledVivi;
        public static FactionDef VV_PlayerViviRefugee;

        // 연방 지원군 (여제·리더 종류는 사망 시 연방이 적대화되므로 쓰지 않는다)
        public static PawnKindDef VV_ViviGuard;
        public static PawnKindDef VV_ViviFirefighter;
        public static PawnKindDef VV_ViviSniper;
    }
}
