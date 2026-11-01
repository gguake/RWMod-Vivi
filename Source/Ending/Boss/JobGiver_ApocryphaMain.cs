using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_ApocryphaMain : ThinkNode_JobGiver
    {
        // 짧은 벽도 우회하지 않고 직선으로 돌파하도록 파괴 가능한 벽·문의 추가 경로 비용을 0으로 둔다.
        private static readonly PathFinderCostTuning ChargeTuning = new PathFinderCostTuning
        {
            costBlockedDoor = 0,
            costBlockedWallBase = 0,
            costBlockedWallExtraForNaturalWalls = 0,
            costBlockedDoorPerHitPoint = 0f,
            costBlockedWallExtraPerHitPoint = 0f,
        };

        // 돌진 중 새로 생긴 벽도 즉시 부수도록 약 1초마다 경로를 다시 검사한다.
        private static readonly IntRange ChargeRepathTicks = new IntRange(45, 90);

        private const int BreachRecheckTicks = 60;

        protected override Job TryGiveJob(Pawn pawn)
        {
            var comp = pawn.TryGetComp<CompApocrypha>();
            if (comp == null) { return null; }

            if (!comp.IsActive)
            {
                return WaitJob(60);
            }

            if (pawn.stances.stunner.Stunned)
            {
                return WaitJob(Mathf.Max(15, pawn.stances.stunner.StunTicksLeft + 1));
            }

            if (comp.IsBreathing)
            {
                return JobMaker.MakeJob(ApocryphaJobDefOf.VV_ApocryphaBreath, comp.BreathCell);
            }

            comp.TryGetStealTarget(out var target, out var range, out var everflower);
            if (target != null && comp.InStealRange())
            {
                return JobMaker.MakeJob(ApocryphaJobDefOf.VV_ApocryphaChannel, target, everflower);
            }

            if (comp.Phase == ApocryphaPhase.Unbound && target != null)
            {
                return ChargeJob(pawn, target, range);
            }

            if (comp.Phase == ApocryphaPhase.Bound && comp.TryFindPursuitCell(out var pursuitCell))
            {
                return PursueJob(pursuitCell);
            }

            if (target != null && GenTicks.TicksGame >= comp.NextApproachTick)
            {
                // 찬탈 대상까지 길이 막혀 있으면 접근 주기를 쓰지 않고, 길이 열릴 때까지 직선으로 다가가 바늘로 길을 연다.
                if (comp.TryStartBreach(target, out var breachCell))
                {
                    return BreachJob(pawn, breachCell);
                }

                comp.Notify_Approached();

                var approachJob = ApproachJob(pawn, comp, target, range);
                if (approachJob != null)
                {
                    return approachJob;
                }
            }

            return WanderOrWaitJob(pawn, comp);
        }

        private static Job WaitJob(int ticks)
        {
            var job = JobMaker.MakeJob(ApocryphaJobDefOf.VV_ApocryphaWait);
            job.expiryInterval = ticks;
            return job;
        }

        private static Job ApproachJob(Pawn pawn, CompApocrypha comp, Thing target, float range)
        {
            var map = pawn.Map;
            var origin = pawn.Position;
            var distance = origin.DistanceTo(target.Position);
            if (distance < 1f) { return null; }

            var direction = (target.Position - origin).ToVector3();
            direction.Normalize();

            bool Reachable(IntVec3 c) => c.InBounds(map) && c != origin && pawn.CanReach(c, PathEndMode.OnCell, Danger.Deadly);

            var cell = IntVec3.Invalid;
            if (distance <= comp.Props.approachDistance)
            {
                cell = CellFinder.StandableCellNear(EdgeCellToward(pawn, target, range), map, 3f, Reachable);
            }

            for (var step = Mathf.Min(comp.Props.approachDistance, distance); !cell.IsValid && step >= 3f; step -= 3f)
            {
                var point = (origin.ToVector3Shifted() + direction * step).ToIntVec3();
                cell = CellFinder.StandableCellNear(point, map, 4f, Reachable);
            }

            if (!cell.IsValid) { return null; }

            var job = JobMaker.MakeJob(ApocryphaJobDefOf.VV_ApocryphaApproach, cell);
            job.locomotionUrgency = LocomotionUrgency.Jog;
            job.expiryInterval = 2500;
            job.checkOverrideOnExpire = true;
            return job;
        }

        // 막히기 직전 칸까지 가서 기다리면 바늘이 길을 연다. 벽이 무너지면 바로 다음 칸을 고르도록 짧게 다시 검사한다.
        private static Job BreachJob(Pawn pawn, IntVec3 standCell)
        {
            if (pawn.Position.InHorDistOf(standCell, 1.5f))
            {
                return WaitJob(BreachRecheckTicks);
            }

            var job = JobMaker.MakeJob(ApocryphaJobDefOf.VV_ApocryphaApproach, standCell);
            job.locomotionUrgency = LocomotionUrgency.Jog;
            job.expiryInterval = BreachRecheckTicks * 5;
            job.checkOverrideOnExpire = true;
            return job;
        }

        // 공격자의 이동에 대응하도록 5초마다 접근 위치를 다시 고른다.
        private static Job PursueJob(IntVec3 cell)
        {
            var job = JobMaker.MakeJob(ApocryphaJobDefOf.VV_ApocryphaPursue, cell);
            job.locomotionUrgency = LocomotionUrgency.Jog;
            job.expiryInterval = 300;
            job.checkOverrideOnExpire = true;
            return job;
        }

        // 영원꽃 위에 올라서지 않도록 찬탈 대상의 둘레에서 채널링 거리 안의 칸을 고른다.
        // 제단의 대각 모서리는 중심에서 4.24칸으로 사거리 3.9칸 밖이다. 이를 제외하지 않으면 이동만 반복한다.
        private static IntVec3 EdgeCellToward(Pawn pawn, Thing target, float range)
        {
            var map = pawn.Map;
            var origin = pawn.Position;
            var best = IntVec3.Invalid;
            var bestDistance = int.MaxValue;
            foreach (var cell in GenAdj.CellsAdjacent8Way(target))
            {
                if (!cell.InBounds(map) || !cell.Standable(map) || !cell.InHorDistOf(target.Position, range)) { continue; }

                var distance = cell.DistanceToSquared(origin);
                if (distance < bestDistance)
                {
                    best = cell;
                    bestDistance = distance;
                }
            }

            return best.IsValid ? best : target.Position;
        }

        private static Job WanderOrWaitJob(Pawn pawn, CompApocrypha comp)
        {
            var mindState = pawn.mindState;
            if (mindState.nextMoveOrderIsWait)
            {
                mindState.nextMoveOrderIsWait = false;
                return WaitJob(comp.Props.wanderWaitTicks.RandomInRange);
            }

            mindState.nextMoveOrderIsWait = true;

            var dest = RCellFinder.RandomWanderDestFor(pawn, pawn.Position, comp.Props.wanderRadius, null, Danger.Deadly);
            if (!dest.IsValid || dest == pawn.Position)
            {
                return WaitJob(comp.Props.wanderWaitTicks.RandomInRange);
            }

            var job = JobMaker.MakeJob(ApocryphaJobDefOf.VV_ApocryphaWander, dest);
            job.locomotionUrgency = LocomotionUrgency.Jog;
            job.expiryInterval = 2500;
            job.checkOverrideOnExpire = true;
            return job;
        }

        private static Job ChargeJob(Pawn pawn, Thing target, float range)
        {
            var map = pawn.Map;
            var goal = EdgeCellToward(pawn, target, range);

            Thing blocker = null;
            var cellBefore = IntVec3.Invalid;
            var found = false;
            using (var path = map.pathFinder.FindPathNow(pawn.Position, goal, TraverseParms.For(TraverseMode.PassAllDestroyableThings), ChargeTuning, PathEndMode.OnCell))
            {
                if (path.Found)
                {
                    found = true;
                    blocker = path.FirstBlockingBuilding(out cellBefore, pawn);
                }
            }

            if (blocker != null && CanSmash(blocker, target))
            {
                var smashJob = JobMaker.MakeJob(ApocryphaJobDefOf.VV_ApocryphaSmash, blocker, cellBefore.IsValid ? cellBefore : pawn.Position);
                smashJob.expiryInterval = 2500;
                return smashJob;
            }

            if (goal == pawn.Position || (!found && !pawn.CanReach(goal, PathEndMode.OnCell, Danger.Deadly)))
            {
                return WaitJob(ChargeRepathTicks.RandomInRange);
            }

            var job = JobMaker.MakeJob(ApocryphaJobDefOf.VV_ApocryphaCharge, goal);
            job.locomotionUrgency = LocomotionUrgency.Jog;
            job.expiryInterval = ChargeRepathTicks.RandomInRange;
            job.checkOverrideOnExpire = true;
            return job;
        }

        public static bool CanSmash(Thing blocker, Thing stealTarget)
        {
            if (blocker == null || blocker.Destroyed || !blocker.Spawned) { return false; }
            if (blocker == stealTarget) { return false; }
            if (blocker is Building_DreamumAltar || blocker is ArcanePlant_Everflower) { return false; }
            if (!blocker.def.useHitPoints) { return false; }
            if (blocker.def.building != null && !blocker.def.building.canBeDamagedByAttacks) { return false; }

            return true;
        }
    }
}
