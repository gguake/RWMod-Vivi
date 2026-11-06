using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VVRace
{
    // 둥지를 떠날 때(평화 이주·적대 도주) 어른은 아기를 안고 나가고, 나머지는 바닐라 맵 나가기로 나간다.
    // 아기는 스스로 걷지 못해 바닐라 맵 나가기 토일로는 맵에 남고 로드가 끝나지 않는다.
    public class LordToil_ViviNestExit : LordToil_ViviNest
    {
        private const int LeftBehindCheckInterval = 60;

        private static readonly List<Pawn> _tmpLeftBehind = new List<Pawn>();

        private readonly LocomotionUrgency _locomotion;
        private readonly bool _canDig;

        public override bool AllowSatisfyLongNeeds => false;

        public LordToil_ViviNestExit(LocomotionUrgency locomotion, bool canDig)
        {
            _locomotion = locomotion;
            _canDig = canDig;
        }

        public override void UpdateAllDuties()
        {
            for (int i = 0; i < lord.ownedPawns.Count; ++i)
            {
                var duty = new PawnDuty(VVDutyDefOf.VV_ViviNestExit)
                {
                    locomotion = _locomotion,
                    canDig = _canDig,
                };
                lord.ownedPawns[i].mindState.duty = duty;
            }
        }

        // 어른은 아기를 하나씩만 안으므로 안아 줄 어른이 더 없으면 남은 아기는 두고 간다(들고 갈 수 있는 만큼만 데려간다).
        // 남은 아기를 로드에 두면 아기가 죽을 때까지 로드가 끝나지 않아 새 유인과 둥지 정리(Cleanup)가 막혔다.
        public override void LordToilTick()
        {
            base.LordToilTick();
            if (Find.TickManager.TicksGame % LeftBehindCheckInterval != 0 || AnyFreeCarrier()) { return; }

            var pawns = lord.ownedPawns;
            for (int i = 0; i < pawns.Count; ++i)
            {
                if (pawns[i].Spawned && pawns[i].DevelopmentalStage.Baby() && !IsBeingFetched(pawns[i])) { _tmpLeftBehind.Add(pawns[i]); }
            }
            for (int i = 0; i < _tmpLeftBehind.Count; ++i)
            {
                lord.Notify_PawnLost(_tmpLeftBehind[i], PawnLostCondition.Undefined);
            }
            _tmpLeftBehind.Clear();
        }

        // 아직 아기를 안으러 갈 수 있는 어른(JobGiver_ViviNestCarryBabyOut): 쓰러지지 않았고 아무것도 안지 않았으며 다른 아기를 데리러 가는 중이 아닌 구성원
        private bool AnyFreeCarrier()
        {
            var pawns = lord.ownedPawns;
            for (int i = 0; i < pawns.Count; ++i)
            {
                var pawn = pawns[i];
                if (pawn.Spawned && !pawn.Downed && pawn.DevelopmentalStage.Adult() && pawn.carryTracker?.CarriedThing == null && pawn.CurJobDef != JobDefOf.Kidnap)
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsBeingFetched(Pawn baby)
        {
            var pawns = lord.ownedPawns;
            for (int i = 0; i < pawns.Count; ++i)
            {
                var job = pawns[i].CurJob;
                if (job != null && job.def == JobDefOf.Kidnap && job.targetA.Thing == baby) { return true; }
            }
            return false;
        }
    }
}
