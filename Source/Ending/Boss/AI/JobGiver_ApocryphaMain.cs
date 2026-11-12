using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class JobGiver_ApocryphaMain : ThinkNode_JobGiver
    {
        private static readonly PathFinderCostTuning ChargeTuning = new PathFinderCostTuning
        {
            costBlockedDoor = 0,
            costBlockedWallBase = 0,
            costBlockedWallExtraForNaturalWalls = 0,
            costBlockedDoorPerHitPoint = 0f,
            costBlockedWallExtraPerHitPoint = 0f,
        };

        private static readonly IntRange ChargeRepathTicks = new IntRange(45, 90);

        private const int BreachRecheckTicks = 60;
        private const int SkipRecoverTicks = 120;

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

            if (comp.Breath.IsBreathing)
            {
                return JobMaker.MakeJob(VVJobDefOf.VV_ApocryphaBreath, comp.Breath.BreathCell);
            }

            comp.Channel.TryGetStealTarget(out var target, out var range, out var everflower);
            if (target != null && comp.Channel.InStealRange())
            {
                return JobMaker.MakeJob(VVJobDefOf.VV_ApocryphaChannel, target, everflower);
            }

            if (comp.Phase == ApocryphaPhase.Unbound && target != null)
            {
                return ChargeJob(pawn, comp, target, range);
            }

            if (comp.Phase == ApocryphaPhase.Bound && target != null && GenTicks.TicksGame >= comp.NextApproachTick
                && comp.TrySkipToStealTarget(target, range))
            {
                comp.Notify_Approached();
                return WaitJob(SkipRecoverTicks);
            }

            if (comp.Phase == ApocryphaPhase.Bound && comp.Targeting.TryFindPursuitCell(out var pursuitCell))
            {
                return PursueJob(pursuitCell);
            }

            if (target != null)
            {
                if (comp.NeedleAttack.BreachInProgress)
                {
                    if (comp.NeedleAttack.TryContinueBreach(out var standCell))
                    {
                        return BreachJob(pawn, standCell);
                    }
                }
                else if (GenTicks.TicksGame >= comp.NextApproachTick)
                {
                    comp.Notify_Approached();

                    if (comp.NeedleAttack.TryStartBreach(target, out var breachCell))
                    {
                        return BreachJob(pawn, breachCell);
                    }

                    var approachJob = ApproachJob(pawn, comp, target, range);
                    if (approachJob != null)
                    {
                        return approachJob;
                    }
                }
            }

            return WanderOrWaitJob(pawn, comp);
        }

        private static Job WaitJob(int ticks)
        {
            var job = JobMaker.MakeJob(VVJobDefOf.VV_ApocryphaWait);
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

            var job = JobMaker.MakeJob(VVJobDefOf.VV_ApocryphaApproach, cell);
            job.locomotionUrgency = LocomotionUrgency.Jog;
            job.expiryInterval = 2500;
            job.checkOverrideOnExpire = true;
            return job;
        }

        private static Job BreachJob(Pawn pawn, IntVec3 standCell)
        {
            if (pawn.Position.InHorDistOf(standCell, 1.5f))
            {
                return WaitJob(BreachRecheckTicks);
            }

            var job = JobMaker.MakeJob(VVJobDefOf.VV_ApocryphaApproach, standCell);
            job.locomotionUrgency = LocomotionUrgency.Jog;
            job.expiryInterval = BreachRecheckTicks * 5;
            job.checkOverrideOnExpire = true;
            return job;
        }

        private static Job PursueJob(IntVec3 cell)
        {
            var job = JobMaker.MakeJob(VVJobDefOf.VV_ApocryphaPursue, cell);
            job.locomotionUrgency = LocomotionUrgency.Jog;
            job.expiryInterval = 300;
            job.checkOverrideOnExpire = true;
            return job;
        }

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

            var job = JobMaker.MakeJob(VVJobDefOf.VV_ApocryphaWander, dest);
            job.locomotionUrgency = LocomotionUrgency.Jog;
            job.expiryInterval = 2500;
            job.checkOverrideOnExpire = true;
            return job;
        }

        private static Job ChargeJob(Pawn pawn, CompApocrypha comp, Thing target, float range)
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

            if (!found && comp.TrySkipToStealTarget(target, range))
            {
                return WaitJob(SkipRecoverTicks);
            }

            if (blocker != null && CanSmash(blocker, target))
            {
                var smashJob = JobMaker.MakeJob(VVJobDefOf.VV_ApocryphaSmash, blocker, cellBefore.IsValid ? cellBefore : pawn.Position);
                smashJob.expiryInterval = 2500;
                return smashJob;
            }

            if (goal == pawn.Position || (!found && !pawn.CanReach(goal, PathEndMode.OnCell, Danger.Deadly)))
            {
                return WaitJob(ChargeRepathTicks.RandomInRange);
            }

            var job = JobMaker.MakeJob(VVJobDefOf.VV_ApocryphaCharge, goal);
            job.locomotionUrgency = LocomotionUrgency.Jog;
            job.expiryInterval = ChargeRepathTicks.RandomInRange;
            job.checkOverrideOnExpire = true;
            return job;
        }

        public static bool CanSmash(Thing blocker, Thing stealTarget)
        {
            if (blocker == null || blocker.Destroyed || !blocker.Spawned) { return false; }
            if (blocker == stealTarget) { return false; }
            if (blocker is Building_EverflowerAltar || blocker is ArcanePlant_Everflower) { return false; }
            if (!blocker.def.useHitPoints) { return false; }
            if (blocker.def.building != null && !blocker.def.building.canBeDamagedByAttacks) { return false; }

            return true;
        }
    }
}
