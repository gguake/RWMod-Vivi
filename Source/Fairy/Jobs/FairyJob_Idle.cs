using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class FairyJob_Idle : FairyJob
    {
        private const int SlotRefreshIntervalTicks = 15;

        [Unsaved]
        private int cachedSlot;
        [Unsaved]
        private int cachedCount = 1;
        [Unsaved]
        private bool slotCacheInitialized;

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

            if (!slotCacheInitialized || fairy.IsHashIntervalTick(SlotRefreshIntervalTicks, delta))
            {
                RefreshSlotCache();
            }

            move?.ConfigureStepTarget(FairyJobUtility.IdleOrbitPositionAround(fairy, owner, cachedSlot, cachedCount));
            orbit?.Configure(owner, cachedSlot, cachedCount);

            if (fairy.IsHashIntervalTick(FairyJobUtility.TargetScanIntervalTicks, delta))
            {
                var target = ViviFairyTargeting.FindConcentratedTarget(owner)
                    ?? ViviFairyTargeting.FindGuardTargetNear(
                        owner as IAttackTargetSearcher,
                        owner.Position,
                        ViviFairyTargeting.GuardScanRadius,
                        owner.Position,
                        owner.Position,
                        excludeDowned: true);
                TryStartAttackOrReturn(target, move);
            }
        }

        private void RefreshSlotCache()
        {
            cachedSlot = 0;
            cachedCount = 1;
            slotCacheInitialized = true;

            var ctrl = fairy?.Controller;
            if (ctrl == null) { return; }

            var idleFairies = ctrl.ActiveFairies
                .Where(f => f != null && !f.Destroyed && f.Spawned && f.CurrentJob.Kind == FairyJobKind.Idle)
                .OrderBy(f => f.thingIDNumber)
                .ToList();
            cachedCount = Mathf.Max(1, idleFairies.Count);
            int index = idleFairies.IndexOf(fairy);
            cachedSlot = index >= 0 ? index : 0;
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
