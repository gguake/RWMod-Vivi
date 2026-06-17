using UnityEngine;
using Verse;

namespace VVRace
{
    public class FairyJob_Materialize : FairyJob
    {
        private int ticksLeft = ViviFairy.MaterializeDurationTicks;

        public override FairyJobKind Kind => FairyJobKind.Materialize;

        public FairyJob_Materialize() { }

        public FairyJob_Materialize(Pawn owner) : base(0, owner) { }

        public FairyJob_Materialize(Pawn owner, int ticksLeft) : base(0, owner)
        {
            this.ticksLeft = Mathf.Max(0, ticksLeft);
        }

        protected override void MakeToils()
        {
            ResetToils(new FairyToil_Wait(ticksLeft, FairyState.Materializing, playPhaseEffect: true, enterIdleOnComplete: true));
        }

        protected override void OnToilSequenceFinished()
        {
            fairy?.StartJob(new FairyJob_Idle(owner));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksLeft, "ticksLeft", ViviFairy.MaterializeDurationTicks);
        }
    }
}
