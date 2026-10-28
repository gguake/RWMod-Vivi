using HarmonyLib;

namespace VVRace
{
    // 아포크리파 1페이즈 기믹 모듈(Task E)의 Harmony 등록 지점. EndingCorePatch가 이름으로 찾아 호출한다.
    // 사출탄·씨앗·꽃·요정 간섭·연결선은 모두 자체 클래스(Projectile/Building/Hediff 파생)와 보스 컴프의 PostDraw 훅으로 처리되므로
    // 현재 바닐라 패치는 필요하지 않다. 패치를 추가할 때는 Patch() 안에서 DefOf/DefDatabase를 읽지 말 것(모드 생성자 시점).
    internal static class ApocryphaGimmickPatch
    {
        internal static void Patch(Harmony harmony)
        {
        }
    }
}
