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

        public override void Notify_Gathered(Pawn pawn, Thing billGiver, Thing target, RecipeDef_Gathering recipe)
        {
            if (target is Filth filth)
            {
                filth.ThinFilth();
            }
        }
    }
}
