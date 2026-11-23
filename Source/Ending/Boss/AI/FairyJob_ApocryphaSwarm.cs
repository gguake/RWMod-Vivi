using Verse;

namespace VVRace
{
    public class FairyJob_ApocryphaSwarm : FairyJob
    {
        private int _nextAttackTick;
        private Thing _anchor;

        public FairySigilWard Ward => _anchor as FairySigilWard;

        public override FairyJobKind Kind => FairyJobKind.Guard;

        public override int ImpactStaggerTicks => Props?.swarmStaggerTicks ?? base.ImpactStaggerTicks;

        public override bool ImpactHarmless => true;

        private CompApocrypha BossComp => owner?.TryGetComp<CompApocrypha>();

        private CompProperties_Apocrypha Props => BossComp?.Props;

        public FairyJob_ApocryphaSwarm() { }

        public FairyJob_ApocryphaSwarm(Pawn owner, Thing anchor) : base(0, owner)
        {
            _anchor = anchor;
        }

        protected override bool TryGetInterruptReason(out FairyJobInterruptReason reason)
        {
            if (base.TryGetInterruptReason(out reason)) { return true; }

            if (_anchor == null || !_anchor.Spawned || _anchor.Map != fairy.Map)
            {
                reason = FairyJobInterruptReason.TargetUnavailable;
                return true;
            }

            return false;
        }

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
            if (fairy == null) { return; }

            var comp = BossComp;
            if (comp != null && Ward == null && comp.Embrace.IsCasting)
            {
                if (CurrentToil is FairyToil_Attack)
                {
                    ResetToils(MakeReturnToRestToils());
                }
                return;
            }

            if (comp == null || !(CurrentToil is FairyToil_ApocryphaSwarmDrift)) { return; }

            var now = GenTicks.TicksGame;
            if (now < _nextAttackTick || !fairy.IsHashIntervalTick(FairyJobUtility.TargetScanIntervalTicks, delta)) { return; }

            var props = comp.Props;

            var center = _anchor.Position;
            var radius = Ward != null ? props.sigilWardAttackRadius : props.swarmAttackRadius;
            var target = ViviFairyTargeting.FindGuardTargetNear(owner, center, radius, center, center, excludeDowned: true, pawnsOnly: true);
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
            if (reason == FairyJobInterruptReason.OwnerUnavailable || reason == FairyJobInterruptReason.TargetUnavailable)
            {
                fairy?.StartJob(new FairyJob_Dematerialize(owner, applyAssimilation: false));
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _nextAttackTick, "nextAttackTick", 0);
            Scribe_References.Look(ref _anchor, "anchor");
        }
    }
}
