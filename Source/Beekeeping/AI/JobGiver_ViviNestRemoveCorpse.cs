using RimWorld;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 둥지 가까이 놓인 시체를 둥지에서 먼 바깥으로 옮긴다(바닐라 HaulToCell).
    // 썩은 시체는 악취 가스를 내고 방 안에 고인 가스는 폐부패를 일으켜, 시체 하나로 로열까지 잇달아 죽었다(장기 계측).
    public class JobGiver_ViviNestRemoveCorpse : ThinkNode_JobGiver
    {
        private const float NearNestRadius = 12f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || pawn.DevelopmentalStage.Baby()) { return null; }
            if (BeekeepingUtility.OutdoorDangerous(pawn.Map)) { return null; }

            var corpses = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse);
            for (int i = 0; i < corpses.Count; ++i)
            {
                var corpse = corpses[i];
                if (!nest.Territory.IsTerritoryCell(corpse.Position) && !corpse.Position.InHorDistOf(nest.NestCell, NearNestRadius)) { continue; }
                if (corpse.IsBurning() || !pawn.CanReserveAndReach(corpse, PathEndMode.ClosestTouch, Danger.Some)) { continue; }
                if (!nest.Planner.TryFindCorpseDumpCell(pawn, out var cell)) { return null; }

                // 둥지 비비에게는 둥지 몫이 아닌 물건이 금지이므로 이 잡만 금지를 보지 않는다.
                var job = JobMaker.MakeJob(JobDefOf.HaulToCell, corpse, cell);
                job.count = 1;
                job.haulMode = HaulMode.ToCellNonStorage;
                job.ignoreForbidden = true;
                return job;
            }

            return null;
        }
    }
}
