using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_LinkEverflower : ThinkNode_JobGiver
    {
        public override float GetPriority(Pawn pawn)
        {
            var compVivi = pawn.GetCompVivi();
            return compVivi != null && compVivi.isRoyal ? 10f : 0f;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            var things = pawn.Map.listerThings.ThingsOfDef(VVThingDefOf.VV_Everflower);
            foreach (var thing in things)
            {
                var everflower = (ArcanePlant_Everflower)thing;
                if (everflower.ReservedPawn == pawn)
                {
                    return JobMaker.MakeJob(VVJobDefOf.VV_LinkEverflower, thing);
                }
            }

            return null;
        }
    }
}
