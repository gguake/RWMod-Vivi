using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class GatherWorker_Pollen : GatherWorker
    {
        public override string JobFailReasonIfNoHarvestable => LocalizeTexts.JobFailReasonNoHarvestablePollenFilths.Translate();

        public override bool PawnCanDoBill(Pawn pawn, Bill bill) => pawn.GetStatValue(bill.recipe.workSpeedStat) > 0f;

        public override IEnumerable<Thing> FindAllGatherableTargetInRegion(Region region)
        {
            foreach (var thing in region.ListerThings.ThingsOfDef(VVThingDefOf.VV_FilthPollen))
            {
                yield return thing;
            }
        }

        public override Thing FilterGatherableTarget(Pawn pawn, Thing billGiver, Bill bill, IEnumerable<Thing> candidates)
        {
            foreach (var thing in candidates)
            {
                // Bill에 허용되지 않는 경우
                if (!bill.IsFixedOrAllowedIngredient(thing)) { continue; }

                // 상호작용이 불가능한 경우
                if (thing.IsForbidden(pawn) || thing.IsBurning()) { continue; }

                // 접근 불가능한 경우
                if (!pawn.CanReserveAndReach(thing, PathEndMode.Touch, recipeDef.maxPathDanger)) { continue; }

                return thing;
            }

            return null;
        }

        [Obsolete]
        public override IEnumerable<Thing> FindAllGatherableTargetInRegion(Pawn pawn, Region region, Thing billGiver, Bill bill)
        {
            var allFilths = region.ListerThings.ThingsInGroup(ThingRequestGroup.Filth);
            if (allFilths.NullOrEmpty()) { yield break; }

            foreach (var thing in allFilths.Where(filth => filth.def == VVThingDefOf.VV_FilthPollen))
            {
                // 거리가 너무 멀 경우
                if (!billGiver.InGatherableRange(thing)) { continue; }

                // Bill에 허용되지 않는 경우
                if (!bill.IsFixedOrAllowedIngredient(thing) || !bill.recipe.ingredients.Any(v => v.filter.Allows(thing))) { continue; }

                // 접근 불가능한 경우
                if (!ReachabilityWithinRegion.ThingFromRegionListerReachable(thing, region, PathEndMode.Touch, pawn)) { continue; }

                // 예약이 불가능한 경우
                if (!pawn.CanReserve(thing)) { continue; }

                yield return thing;
            }

            yield break;
        }

        public override void Notify_Gathered(Pawn pawn, Thing billGiver, Thing target, RecipeDef_Gathering recipe)
        {
            if (target is Filth filth)
            {
                filth.ThinFilth();
            }
        }
    }
}
