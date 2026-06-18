using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class Hediff_FairyGuarded : HediffWithComps
    {
        public Pawn ownerVivi;
        public int jobId;

        public override bool ShouldRemove => ownerVivi == null || ownerVivi.Dead || !ownerVivi.Spawned;

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
                    var ctrl = ownerVivi != null ? ownerVivi.GetComp<CompViviHolder>() : null;
                    if (ctrl != null)
                    {
                        ctrl.InterruptJob(jobId, FairyJobInterruptReason.ExternalCancel);
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
