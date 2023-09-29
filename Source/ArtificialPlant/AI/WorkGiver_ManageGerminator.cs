using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class WorkGiver_ManageGerminator : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(VVThingDefOf.VV_SeedlingGerminator);

        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            var germinator = t as Building_SeedlingGerminator;
            if (germinator == null || t.def != VVThingDefOf.VV_SeedlingGerminator)
            {
                return false;
            }

            if (germinator.CurrentSchedule == null)
            {
                return false;
            }

            if (germinator.IsForbidden(pawn))
            {
                return false;
            }

            if (!pawn.CanReserve(germinator, ignoreOtherReservations: forced)) 
            {
                return false;
            }

            switch (germinator.CurrentSchedule.Stage)
            {
                case GerminateStage.None:
                    var ingredient = FindGerminateIngredient(pawn, germinator);
                    return ingredient != null;

                case GerminateStage.GerminateInProgress:
                    if (!germinator.CurrentSchedule.CanManageJob)
                    {
                        return false;
                    }

                    var def = germinator.CurrentSchedule.CurrentManageScheduleDef;
                    if (def.ingredients?.Count > 0 && !TryFindManageIngredients(pawn, def, out _))
                    {
                        return false;
                    }

                    return true;

                case GerminateStage.GerminateCompleted:
                default:
                    return false;
            }
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            var germinator = (Building_SeedlingGerminator)t;
            if (germinator.CurrentSchedule != null)
            {
                switch (germinator.CurrentSchedule.Stage)
                {
                    case GerminateStage.None:
                        var ingredient = FindGerminateIngredient(pawn, germinator);
                        if (ingredient != null)
                        {
                            var job = JobMaker.MakeJob(VVJobDefOf.VV_HaulGerminatingIngredient, ingredient, germinator);
                            return job;
                        }
                        else
                        {
                            return null;
                        }

                    case GerminateStage.GerminateInProgress:
                        var def = germinator.CurrentSchedule.CurrentManageScheduleDef;
                        if (def.ingredients?.Count > 0)
                        {
                            if (TryFindManageIngredients(pawn, def, out var ingredients))
                            {
                                var job = JobMaker.MakeJob(def.germinateJob, germinator);
                                job.targetQueueB = ingredients.Select(v => new LocalTargetInfo(v.Item1)).ToList();
                                job.countQueue = ingredients.Select(v => v.Item2).ToList();
                                job.haulMode = HaulMode.ToCellNonStorage;
                                return job;
                            }
                            else
                            {
                                return null;
                            }
                        }
                        else
                        {
                            var job = JobMaker.MakeJob(def.germinateJob, germinator);
                            return job;
                        }

                    case GerminateStage.GerminateCompleted:
                        break;
                }
            }

            return null;
        }

        private Thing FindGerminateIngredient(Pawn pawn, Building_SeedlingGerminator germinator)
        {
            foreach (var tuple in germinator.RequiredGerminateIngredients)
            {
                var target = GenClosest.ClosestThingReachable(
                    pawn.Position,
                    pawn.Map,
                    ThingRequest.ForDef(tuple.def),
                    PathEndMode.ClosestTouch,
                    TraverseParms.For(pawn),
                    9999f,
                    thing => !thing.IsForbidden(pawn) && pawn.CanReserve(thing) && thing.def == tuple.def);

                if (target != null)
                {
                    return target;
                }
            }

            return null;
        }

        private bool TryFindManageIngredients(Pawn pawn, GerminateScheduleDef def, out List<(Thing, int)> ingredients)
        {
            ingredients = null;
            return false;
        }
    }
}
