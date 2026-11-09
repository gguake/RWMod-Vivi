using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 둥지 방(벽이 뚫린 동안은 원래 방 칸)의 오물을 치운다. 바닐라 청소(WorkGiver_CleanFilth)는 플레이어 집 구역 오물만 보고 청소 잡도 집 구역 밖 오물을
    // 건너뛰므로(JumpIfOutsideHomeArea) 바닐라와 같은 조건(안개·예약·두꺼워진 지 600틱)으로 고르고 집 구역 검사가 없는 둥지 청소 잡으로 치운다.
    // 한 번에 가장 가까운 오물과 같은 방의 오물을 가까운 것부터 15개까지 묶는다(바닐라와 같다).
    public class JobGiver_ViviNestClean : ThinkNode_JobGiver
    {
        private const int MinTicksSinceThickened = 600;
        private const int MaxQueuedFilth = 15;

        private static readonly List<Thing> _tmpFilth = new List<Thing>();

        protected override Job TryGiveJob(Pawn pawn)
        {
            var nest = BeekeepingUtility.SettledNestOf(pawn);
            if (nest == null || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Cleaning)) { return null; }

            var map = pawn.Map;
            _tmpFilth.Clear();
            foreach (var cell in nest.Territory.OwnedRoomCells)
            {
                var things = map.thingGrid.ThingsListAtFast(cell);
                for (int i = 0; i < things.Count; ++i)
                {
                    if (things[i] is Filth filth && !filth.Fogged() && filth.TicksSinceThickened >= MinTicksSinceThickened && pawn.CanReserve(filth))
                    {
                        _tmpFilth.Add(filth);
                    }
                }
            }
            if (_tmpFilth.Count == 0) { return null; }

            var position = pawn.Position;
            _tmpFilth.SortBy(t => t.Position.DistanceToSquared(position));
            if (!pawn.CanReach(_tmpFilth[0], PathEndMode.Touch, Danger.Some))
            {
                _tmpFilth.Clear();
                return null;
            }

            var job = JobMaker.MakeJob(VVJobDefOf.VV_ViviNestClean);
            var room = _tmpFilth[0].GetRoom();
            for (int i = 0; i < _tmpFilth.Count && job.GetTargetQueue(TargetIndex.A).Count < MaxQueuedFilth; ++i)
            {
                if (i == 0 || _tmpFilth[i].GetRoom() == room) { job.AddQueuedTarget(TargetIndex.A, _tmpFilth[i]); }
            }
            _tmpFilth.Clear();
            return job;
        }
    }
}
