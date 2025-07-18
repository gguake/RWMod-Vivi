using System.Linq;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_TryEverflowerRitual : ThinkNode_JobGiver
    {
        public override float GetPriority(Pawn pawn)
        {
            var compVivi = pawn.GetCompVivi();
            return compVivi != null && compVivi.isRoyal && compVivi.LinkedEverflower?.CurReservedPawn == pawn ? 10 : 0;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            var compVivi = pawn.GetCompVivi();
            if (compVivi == null || !compVivi.isRoyal) { return null; }

            var targetEverflower = compVivi.LinkedEverflower;
            if (targetEverflower == null)
            {
                var things = pawn.Map.listerThings.ThingsOfDef(VVThingDefOf.VV_Everflower);
                foreach (var thing in things.Where(v => v.Spawned && v.Faction == pawn.Faction))
                {
                    var everflower = (ArcanePlant_Everflower)thing;
                    if (everflower.CurReservedPawn == pawn)
                    {
                        targetEverflower = everflower;
                        break;
                    }
                }
            }

            if (targetEverflower == null || targetEverflower.CurReservationInfo == null || targetEverflower.CurReservedPawn != pawn) { return null; }
            if (!targetEverflower.CurReservationInfo.ritualDef.Worker.CanRitual(targetEverflower, pawn)) { return null; }

            var job = targetEverflower.CurReservedRitual.Worker.TryGiveJob(targetEverflower.CurReservationInfo);
            return job;
        }
    }
}
