using Verse;

namespace VVRace
{
    public class FairyJob_Materialize : FairyJob
    {
        public override FairyJobKind Kind => FairyJobKind.Materialize;

        public FairyJob_Materialize() { }

        public FairyJob_Materialize(Pawn owner) : base(0, owner) { }

        protected override void MakeToils()
        {
            ResetToils(new FairyToil_Wait(ViviFairy.MaterializeDurationTicks, FairyState.Materializing, playPhaseEffect: true, enterIdleOnComplete: true));
        }

        protected override void OnToilSequenceFinished()
        {
            fairy?.StartJob(new FairyJob_Idle(owner));
        }
    }
}
