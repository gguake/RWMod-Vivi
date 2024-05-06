using RimWorld;
using System;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    public class JobGiver_FixDamagedNearPoint : ThinkNode_JobGiver
    {
        public float maxDistFromPoint = -1f;

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            JobGiver_FixDamagedNearPoint obj = (JobGiver_FixDamagedNearPoint)base.DeepCopy(resolve);
            obj.maxDistFromPoint = maxDistFromPoint;
            return obj;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn.WorkTypeIsDisabled(WorkTypeDefOf.Construction) || pawn.WorkTagIsDisabled(WorkTags.Constructing))
            {
                return null;
            }

            var thing = GenClosest.ClosestThingReachable(
                pawn.GetLord().CurLordToil.FlagLoc,
                pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial),
                PathEndMode.Touch,
                TraverseParms.For(pawn),
                maxDistFromPoint,
                building =>
                {
                    if (building.Faction != pawn.Faction) { return false; }
                    if (building.def.building == null || !building.def.building.repairable)
                    {
                        return false;
                    }
                    if (!building.def.useHitPoints || building.HitPoints >= building.MaxHitPoints)
                    {
                        return false;
                    }

                    return pawn.CanReserve(building);
                });

            if (thing != null)
            {
                return JobMaker.MakeJob(JobDefOf.Repair, thing);
            }
            return null;
        }

        public static Predicate<Thing> BuildingValidator(Pawn pawn)
        {
            return delegate (Thing t)
            {
                if (((AttachableThing)t).parent is Pawn)
                {
                    return false;
                }
                if (!pawn.CanReserve(t))
                {
                    return false;
                }

                return (!pawn.WorkTagIsDisabled(WorkTags.Firefighting)) ? true : false;
            };
        }
    }
}
