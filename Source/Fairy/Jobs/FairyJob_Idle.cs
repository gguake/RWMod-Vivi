using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class FairyJob_Idle : FairyJob
    {
        public override FairyJobKind Kind => FairyJobKind.Idle;
        protected override bool RebuildLegacyMoveOnlyToils => true;

        public FairyJob_Idle() { }

        public FairyJob_Idle(Pawn owner) : base(0, owner) { }

        protected override void MakeToils()
        {
            ResetToils(MakeReturnToRestToils());
        }

        protected override FairyToil[] MakeReturnToRestToils()
        {
            return MakeMoveThenIdleOrbitToils();
        }

        protected override void TickActiveBeforeToil(int delta)
        {
            var move = CurrentToilAs<FairyToil_MoveToIdleOrbit>();
            var orbit = CurrentToilAs<FairyToil_IdleOrbit>();
            if (fairy == null) { return; }
            if (move == null && orbit == null && !(CurrentToil is FairyToil_Attack)) { return; }

            int slot = 0;
            int count = 1;
            var ctrl = fairy?.Controller;
            if (ctrl != null)
            {
                var idleFairies = ctrl.ActiveFairies
                    .Where(f => f != null && !f.Destroyed && f.Spawned && f.CurrentJob.Kind == FairyJobKind.Idle)
                    .OrderBy(f => f.thingIDNumber)
                    .ToList();
                count = Mathf.Max(1, idleFairies.Count);
                int index = idleFairies.IndexOf(fairy);
                slot = index >= 0 ? index : 0;
            }

            move?.ConfigureStepTarget(FairyJobUtility.IdleOrbitPositionAround(fairy, owner, slot, count));
            orbit?.Configure(owner, slot, count);

            var target = ViviFairyTargeting.FindConcentratedTarget(owner)
                ?? ViviFairyTargeting.FindHostileNear(
                    owner as IAttackTargetSearcher,
                    owner.Position,
                    ViviFairyTargeting.GuardScanRadius,
                    owner.Position,
                    excludeDowned: true);
            TryStartAttackOrReturn(target, move);
        }

        protected override void OnInterrupted(FairyJobInterruptReason reason)
        {
            if (reason == FairyJobInterruptReason.OwnerUnavailable)
            {
                fairy?.StartJob(new FairyJob_Dematerialize(owner, applyAssimilation: false));
            }
        }
    }
}
