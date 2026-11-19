using Verse;

namespace VVRace
{
    public class Hediff_DoomBlight : HediffWithComps
    {
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
                var level = LocalizeString_Etc.VV_Hediff_DoomBlightLevel.Translate(CurStageIndex + 1, DoomBlightUtility.MaxBlightLevel).Resolve();
                return EndingVfxUtility.AppendTipLine(base.TipStringExtra, level);
            }
        }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);
            EndingVfxUtility.RemoveSelfIfPresent(pawn, this);
        }
    }
}
