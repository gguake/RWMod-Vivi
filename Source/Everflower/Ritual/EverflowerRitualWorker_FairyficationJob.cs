using RimWorld;
using System;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public class EverflowerRitualWorker_FairyficationJob : EverflowerRitualWorker
    {
        public EverflowerRitualWorker_FairyficationJob(EverflowerRitualDef def) : base(def)
        {
        }

        public override void StartRitual(ArcanePlant_Everflower flower, Pawn caster, Action<EverflowerRitualReservation> onStartCallback)
        {
            var targetParms = TargetingParameters.ForColonist();

            Find.Targeter.BeginTargeting(
                targetParms,
                action: (target) =>
                {
                    onStartCallback(new EverflowerRitualReservation()
                    {
                        ritualDef = _def,
                        casterPawn = caster,
                        targetPawn = target.Pawn,
                    });
                },
                highlightAction: (target) =>
                {
                    var pawn = target.Pawn;
                    if (pawn != null)
                    {
                        GenDraw.DrawTargetHighlight(target);
                    }
                },
                targetValidator: ValidateTarget,
                mouseAttachment: _def.uiIcon,
                onGuiAction: (target) =>
                {
                    var pawn = target.Pawn;
                    if (pawn != null)
                    {
                        var compVivi = pawn.GetCompVivi();
                        if (compVivi == null)
                        {
                            Widgets.MouseAttachedLabel(LocalizeString_Etc.VV_Targetter_InvalidTarget_NotVivi.Translate(_def.LabelCap.Named("RITUAL")));
                            return;
                        }

                        if (compVivi.isRoyal)
                        {
                            Widgets.MouseAttachedLabel(LocalizeString_Etc.VV_Targetter_InvalidTarget_CannotTargetRoyalVivi.Translate(_def.LabelCap.Named("RITUAL")));
                            return;
                        }

                        if (pawn.GetMother() != caster)
                        {
                            Widgets.MouseAttachedLabel(LocalizeString_Etc.VV_Targetter_InvalidTarget_NotMyChild.Translate(_def.LabelCap.Named("RITUAL")));
                            return;
                        }

                        if (pawn.DevelopmentalStage != DevelopmentalStage.Adult)
                        {
                            Widgets.MouseAttachedLabel(LocalizeString_Etc.VV_Targetter_InvalidTarget_NotAdult.Translate(_def.LabelCap.Named("RITUAL")));
                            return;
                        }

                        Widgets.MouseAttachedLabel(LocalizeString_Etc.VV_Targetter_TargetEverflowerRitual.Translate(target.Thing.Named("TARGET"), _def.LabelCap.Named("RITUAL")));
                    }
                });

            bool ValidateTarget(LocalTargetInfo target)
            {
                var pawn = target.Pawn;
                if (pawn == null) { return false; }
                if (pawn.TryGetComp<CompVivi>(out var compVivi) && compVivi.isRoyal) { return false; }
                if (pawn.GetMother() != caster) { return false; }
                if (pawn.DevelopmentalStage != DevelopmentalStage.Adult) { return false; }

                return true;
            }
        }

        public override Job TryGiveJob(ArcanePlant_Everflower flower)
        {
            if (flower.CurReservedRitual.Worker != this) { return null; }

            if (!flower.CurReservationInfo.casterPawn.CanReserveAndReach(flower, PathEndMode.Touch, Danger.Deadly)) { return null; }
            if (!flower.CurReservationInfo.targetPawn.CanReserveAndReach(flower, PathEndMode.Touch, Danger.Deadly)) { return null; }

            var job = JobMaker.MakeJob(_def.job, flower, flower.CurReservationInfo.targetPawn);
            job.count = 1;

            return job;
        }
    }
}
