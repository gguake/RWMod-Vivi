using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 둥지 새싹 등 연료(마나 비료)를 먹는 둥지 것을 바닐라 연료 보충으로 채운다(연료는 둥지 몫만 쓴다).
    public class JobGiver_ViviNestRefuel : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || pawn.DevelopmentalStage.Baby()) { return null; }

            var refuelables = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Refuelable);
            for (int i = 0; i < refuelables.Count; ++i)
            {
                var thing = refuelables[i];
                if (thing.Faction != nest.Faction || !pawn.CanReach(thing, PathEndMode.Touch, Danger.Some)) { continue; }
                if (!RefuelWorkGiverUtility.CanRefuel(pawn, thing)) { continue; }

                return RefuelWorkGiverUtility.RefuelJob(pawn, thing);
            }

            return null;
        }
    }
}
