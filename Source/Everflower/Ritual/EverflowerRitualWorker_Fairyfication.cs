using RimWorld;
using System;
using Verse;
using Verse.AI;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public class EverflowerRitualWorker_Fairyfication : EverflowerRitualWorker
    {
        public EverflowerRitualWorker_Fairyfication(EverflowerRitualDef def) : base(def)
        {
        }

        public override AcceptanceReport CanRitual(ArcanePlant_Everflower everflower, Pawn caster)
        {
            if (!caster.TryGetComp<CompViviHolder>(out var compViviHolder) || !compViviHolder.CanJoin)
            {
                return LocalizeString_Etc.VV_FailReason_FairyViviFull.Translate();
            }

            return base.CanRitual(everflower, caster);
        }

        public override void StartRitual(ArcanePlant_Everflower flower, Pawn caster, Action<EverflowerRitualReservation> onStartCallback)
        {
            var targetParms = TargetingParameters.ForColonist();

            Find.Targeter.BeginTargeting(
                targetParms,
                action: (target) =>
                {
                    onStartCallback(new EverflowerRitualReservation(flower)
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

                        if (pawn.health.hediffSet.HasHediff(VVHediffDefOf.VV_FairyficationSickness))
                        {
                            Widgets.MouseAttachedLabel(LocalizeString_Etc.VV_Targetter_InvalidTarget_FairyficationSickness.Translate(_def.LabelCap.Named("RITUAL")));
                            return;
                        }

                        Widgets.MouseAttachedLabel(LocalizeString_Etc.VV_Targetter_TargetEverflowerRitual.Translate(target.Thing.Named("TARGET"), _def.LabelCap.Named("RITUAL")));
                    }
                });

            bool ValidateTarget(LocalTargetInfo target)
            {
                var pawn = target.Pawn;
                if (pawn == null) { return false; }
                if (!pawn.TryGetComp<CompVivi>(out var compVivi) || !compVivi.isRoyal) { return false; }
                if (pawn.GetMother() != caster) { return false; }
                if (pawn.DevelopmentalStage != DevelopmentalStage.Adult) { return false; }
                if (pawn.health.hediffSet.HasHediff(VVHediffDefOf.VV_FairyficationSickness)) { return false; }

                return true;
            }
        }

        public override Job TryGiveJob(EverflowerRitualReservation reservation)
        {
            if (reservation.ritualDef.Worker != this) { return null; }

            if (!reservation.casterPawn.CanReserveAndReach(reservation.flower, PathEndMode.Touch, Danger.Deadly)) { return null; }
            if (!reservation.targetPawn.CanReserveAndReach(reservation.flower, PathEndMode.Touch, Danger.Deadly)) { return null; }

            var job = JobMaker.MakeJob(_def.job, reservation.flower, reservation.targetPawn);
            job.count = 1;

            return job;
        }

        public override void Complete(EverflowerRitualReservation reservation)
        {
            if (reservation.casterPawn.TryGetComp<CompViviHolder>(out var compViviHolder))
            {
                compViviHolder.JoinVivi(reservation.targetPawn);
            }

            base.Complete(reservation);
        }
    }
}
