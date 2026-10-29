using RimWorld;
using Verse;

namespace VVRace
{
    // 〈여명의 맥동〉 6단계 개화 응축: 공성 기간(의식 품질에 따라 8~10일) 동안 앵커가 맵에 있을 때만 0→99%로 차오르고,
    // 99%에 이르면 끝난다(완료 신호 → 평화 기간). 남은 1%는 아포크리파를 토벌해야 채워진다.
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

        // 퀘스트 탭의 진행 표시
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

        // DEV: 응축률을 바로 바꾼다(99% 이상이면 곧바로 평화 기간으로).
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
