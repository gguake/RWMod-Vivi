using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class FairyJob_Guard : FairyJob
    {
        private const float GuardScanRadius = 4.9f;

        private Pawn ally;

        public override FairyJobKind Kind => FairyJobKind.Guard;
        public override FairyRole Role => FairyRole.Guard;

        public FairyJob_Guard() { }

        public FairyJob_Guard(int id, Pawn owner, Pawn ally) : base(id, owner)
        {
            this.ally = ally;
        }

        protected override void MakeToils()
        {
            ResetToils(new FairyToil_IdleOrbit(ally, 0, 1));
        }

        protected override bool TryGetInterruptReason(out FairyJobInterruptReason reason)
        {
            if (ally == null || !ally.Spawned || ally.Dead)
            {
                reason = FairyJobInterruptReason.TargetUnavailable;
                return true;
            }
            if (base.TryGetInterruptReason(out reason))
            {
                return true;
            }
            if (fairy != null && fairy.LifespanExpired)
            {
                reason = FairyJobInterruptReason.LifespanExpired;
                return true;
            }

            return false;
        }

        protected override void TickActiveBeforeToil(int delta)
        {
            var orbit = CurrentToilAs<FairyToil_IdleOrbit>();
            if (orbit == null) { return; }

            orbit.Configure(ally, 0, 1);

            if (fairy == null || fairy.State != FairyState.Idle) { return; }

            var target = ViviFairyTargeting.FindHostileNear(
                owner as IAttackTargetSearcher, ally.Position, GuardScanRadius, ally.Position, excludeDowned: true);

            if (target == null) { return; }

            Vector3 restPos = FairyJobUtility.OrbitPositionAround(fairy, ally, 0, 1);
            ResetToils(
                new FairyToil_Attack(target),
                new FairyToil_MoveToIdleOrbit(restPos),
                new FairyToil_IdleOrbit(ally, 0, 1));
        }

        protected override void OnInterrupted(FairyJobInterruptReason reason)
        {
            if (reason == FairyJobInterruptReason.LifespanExpired)
            {
                fairy?.StartJob(new FairyJob_Dematerialize(owner, applyAssimilation: true));
                return;
            }
            if (reason == FairyJobInterruptReason.OwnerUnavailable)
            {
                fairy?.StartJob(new FairyJob_Dematerialize(owner, applyAssimilation: false));
                return;
            }

            StartIdleJob();
        }

        protected override void OnEnded()
        {
            if (ally != null && !ally.Dead && ally.health != null)
            {
                var hediff = ally.health.hediffSet.GetFirstHediffOfDef(VVHediffDefOf.VV_FairyGuarded);
                if (hediff != null) { ally.health.RemoveHediff(hediff); }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref ally, "ally");
        }
    }
}
