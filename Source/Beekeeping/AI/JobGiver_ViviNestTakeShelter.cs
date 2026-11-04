using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 적·사냥하는 포식자가 가까우면 둥지 방으로 피하고 방 안에 머문다. 위협이 방 안에 있거나 방으로 갈 수 없으면 바닐라 도망으로 멀어진다.
    public class JobGiver_ViviNestTakeShelter : ThinkNode_JobGiver
    {
        private const float DangerRadius = 15f;
        private const int FleeDistance = 12;
        private const int WaitTicks = 180;

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || pawn.Downed || pawn.DevelopmentalStage.Baby()) { return null; }
            if (!GenAI.EnemyIsNear(pawn, DangerRadius, out var threat)) { return null; }

            var territory = nest.Territory;
            var threatInRoom = territory.IsRoomCell(threat.Position);
            if (!threatInRoom)
            {
                if (territory.IsRoomCell(pawn.Position))
                {
                    var wait = JobMaker.MakeJob(JobDefOf.Wait_Combat, WaitTicks);
                    wait.checkOverrideOnExpire = true;
                    return wait;
                }

                if (nest.TryGetRandomRoomCell(out var cell) && pawn.CanReach(cell, PathEndMode.OnCell, Danger.Deadly))
                {
                    var job = JobMaker.MakeJob(JobDefOf.Goto, cell);
                    job.locomotionUrgency = LocomotionUrgency.Sprint;
                    return job;
                }
            }

            return FleeUtility.FleeJob(pawn, threat, FleeDistance);
        }
    }
}
