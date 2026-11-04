using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    // NPC는 바닐라 옷 고르기를 하지 않으므로, 둥지가 만든 기본옷을 입고 새총잡이는 새총을 든다.
    public class JobGiver_ViviNestDress : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled) { return null; }

            if (ViviNestProduction.NeedsCostume(pawn))
            {
                var costume = FindOwned(pawn, nest, VVThingDefOf.VV_BasicWorkerCostume);
                if (costume != null)
                {
                    return JobMaker.MakeJob(JobDefOf.Wear, costume);
                }
            }

            if (ViviNestProduction.NeedsSlingshot(pawn))
            {
                var slingshot = FindOwned(pawn, nest, VVThingDefOf.VV_Slingshot);
                if (slingshot != null)
                {
                    return JobMaker.MakeJob(JobDefOf.Equip, slingshot);
                }
            }

            return null;
        }

        private static Thing FindOwned(Pawn pawn, LordJob_ViviNest nest, ThingDef def)
        {
            var things = pawn.Map.listerThings.ThingsOfDef(def);
            for (int i = 0; i < things.Count; ++i)
            {
                var thing = things[i];
                if (nest.Stock.IsNestOwned(thing) && pawn.CanReserveAndReach(thing, PathEndMode.ClosestTouch, Danger.Some))
                {
                    return thing;
                }
            }
            return null;
        }
    }
}
