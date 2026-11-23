using Verse;

namespace VVRace
{
    public class FairyJob_AsphodelBondOrbit : FairyJob
    {
        private Pawn _target;
        private int _slot;
        private int _count;

        public Pawn Target => _target;
        public int Slot => _slot;
        public int Count => _count;

        public override FairyJobKind Kind => FairyJobKind.Idle;

        public FairyJob_AsphodelBondOrbit() { }

        public FairyJob_AsphodelBondOrbit(Pawn owner, Pawn target, int slot, int count) : base(0, owner)
        {
            _target = target;
            _slot = slot;
            _count = count;
        }

        protected override bool TryGetInterruptReason(out FairyJobInterruptReason reason)
        {
            if (base.TryGetInterruptReason(out reason)) { return true; }

            if (_target == null
                || _target.Destroyed
                || !_target.Spawned
                || _target.Dead
                || _target.Map != fairy.Map
                || _target.health?.hediffSet?.HasHediff(VVHediffDefOf.VV_AsphodelBond) != true)
            {
                reason = FairyJobInterruptReason.TargetUnavailable;
                return true;
            }

            return false;
        }

        protected override void MakeToils()
        {
            ResetToils(new FairyToil_AsphodelBondOrbit(_target, _slot, _count));
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
            Scribe_References.Look(ref _target, "target");
            Scribe_Values.Look(ref _slot, "slot", 0);
            Scribe_Values.Look(ref _count, "count", 1);
        }
    }
}
