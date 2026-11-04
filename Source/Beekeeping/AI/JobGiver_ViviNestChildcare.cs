using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 둥지 아기 돌보기: 바닐라 육아 워크기버(위험에서 데려오기·젖병 먹이기·놀아 주기)를 둥지 아기에게만 쓴다.
    // 로열이 육아에 전념하도록 로열용은 일보다 먼저, 다른 어른용은 일이 없을 때 돌보게 듀티에 따로 둔다.
    public class JobGiver_ViviNestChildcare : ThinkNode_JobGiver
    {
        public bool royal;

        private static readonly string[] WorkGiverNames = { "BringBabyToSafety", "BottleFeedBaby", "PlayWithBaby" };
        private static List<WorkGiver> _workers;

        private static List<WorkGiver> Workers
        {
            get
            {
                if (_workers == null)
                {
                    _workers = new List<WorkGiver>();
                    foreach (var name in WorkGiverNames)
                    {
                        var worker = DefDatabase<WorkGiverDef>.GetNamedSilentFail(name)?.Worker;
                        if (worker != null) { _workers.Add(worker); }
                    }
                }
                return _workers;
            }
        }

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            var copy = (JobGiver_ViviNestChildcare)base.DeepCopy(resolve);
            copy.royal = royal;
            return copy;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = pawn.GetNestLordJob();
            if (nest == null || !nest.Settled || !pawn.DevelopmentalStage.Adult()) { return null; }
            if (pawn.IsRoyalVivi() != royal) { return null; }

            var members = nest.lord.ownedPawns;
            foreach (var worker in Workers)
            {
                // 위험에서 데려오기는 맵의 같은 세력 아기를 스스로 찾는 비스캔 워크기버다.
                if (!(worker is WorkGiver_Scanner scanner))
                {
                    var job = worker.NonScanJob(pawn);
                    if (job != null) { return job; }
                    continue;
                }

                for (int i = 0; i < members.Count; ++i)
                {
                    var baby = members[i];
                    if (!baby.DevelopmentalStage.Baby() || !baby.Spawned) { continue; }

                    if (scanner.HasJobOnThing(pawn, baby))
                    {
                        var job = scanner.JobOnThing(pawn, baby);
                        if (job != null) { return job; }
                    }
                }
            }

            return null;
        }
    }
}
