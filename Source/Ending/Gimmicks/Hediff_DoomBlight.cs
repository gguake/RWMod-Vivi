using Verse;

namespace VVRace
{
    // 멸망꽃 저주 헤디프(공포/허기/역병 3종).
    // 보통은 결속 해방(ClearDoomBlights)이나 엔딩 정리(CleanupAll)가 맵의 정착민에게서 제거하지만,
    // 그때 맵 밖(캐러밴 등)에 있던 정착민의 저주는 엔딩이 끝난 뒤(완료/실패) 스스로 사라지고, 죽은 정착민의 저주는 죽는 순간 사라진다.
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
                    _endingFinished = quest != null && quest.Stage >= ViviEndingStage.Coronation;
                }

                return _endingFinished;
            }
        }

        public override bool ShouldRemove => base.ShouldRemove || EndingFinished;

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);

            // 죽은 폰은 헬스 틱이 돌지 않아 ShouldRemove로 사라지지 않고, 맵 정리도 스폰된 폰만 훑으므로 여기서 바로 제거한다.
            // (1.7 HediffSet.Notify_PawnDied는 헤디프 목록을 역순으로 돌므로 즉시 제거해도 다른 헤디프를 건너뛰지 않는다)
            if (pawn?.health != null && pawn.health.hediffSet.hediffs.Contains(this))
            {
                pawn.health.RemoveHediff(this);
            }
        }
    }
}
