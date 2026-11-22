using Verse;

namespace VVRace
{
    public class FairyJob_ApocryphaSwarm : FairyJob
    {
        private int _nextAttackTick;

        public override FairyJobKind Kind => FairyJobKind.Guard;

        public override int ImpactStaggerTicks => Props?.swarmStaggerTicks ?? base.ImpactStaggerTicks;

        private CompApocrypha BossComp => owner?.TryGetComp<CompApocrypha>();

        private CompProperties_Apocrypha Props => BossComp?.Props;

        public FairyJob_ApocryphaSwarm() { }

        public FairyJob_ApocryphaSwarm(Pawn owner) : base(0, owner) { }

        protected override void MakeToils()
        {
            ResetToils(
                new FairyToil_Wait(ViviFairy.MaterializeDurationTicks, FairyState.Materializing, playPhaseEffect: true, enterIdleOnComplete: true),
                new FairyToil_ApocryphaSwarmDrift());
        }

        protected override FairyToil[] MakeReturnToRestToils()
        {
            return new FairyToil[] { new FairyToil_ApocryphaSwarmDrift() };
        }

        protected override void TickActiveBeforeToil(int delta)
        {
            if (fairy == null || !(CurrentToil is FairyToil_ApocryphaSwarmDrift)) { return; }

            var now = GenTicks.TicksGame;
            if (now < _nextAttackTick || !fairy.IsHashIntervalTick(FairyJobUtility.TargetScanIntervalTicks, delta)) { return; }

            var comp = BossComp;
            if (comp == null || comp.Embrace.IsCasting) { return; }

            var props = comp.Props;

            var target = ViviFairyTargeting.FindGuardTargetNear(owner, owner.Position, props.swarmAttackRadius, owner.Position, owner.Position, excludeDowned: true);
            if (TryStartAttackOrReturn(target, null))
            {
                _nextAttackTick = now + props.swarmAttackCooldownTicks.RandomInRange;
            }
            else
            {
                _nextAttackTick = now + Rand.Range(0, props.swarmAttackCooldownTicks.min);
            }
        }

        protected override void OnInterrupted(FairyJobInterruptReason reason)
        {
            if (reason == FairyJobInterruptReason.OwnerUnavailable)
            {
                fairy?.StartJob(new FairyJob_Dematerialize(owner, applyAssimilation: false));
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _nextAttackTick, "nextAttackTick", 0);
        }
    }
}
