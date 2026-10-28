using Verse;

namespace VVRace
{
    // 멸망꽃 저주 헤디프(허기/피로/고통/정신 4종 + 보이지 않는 중독 표식).
    // 보통은 결속 해방(ClearDoomBlights)이나 엔딩 정리(CleanupAll)가 맵의 정착민에게서 제거하지만,
    // 그때 맵 밖(캐러밴 등)에 있던 정착민의 저주는 엔딩이 끝난 뒤(완료/실패) 스스로 사라진다.
    public class Hediff_DoomBlight : HediffWithComps
    {
        // ShouldRemove는 매 틱 모든 헤디프에 대해 불리므로 엔딩 종료 여부는 틱당 한 번만 계산한다.
        private static int _endingCheckedTick = -1;
        private static bool _endingFinished;

        private static bool EndingFinished
        {
            get
            {
                if (Current.Game == null) { return false; }

                var ticksGame = Find.TickManager.TicksGame;
                if (ticksGame != _endingCheckedTick)
                {
                    _endingCheckedTick = ticksGame;

                    var quest = ViviEndingQuest.Current;
                    // 아포크리파가 쓰러진 뒤(대관식 대기 포함)에는 멸망꽃 저주가 남지 않는다.
                    _endingFinished = quest != null && (quest.Stage == ViviEndingStage.Coronation || quest.Stage == ViviEndingStage.Completed || quest.Stage == ViviEndingStage.Failed);
                }

                return _endingFinished;
            }
        }

        public override bool ShouldRemove => base.ShouldRemove || EndingFinished;
    }
}
