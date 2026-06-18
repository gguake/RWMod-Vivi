using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class FairyJob_Concentration : FairyJob
    {
        private const int MaxDurationTicks = 2000;
        private const int SpreadWaitTicks = 35;
        private const float ConcRadius = 1.6f;

        private Thing target;
        private int slot;
        private int count = 1;
        private int elapsed;

        public override FairyJobKind Kind => FairyJobKind.Concentration;
        public override FairyRole Role => FairyRole.Concentration;

        public FairyJob_Concentration() { }

        public FairyJob_Concentration(int id, Pawn owner, Thing target, int slot, int count) : base(id, owner)
        {
            this.target = target;
            this.slot = slot;
            this.count = Mathf.Max(1, count);
        }

        protected override void MakeToils()
        {
            var center = target != null ? target.TrueCenter().Yto0() : Vector3.zero;
            float slotAngle = 360f / Mathf.Max(1, count);
            float angle = (90f + slotAngle * slot) * Mathf.Deg2Rad;
            var cell = (center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ConcRadius).ToIntVec3();
            var map = owner != null ? owner.Map : null;
            if (target != null && (map == null || !cell.InBounds(map)))
            {
                cell = target.Position;
            }

            ResetToils(
                new FairyToil_Teleport(cell, target),
                new FairyToil_Wait(Mathf.Max(0, SpreadWaitTicks - ViviFairy.TeleportDurationTicks)),
                new FairyToil_MoveToIdleOrbit());
        }

        protected override bool TryGetInterruptReason(out FairyJobInterruptReason reason)
        {
            if (base.TryGetInterruptReason(out reason))
            {
                return true;
            }

            if (target == null || !target.Spawned || target.Map != owner.Map || (target is Pawn tp && tp.Dead))
            {
                reason = FairyJobInterruptReason.TargetUnavailable;
                return true;
            }

            return false;
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

            Vector3 center = target.TrueCenter().Yto0();
            float slotAngle = 360f / Mathf.Max(1, count);
            float angle = (90f + slotAngle * slot) * Mathf.Deg2Rad;
            Vector3 restPos = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ConcRadius;
            move.ConfigureStepTarget(restPos);

            if (fairy.State != FairyState.Idle && fairy.State != FairyState.Attacking) { return; }
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

        protected override void OnEnded()
        {
            var ctrl = fairy?.Controller;
            if (ctrl != null && ctrl.HasActiveJobInGroup(id, FairyJobKind.Concentration, this))
            {
                return;
            }

            if (target is Pawn p && !p.Dead && p.health != null)
            {
                var hediff = p.health.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_FairyConcentrated);
                if (hediff != null) { p.health.RemoveHediff(hediff); }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref target, "target");
            Scribe_Values.Look(ref slot, "slot");
            Scribe_Values.Look(ref count, "count", 1);
            Scribe_Values.Look(ref elapsed, "elapsed");
        }
    }
}
