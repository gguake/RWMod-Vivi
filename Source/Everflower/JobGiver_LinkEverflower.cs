using RimWorld;
using System.Linq;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_LinkEverflower : ThinkNode_JobGiver
    {
        public override float GetPriority(Pawn pawn)
        {
            return pawn.CanLinkEverflower() ? 10f : 0f;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!pawn.CanLinkEverflower()) { return null; }

            var things = pawn.Map.listerThings.ThingsOfDef(VVThingDefOf.VV_Everflower);
            foreach (var thing in things.Where(v => v.Spawned && v.Faction == pawn.Faction))
            {
                var everflower = (ArcanePlant_Everflower)thing;
                if (everflower.ReservedPawn == pawn)
                {
                    if (everflower.IsBurning() || everflower.IsForbidden(pawn)) { continue; }

                    if (!pawn.CanReserveAndReach(everflower, PathEndMode.Touch, Danger.Deadly)) { continue; }

                    return JobMaker.MakeJob(VVJobDefOf.VV_LinkEverflower, thing);
                }
            }

            return null;
        }
    }
}
