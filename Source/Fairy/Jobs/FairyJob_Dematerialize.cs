using UnityEngine;
using Verse;

namespace VVRace
{
    public class FairyJob_Dematerialize : FairyJob
    {
        private int ticksLeft = ViviFairy.DematerializeDurationTicks;
        private bool applyAssimilation = true;

        public override FairyJobKind Kind => FairyJobKind.Dematerialize;

        public FairyJob_Dematerialize() { }

        public FairyJob_Dematerialize(Pawn owner, bool applyAssimilation) : base(0, owner)
        {
            this.applyAssimilation = applyAssimilation;
        }

        public FairyJob_Dematerialize(Pawn owner, bool applyAssimilation, int ticksLeft) : base(0, owner)
        {
            this.applyAssimilation = applyAssimilation;
            this.ticksLeft = Mathf.Max(0, ticksLeft);
        }

        protected override bool TryGetInterruptReason(out FairyJobInterruptReason reason)
        {
            reason = FairyJobInterruptReason.None;
            return false;
        }

        protected override void MakeToils()
        {
            ResetToils(new FairyToil_Wait(ticksLeft, FairyState.Dematerializing, playPhaseEffect: true, enterIdleOnComplete: false));
        }

        protected override void OnToilSequenceFinished()
        {
            if (applyAssimilation)
            {
                fairy?.ApplyAssimilationFromJob();
            }
            fairy?.Destroy();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksLeft, "ticksLeft", ViviFairy.DematerializeDurationTicks);
            Scribe_Values.Look(ref applyAssimilation, "applyAssimilation", true);
        }
    }
}
