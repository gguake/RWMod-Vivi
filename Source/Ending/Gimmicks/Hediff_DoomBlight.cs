using RimWorld;
using Verse;

namespace VVRace
{
    public class Hediff_DoomBlight : HediffWithComps
    {
        private static int _endingCheckedTick = -1;
        private static bool _endingFinished;

        private int _expireTick = -1;

        public bool IsTimed => _expireTick >= 0;
        public int ExpireTick => _expireTick;

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

        public override bool ShouldRemove =>
            base.ShouldRemove
            || EndingFinished
            || (_expireTick >= 0 && Current.Game != null && Find.TickManager.TicksGame >= _expireTick);

        public override string LabelInBrackets
        {
            get
            {
                if (_expireTick >= 0 && Current.Game != null)
                {
                    var remaining = _expireTick - Find.TickManager.TicksGame;
                    if (remaining > 0)
                    {
                        return remaining.ToStringTicksToPeriod(allowSeconds: false, shortForm: true);
                    }
                }

                return base.LabelInBrackets;
            }
        }

        public override string TipStringExtra
        {
            get
            {
                var level = LocalizeString_Etc.VV_Hediff_DoomBlightLevel.Translate(CurStageIndex + 1, DoomBlightUtility.MaxBlightLevel).Resolve();
                return EndingVfxUtility.AppendTipLine(base.TipStringExtra, level);
            }
        }

        public void SetTimedExpiration(int expireTick)
        {
            _expireTick = expireTick;
        }

        public void ExtendTimedExpiration(int expireTick)
        {
            if (_expireTick >= 0 && expireTick > _expireTick)
            {
                _expireTick = expireTick;
            }
        }

        public void ClearTimedExpiration()
        {
            _expireTick = -1;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _expireTick, "expireTick", -1);
        }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);
            EndingVfxUtility.RemoveSelfIfPresent(pawn, this);
        }
    }
}
