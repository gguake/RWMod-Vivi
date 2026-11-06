using Verse;
using Verse.AI.Group;

namespace VVRace
{
    // 둥지 로드의 토일(생활·떠나기) 공통: 둥지가 미리 만들어 둔 칸 집합(경계 문·작업 자리)과 증축 자리 실패 기억이 맵의 건물 변화를 바로 따르게 알린다.
    // 떠나는 동안에도 둥지 계획(경계 수리 등)이 칸 집합을 쓰므로 떠나기 토일도 알린다.
    public abstract class LordToil_ViviNest : LordToil
    {
        public override void Notify_BuildingSpawnedOnMap(Building b)
        {
            base.Notify_BuildingSpawnedOnMap(b);
            (lord?.LordJob as LordJob_ViviNest)?.Notify_BuildingChanged(b);
        }

        public override void Notify_BuildingDespawnedOnMap(Building b)
        {
            base.Notify_BuildingDespawnedOnMap(b);
            (lord?.LordJob as LordJob_ViviNest)?.Notify_BuildingChanged(b);
        }
    }
}
