using RimWorld;
using RimWorld.Planet;
using Verse;

namespace VVRace
{
    // 코어를 추출하기 전에는 원정대가 떠나거나 전멸해도 맵만 지우고 유적은 남긴다.
    // 코어를 추출하면 재봉인 연출 뒤 인원을 월드로 내보내며 유적이 닫힌다.
    public class Site_ViviAncientRuins : Site
    {
        private static bool ClosesWhenLeft => ViviEndingQuest.Current?.Ruins.CoreExtracted ?? true;

        public override AcceptanceReport CanBeSettled => false;

        public override bool ShouldRemoveMapNow(out bool alsoRemoveWorldObject)
        {
            if (HasMap && Map.GetComponent<MapComponent_ViviAncientRuins>()?.IsResealing == true)
            {
                alsoRemoveWorldObject = false;
                return false;
            }

            if (!base.ShouldRemoveMapNow(out alsoRemoveWorldObject)) { return false; }

            alsoRemoveWorldObject = ClosesWhenLeft;
            return true;
        }

        // 중력선 이륙은 ShouldRemoveMapNow를 거치지 않고 바로 Abandon을 부른다.
        public override void Abandon(bool wasGravshipLaunch)
        {
            if (ClosesWhenLeft)
            {
                base.Abandon(wasGravshipLaunch);
                return;
            }

            if (HasMap)
            {
                Current.Game.DeinitAndRemoveMap(Map, notifyPlayer: false);
            }
        }
    }
}
