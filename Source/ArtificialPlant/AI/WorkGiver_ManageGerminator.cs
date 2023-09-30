using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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
                    if (def.germinateJob == null)
                    {
                        return false;
                    }

                    if (def.ingredients != null && def.ingredients.Count > 0)
                    {
                        ingredient = TryFindManageIngredients(pawn, def);
                        return ingredient != null;
                    }
                    else
                    {
                        return true;
                    }

                case GerminateStage.GerminateComplete:
                    return germinator.ProductThingDef == null || germinator.CanWithdrawProduct;

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
                            var job = JobMaker.MakeJob(VVJobDefOf.VV_HaulGerminatingIngredient, germinator, ingredient);
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
                            ingredient = TryFindManageIngredients(pawn, def);
                            if (ingredient != null)
                            {
                                var job = JobMaker.MakeJob(def.germinateJob, germinator, ingredient);
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

                    case GerminateStage.GerminateComplete:
                        {
                            if (germinator.ProductThingDef != null)
                            {
                                return germinator.CanWithdrawProduct ? JobMaker.MakeJob(VVJobDefOf.VV_PackingSeedling, germinator) : null;
                            }
                            else
                            {
                                return JobMaker.MakeJob(VVJobDefOf.VV_ClearGerminator, germinator);
                            }
                        }
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
                    thing => !thing.IsForbidden(pawn) && pawn.CanReserve(thing));

                if (target != null)
                {
                    return target;
                }
            }

            return null;
        }

        private Thing TryFindManageIngredients(Pawn pawn, GerminateScheduleDef def)
        {
            var ingredientThingDef = def.ingredients[0].thingDef;

            var target = GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForDef(ingredientThingDef),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                9999f,
                thing => !thing.IsForbidden(pawn) && pawn.CanReserve(thing));

            if (target != null)
            {
                return target;
            }

            return null;
        }
    }
}
