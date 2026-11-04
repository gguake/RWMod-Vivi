using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 세력 없는 광폭 말벌은 공격 대상 캐시에 정착민이 잡히지 않아 바닐라 JobGiver_AIGotoNearestHostile이 아무 대상도 찾지 못한다.
    // 광폭 공격 탐색 거리 밖에 있는 정착지로는 플레이어 세력 폰을 직접 훑어 다가간다.
    public class JobGiver_HornetGotoColony : ThinkNode_JobGiver
    {
        private const int ExpiryIntervalTicks = 120;

        private static readonly List<Pawn> _tmpCandidates = new List<Pawn>();

        protected override Job TryGiveJob(Pawn pawn)
        {
            _tmpCandidates.Clear();
            foreach (var candidate in pawn.Map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer))
            {
                if (candidate.Downed || candidate.IsPsychologicallyInvisible() || !pawn.HostileTo(candidate)) { continue; }

                _tmpCandidates.Add(candidate);
            }

            if (_tmpCandidates.Count == 0) { return null; }

            _tmpCandidates.SortBy(candidate => (candidate.Position - pawn.Position).LengthHorizontalSquared);

            var fenceBlocked = pawn.def.race.FenceBlocked;
            var passDoors = TraverseParms.For(pawn, Danger.Deadly, TraverseMode.PassDoors, canBashFences: fenceBlocked);
            try
            {
                foreach (var target in _tmpCandidates)
                {
                    if (pawn.CanReach(target, PathEndMode.Touch, Danger.Deadly, canBashDoors: false, fenceBlocked))
                    {
                        // 다가가는 도중 광폭 공격 탐색 거리에 들어오면 근접 공격으로 바뀌도록 짧게 재평가한다.
                        var job = JobMaker.MakeJob(JobDefOf.Goto, target);
                        job.expiryInterval = ExpiryIntervalTicks;
                        job.checkOverrideOnExpire = true;
                        job.collideWithPawns = true;
                        job.canBashFences = fenceBlocked;
                        return job;
                    }

                    if (pawn.Map.reachability.CanReach(pawn.Position, target, PathEndMode.Touch, passDoors))
                    {
                        return JobGiver_HornetBerserk.ApproachBlockedTargetJob(pawn, target);
                    }
                }
            }
            finally
            {
                _tmpCandidates.Clear();
            }

            return null;
        }
    }
}
