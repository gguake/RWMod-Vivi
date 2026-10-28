using HarmonyLib;

namespace VVRace
{
    // 엔딩 퀘스트 — 영원꽃 제단(Task A) Harmony 패치 등록 지점. EndingCorePatch.Patch가 이름으로 찾아 호출한다.
    // 제단은 발판에 마법 식물이 있으면 지을 수 없고(PlaceWorker_DreamumAltar), 청사진·틀 위로는 마법 식물을 옮길 수 없으므로
    // (ArcanePlantUtility.CanPlaceArcanePlantToCell) 틀이 영원꽃을 제거하는 상황이 발생하지 않아 현재는 바닐라 패치가 필요하지 않다.
    // 패치를 추가할 때는 Patch() 안에서 DefOf/DefDatabase를 읽지 말 것(모드 생성자 시점).
    internal static class AltarPatch
    {
        internal static void Patch(Harmony harmony)
        {
        }
    }
}
