using RimWorld;
using Verse;

namespace VVRace
{
    public class QuestPart_ViviBloomCondensation : QuestPartActivable
    {
        public const float LockedProgress = 0.99f;
        private const int TickInterval = 250;
        private const int FallbackDurationTicks = 600000;

        public ArcanePlant_Everflower anchor;
        public int durationTicks;

        private float _progress;

        public float Progress => _progress;
        public int DurationTicks => durationTicks;

        public override string ExpiryInfoPart
        {
            get
            {
                if (quest == null || quest.Historical) { return null; }

                return LocalizeString_EndingCore.VV_Inspector_EndingBloom.Translate(_progress.ToStringPercent().Named("PERCENT")).Resolve();
            }
        }

        public override void QuestPartTick()
        {
            base.QuestPartTick();

            if (Find.TickManager.TicksGame % TickInterval != 0) { return; }
            if (anchor == null || anchor.Destroyed || !anchor.Spawned) { return; }

            if (durationTicks <= 0)
            {
                durationTicks = FallbackDurationTicks;
            }

            SetProgress(_progress + LockedProgress * TickInterval / durationTicks);
        }

        public void DebugSetProgress(float value)
        {
            if (State != QuestPartState.Enabled) { return; }

            SetProgress(value);
        }

        private void SetProgress(float value)
        {
            _progress = Mathf.Clamp(value, 0f, LockedProgress);
            if (_progress >= LockedProgress)
            {
                Complete();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref anchor, "anchor");
            Scribe_Values.Look(ref durationTicks, "durationTicks", 0);
            Scribe_Values.Look(ref _progress, "progress", 0f);
        }
    }
}
