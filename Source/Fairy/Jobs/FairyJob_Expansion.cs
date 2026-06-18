using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class FairyJob_Expansion : FairyJob
    {
        private const int MaxDurationTicks = 10000;
        private const float HexRadius = 2.2f;
        private const float OrbitSpeed = 0.01f;
        private const float ExpansionRadius = 14f;

        private int slot;
        private int count = 1;
        private int elapsed;
        private float orbitPhase;

        public override FairyJobKind Kind => FairyJobKind.Expansion;
        public override FairyRole Role => FairyRole.Expansion;

        public FairyJob_Expansion() { }

        public FairyJob_Expansion(int id, Pawn owner, int slot, int count) : base(id, owner)
        {
            this.slot = slot;
            this.count = Mathf.Max(1, count);
        }

        protected override void MakeToils()
        {
            ResetToils(new FairyToil_MoveToIdleOrbit());
        }

        protected override void TickActiveBeforeToil(int delta)
        {
            elapsed += delta;
            if (elapsed >= MaxDurationTicks)
            {
                Interrupt(FairyJobInterruptReason.DurationExpired);
                return;
            }

            var move = CurrentToilAs<FairyToil_MoveToIdleOrbit>();
            if (move == null || fairy == null) { return; }

            orbitPhase += OrbitSpeed * delta * fairy.OrbitSpeedFactor * fairy.OrbitDirection;
            Vector3 center = owner.DrawPos.Yto0();
            float radiusFactor = (fairy.OrbitRadiusXFactor + fairy.OrbitRadiusZFactor) * 0.5f;
            float phaseOffset = fairy.OrbitPhaseOffset;
            float slotAngle = Mathf.PI * 2f / Mathf.Max(1, count);
            float angle = slotAngle * slot + orbitPhase + phaseOffset;
            Vector3 restPos = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * HexRadius * radiusFactor;
            move.ConfigureStepTarget(restPos);

            if (fairy.State != FairyState.Idle && fairy.State != FairyState.Attacking) { return; }

            var target = ViviFairyTargeting.FindHostileNear(
                owner as IAttackTargetSearcher, owner.Position, ExpansionRadius, owner.Position, excludeDowned: false);

            if (target == null)
            {
                if (fairy.State == FairyState.Attacking)
                {
                    fairy.EnterIdle();
                }
                return;
            }
            if (fairy.State != FairyState.Attacking && !move.IsNearStepTarget(0.35f)) { return; }

            ResetToils(
                new FairyToil_Attack(target),
                new FairyToil_MoveToIdleOrbit());
        }

        protected override void OnInterrupted(FairyJobInterruptReason reason)
        {
            if (reason == FairyJobInterruptReason.OwnerUnavailable)
            {
                fairy?.StartJob(new FairyJob_Dematerialize(owner, applyAssimilation: false));
                return;
            }
            if (reason == FairyJobInterruptReason.LifespanExpired)
            {
                fairy?.StartJob(new FairyJob_Dematerialize(owner, applyAssimilation: true));
                return;
            }

            fairy?.StartJob(new FairyJob_Idle(owner));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref slot, "slot");
            Scribe_Values.Look(ref count, "count", 1);
            Scribe_Values.Look(ref elapsed, "elapsed");
            Scribe_Values.Look(ref orbitPhase, "orbitPhase");
        }
    }
}
