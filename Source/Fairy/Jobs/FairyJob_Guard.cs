using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class FairyJob_Guard : FairyJob
    {
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
            ResetToils(new FairyToil_MoveToIdleOrbit());
        }

        protected override bool TryGetInterruptReason(out FairyJobInterruptReason reason)
        {
            if (base.TryGetInterruptReason(out reason))
            {
                return true;
            }
            if (ally == null || !ally.Spawned || ally.Dead)
            {
                reason = FairyJobInterruptReason.TargetUnavailable;
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
            var move = CurrentToilAs<FairyToil_MoveToIdleOrbit>();
            if (orbit == null && move == null && !(CurrentToil is FairyToil_Attack)) { return; }

            orbit?.Configure(ally, 0, 1);

            if (fairy == null) { return; }

            Vector3 restPos = FairyJobUtility.OrbitPositionAround(fairy, ally, 0, 1);
            move?.ConfigureStepTarget(restPos);

            var target = ViviFairyTargeting.FindHostileNear(
                owner as IAttackTargetSearcher,
                ally.Position,
                ViviFairyTargeting.GuardScanRadius,
                ally.Position,
                excludeDowned: true);
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

        protected override void OnEnded()
        {
            if (ally != null)
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
