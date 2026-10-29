using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    // 아포크리파의 유일한 작업 지급기. 판단의 근거(타이머/조준/채널링 상태)는 CompApocrypha에 있고 여기서는 알맞은 작업만 고른다.
    public class JobGiver_ApocryphaMain : ThinkNode_JobGiver
    {
        // 2페이즈 돌진 경로: 부술 수 있는 벽·문에 추가 비용을 주지 않아 우회하지 않고 직선으로 뚫고 가는 경로를 얻는다.
        // (비용을 조금이라도 주면 짧은 벽은 몇 칸 우회하는 쪽의 비용이 더 낮아져, 의도한 "직선 돌파"가 이루어지지 않는다)
        private static readonly PathFinderCostTuning ChargeTuning = new PathFinderCostTuning
        {
            costBlockedDoor = 0,
            costBlockedWallBase = 0,
            costBlockedWallExtraForNaturalWalls = 0,
            costBlockedDoorPerHitPoint = 0f,
            costBlockedWallExtraPerHitPoint = 0f,
        };

        // 돌진 중 앞에 새 벽이 세워지면 일반 경로로 돌아가지 않고 곧바로 부수도록 자주(약 1초마다) 경로를 다시 본다.
        private static readonly IntRange ChargeRepathTicks = new IntRange(45, 90);

        protected override Job TryGiveJob(Pawn pawn)
        {
            var comp = pawn.TryGetComp<CompApocrypha>();
            if (comp == null) { return null; }

            // 전환/소멸 중이거나 기절해 있으면 제자리에서 기다린다(기절 중에 채널링을 다시 잡지 않게).
            if (!comp.IsActive)
            {
                return WaitJob(60);
            }

            if (pawn.stances.stunner.Stunned)
            {
                return WaitJob(Mathf.Max(15, pawn.stances.stunner.StunTicksLeft + 1));
            }

            // 1페이즈 사출 포격 조준은 작업 없이 CompApocrypha가 처리한다(조준 중에도 계속 움직인다).
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

            if (target != null && GenTicks.TicksGame >= comp.NextApproachTick)
            {
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

        // 1페이즈: 영원꽃(제단) 쪽으로 최대 12칸 다가간다. 가까우면 제단 발판 가장자리까지, 막혀 있으면 갈 수 있는 데까지만.
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

        // 찬탈 대상(제단 5x5 또는 영원꽃)의 둘레에서 채널링 거리 안에 있고 서 있을 수 있는 칸 중 보스에게 가장 가까운 칸. 발판 중앙의 영원꽃 위로 올라서지 않게 한다.
        // (제단 대각 모서리 칸은 둘레지만 중심에서 4.24칸이라 채널링 거리 3.9 밖이다. 거기 멈춰 서면 채널링을 못 하고 계속 같은 칸만 고르게 된다)
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

        // 1페이즈 평상시: 현재 위치 반경 10 안을 배회하고 600~1200틱씩 멈춰 선다.
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

        // 2페이즈: 제단으로 직선 돌진하며 가로막는 구조물(제단·영원꽃 제외)은 부수고 지나간다.
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

        // 제단·영원꽃·찬탈 대상은 절대 부수지 않는다. 공격으로 피해를 입지 않는 구조물도 대상에서 뺀다.
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
