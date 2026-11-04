using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class Hediff_FairyGuarded : HediffWithComps
    {
        private const int JobCheckIntervalTicks = 60;

        public Pawn ownerVivi;
        public int jobId;

        [Unsaved]
        private bool _jobLost;

        public override bool ShouldRemove => _jobLost || ownerVivi == null || ownerVivi.Dead || !ownerVivi.Spawned;

        private bool HasGuardJob
        {
            get
            {
                var ctrl = ownerVivi?.GetComp<CompViviHolder>();
                return ctrl != null && ctrl.HasActiveJob(jobId);
            }
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);

            // 호위 요정이 작업을 마치지 못하고 사라지면 작업의 OnEnded가 이 헤디프를 지우지 못하므로 주기적으로 확인한다.
            if (pawn.IsHashIntervalTick(JobCheckIntervalTicks, delta) && !HasGuardJob)
            {
                _jobLost = true;
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (pawn == null || pawn.Faction != Faction.OfPlayer || ownerVivi == null)
            {
                yield break;
            }

            yield return new Command_Action
            {
                defaultLabel = LocalizeString_Etc.VV_Command_ReleaseGuard.Translate(),
                defaultDesc = LocalizeString_Etc.VV_Command_ReleaseGuardDesc.Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/VV_FairyReleaseGuard", reportFailure: false),
                action = () =>
                {
                    if (HasGuardJob)
                    {
                        ownerVivi.GetComp<CompViviHolder>().InterruptJob(jobId, FairyJobInterruptReason.ExternalCancel);
                    }
                    else if (pawn?.health != null)
                    {
                        pawn.health.RemoveHediff(this);
                    }
                }
            };
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref ownerVivi, "ownerVivi");
            Scribe_Values.Look(ref jobId, "jobId");
        }
    }
}
