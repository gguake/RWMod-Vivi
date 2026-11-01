using Verse;

namespace VVRace
{
    public class Hediff_DoomBlight : HediffWithComps
    {
        // ShouldRemove는 매 틱 모든 헤디프에서 호출되므로 엔딩 종료 여부는 틱당 한 번만 계산한다.
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
                    _endingFinished = quest != null && quest.Stage >= ViviEndingStage.Coronation;
                }

                return _endingFinished;
            }
        }

        public override bool ShouldRemove => base.ShouldRemove || EndingFinished;

        public override string TipStringExtra
        {
            get
            {
                var level = LocalizeString_EndingGimmicks.VV_Hediff_DoomBlightLevel.Translate(CurStageIndex + 1, DoomBlightUtility.MaxBlightLevel).Resolve();
                var tip = base.TipStringExtra;
                return tip.NullOrEmpty() ? level : level + "\n" + tip;
            }
        }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);

            // 죽은 폰은 건강 상태 틱이 없고 맵 정리도 스폰된 폰만 처리하므로 사망 시 즉시 제거한다.
            // 1.7의 HediffSet.Notify_PawnDied는 역순으로 순회하므로 다른 헤디프의 알림이 누락되지 않는다.
            if (pawn?.health != null && pawn.health.hediffSet.hediffs.Contains(this))
            {
                pawn.health.RemoveHediff(this);
            }
        }
    }
}
