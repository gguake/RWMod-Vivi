using RimWorld;
using System;
using Verse;
using Verse.AI;

namespace VVRace
{
    [StaticConstructorOnStartup]
    public class EverflowerRitualWorker_MoveEverflower : EverflowerRitualWorker
    {
        public const float RadiusRange = 13.4f;

        public EverflowerRitualWorker_MoveEverflower(EverflowerRitualDef def) : base(def)
        {
        }

        public override AcceptanceReport CanRitual(ArcanePlant_Everflower everflower, Pawn caster)
        {
            if (everflower.CurReservationInfo != null && everflower.CurReservationInfo.ritualDef == _def)
            {
                if (!ArcanePlantUtility.CanPlaceArcanePlantToCell(everflower.Map, everflower.CurReservationInfo.targetCell, VVThingDefOf.VV_Everflower))
                {
                    return false;
                }
            }

            return base.CanRitual(everflower, caster);
        }

        public override void StartRitual(ArcanePlant_Everflower flower, Pawn caster, Action<EverflowerRitualReservation> onStartCallback)
        {
            var targetParms = TargetingParameters.ForCell();

            Find.Targeter.BeginTargeting(
                targetParms,
                action: (target) =>
                {
                    onStartCallback(new EverflowerRitualReservation(flower)
                    {
                        ritualDef = _def,
                        casterPawn = caster,
                        targetCell = target.Cell,
                    });
                },
                highlightAction: (target) => 
                {
                    var cell = target.Cell;
                    if (ValidateTarget(target))
                    {
                        GenDraw.DrawTargetHighlight(target);
                    }
                },
                targetValidator: ValidateTarget,
                mouseAttachment: _def.uiIcon,
                onUpdateAction: (target) =>
                {
                    GenDraw.DrawRadiusRing(flower.Position, RadiusRange);
                });

            bool ValidateTarget(LocalTargetInfo target)
            {
                var cell = target.Cell;
                if (cell.DistanceTo(flower.Position) > RadiusRange)
                {
                    return false;
                }

                if (!ArcanePlantUtility.CanPlaceArcanePlantToCell(flower.Map, cell, VVThingDefOf.VV_Everflower))
                {
                    return false;
                }

                return true;
            }
        }

        public override Job TryGiveJob(EverflowerRitualReservation reservation)
        {
            if (reservation.ritualDef.Worker != this) { return null; }

            if (!reservation.casterPawn.CanReserveAndReach(reservation.flower, PathEndMode.Touch, Danger.Deadly)) { return null; }

            var job = JobMaker.MakeJob(_def.job, reservation.flower, reservation.targetPawn);
            job.count = 1;

            return job;
        }

        public override void Complete(EverflowerRitualReservation reservation)
        {
            var previousPosition = reservation.flower.Position;
            reservation.flower.Position = reservation.targetCell;

            EffecterDef effecterDef = null;
            switch (reservation.flower.EverflowerComp.AttunementLevel)
            {
                case 1:
                    effecterDef = VVEffecterDefOf.VV_EverflowerGrow_1_Level;
                    break;
                case 2:
                    effecterDef = VVEffecterDefOf.VV_EverflowerGrow_2_Level;
                    break;
                case 3:
                    effecterDef = VVEffecterDefOf.VV_EverflowerGrow_3_Level;
                    break;
            }

            if (effecterDef != null)
            {
                effecterDef.Spawn(previousPosition, reservation.flower.Map);
                effecterDef.SpawnAttached(reservation.flower, reservation.flower.Map);
            }

            base.Complete(reservation);
        }
    }
}
