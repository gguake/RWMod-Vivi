using System.Linq;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class FairyJob_Idle : FairyJob
    {
        public override FairyJobKind Kind => FairyJobKind.Idle;

        public FairyJob_Idle() { }

        public FairyJob_Idle(Pawn owner) : base(0, owner) { }

        public override void Notify_AssignedToFairy(ViviFairy fairy)
        {
            base.Notify_AssignedToFairy(fairy);
            if (toils != null && toils.Count == 1 && toils[0] is FairyToil_MoveToIdleOrbit)
            {
                MakeToils();
            }
        }

        protected override void MakeToils()
        {
            ResetToils(
                new FairyToil_MoveToIdleOrbit(completeWhenNear: true),
                new FairyToil_IdleOrbit());
        }

        protected override void TickActiveBeforeToil(int delta)
        {
            var move = CurrentToilAs<FairyToil_MoveToIdleOrbit>();
            var orbit = CurrentToilAs<FairyToil_IdleOrbit>();
            if ((move == null && orbit == null) || fairy == null) { return; }

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
