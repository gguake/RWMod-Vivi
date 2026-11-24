using UnityEngine;
using Verse;

namespace VVRace
{
    public class FairyJob_AsphodelBondOrbit : FairyJob
    {
        private Pawn _target;
        private int _slot;
        private int _count;
        private float _orbitAngle;
        private bool _orbitAngleInitialized;
        private int _nextAttackTick;

        public Pawn Target => _target;
        public int Slot => _slot;
        public int Count => _count;
        internal float OrbitAngle => _orbitAngle;

        public override FairyJobKind Kind => FairyJobKind.Guard;

        public override int ImpactStaggerTicks => Props?.swarmStaggerTicks ?? base.ImpactStaggerTicks;

        public override bool ImpactHarmless => true;

        [Unsaved]
        private CompApocrypha _cachedBossComp;

        private CompApocrypha BossComp
        {
            get
            {
                if (_cachedBossComp == null || _cachedBossComp.parent != owner)
                {
                    _cachedBossComp = owner?.TryGetComp<CompApocrypha>();
                }
                return _cachedBossComp;
            }
        }

        private CompProperties_Apocrypha Props => BossComp?.Props;

        public FairyJob_AsphodelBondOrbit() { }

        public FairyJob_AsphodelBondOrbit(Pawn owner, Pawn target, int slot, int count) : base(0, owner)
        {
            _target = target;
            _slot = slot;
            _count = Mathf.Max(1, count);
            _orbitAngle = _slot * (Mathf.PI * 2f / _count);
            _orbitAngleInitialized = true;

            var cooldown = owner?.TryGetComp<CompApocrypha>()?.Props.swarmAttackCooldownTicks.min ?? 100;
            _nextAttackTick = GenTicks.TicksGame + _slot * cooldown / _count;
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
            ResetToils(new FairyToil_AsphodelBondOrbit(_target, _slot, _count, playPhaseEffect: true, smoothReturn: false));
        }

        protected override FairyToil[] MakeReturnToRestToils()
        {
            return new FairyToil[] { new FairyToil_AsphodelBondOrbit(_target, _slot, _count, playPhaseEffect: false, smoothReturn: true) };
        }

        protected override void TickActiveBeforeToil(int delta)
        {
            if (fairy == null) { return; }

            AdvanceOrbitAngle(delta);

            var comp = BossComp;
            if (comp == null || !(CurrentToil is FairyToil_AsphodelBondOrbit orbitToil) || orbitToil.IsReturning) { return; }

            var now = GenTicks.TicksGame;
            if (now < _nextAttackTick || !fairy.IsHashIntervalTick(FairyJobUtility.TargetScanIntervalTicks, delta)) { return; }

            var props = comp.Props;
            var center = _target.Position;
            var target = ViviFairyTargeting.FindGuardTargetNear(
                owner,
                center,
                props.bondGuardAttackRadius,
                center,
                center,
                excludeDowned: true,
                exclude: _target,
                pawnsOnly: true);
            if (TryStartAttackOrReturn(target, null))
            {
                _nextAttackTick = now + props.swarmAttackCooldownTicks.RandomInRange;
            }
            else
            {
                _nextAttackTick = now + Rand.Range(0, props.swarmAttackCooldownTicks.min);
            }
        }

        private void AdvanceOrbitAngle(int delta)
        {
            if (!_orbitAngleInitialized)
            {
                _orbitAngle = _slot * (Mathf.PI * 2f / Mathf.Max(1, _count));
                _orbitAngleInitialized = true;
            }

            if (delta <= 0 || _target == null) { return; }

            var bond = _target.health?.hediffSet?.GetFirstHediffOfDef(VVHediffDefOf.VV_AsphodelBond) as Hediff_AsphodelBond;
            var progress = bond?.Progress ?? 1f;
            var angularSpeed = Mathf.Lerp(FairyToil_AsphodelBondOrbit.MinAngularSpeed, FairyToil_AsphodelBondOrbit.MaxAngularSpeed, progress);
            _orbitAngle = (_orbitAngle + angularSpeed * delta) % (Mathf.PI * 2f);
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
            Scribe_Values.Look(ref _orbitAngle, "orbitAngle", 0f);
            Scribe_Values.Look(ref _orbitAngleInitialized, "orbitAngleInitialized", false);
            Scribe_Values.Look(ref _nextAttackTick, "nextAttackTick", 0);
        }
    }
}
