using RimWorld;
using Verse;

namespace VVRace
{
    // 엔딩 퀘스트 — 영원꽃 제단·5/6단계 영원꽃(Task A) 전용 DefOf. 병렬 작업 충돌을 피하려고 VVDefOf.cs 대신 여기에 둔다.
    [DefOf]
    public static class AltarDefOf
    {
        public static ThingDef VV_DreamumAltar;

        public static EffecterDef VV_Effecter_AltarEnshrine;
    }
}
