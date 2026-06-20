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
        public const float ExpansionRadius = 18.6f;

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
            // 요정의 활성 시간이 지나도 끝날때까지 유지되야함
            elapsed += delta;
            if (elapsed >= MaxDurationTicks)
            {
                Interrupt(FairyJobInterruptReason.DurationExpired);
                return;
            }

            var move = CurrentToilAs<FairyToil_MoveToIdleOrbit>();
            if (fairy == null) { return; }
            if (move == null && !(CurrentToil is FairyToil_Attack)) { return; }

            orbitPhase += OrbitSpeed * delta * fairy.OrbitSpeedFactor * fairy.OrbitDirection;
            if (move != null)
            {
                Vector3 center = owner.DrawPos.Yto0();
                float radiusFactor = (fairy.OrbitRadiusXFactor + fairy.OrbitRadiusZFactor) * 0.5f;
                float phaseOffset = fairy.OrbitPhaseOffset;
                float slotAngle = Mathf.PI * 2f / Mathf.Max(1, count);
                float angle = slotAngle * slot + orbitPhase + phaseOffset;
                Vector3 restPos = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * HexRadius * radiusFactor;
                move.ConfigureStepTarget(restPos);
            }

            var target = ViviFairyTargeting.FindHostileNear(
                owner as IAttackTargetSearcher, owner.Position, ExpansionRadius, owner.Position, excludeDowned: false);
            TryStartAttackOrReturn(target, move);
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
