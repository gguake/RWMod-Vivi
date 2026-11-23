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
        private const int UrgentApproachRepathTicks = 300;
        private const float BreachRangeBuffer = 2f;
        private const float ApproachGoalTolerance = 2f;
        private const float StealRangeMargin = 1.5f;
        private const float EngageRange = 1.5f;
        private const int EngageHoldTicks = 30;

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

            if (comp.EscortSummon.IsCasting)
            {
                return WaitJob(Mathf.Max(15, comp.EscortSummon.CastTicksLeft + 1));
            }

            if (comp.Embrace.IsCasting)
            {
                return WaitJob(Mathf.Max(15, comp.Embrace.TicksLeft + 1));
            }

            if (comp.BlastBreach.ClearingPathBlock)
            {
                return WaitJob(Mathf.Max(15, comp.BlastBreach.PathBlockTicksLeft + 1));
            }

            comp.Channel.TryGetStealTarget(out var target, out var range, out var everflower);
            if (target != null && comp.Channel.InStealRange())
            {
                return JobMaker.MakeJob(VVJobDefOf.VV_ApocryphaChannel, target, everflower);
            }

            if (comp.Phase == ApocryphaPhase.Queen)
            {
                var engageTarget = comp.Targeting.EngageTarget;
                if (engageTarget != null)
                {
                    return EngageJob(pawn, comp, engageTarget);
                }

                if (target != null)
                {
                    return ChargeJob(pawn, comp, target, range, VVJobDefOf.VV_ApocryphaCharge, allowSkip: true);
                }
            }

            if (comp.Phase == ApocryphaPhase.Asphodel)
            {
                var asphodelJob = TryGiveAsphodelJob(pawn, comp, target, range);
                if (asphodelJob != null) { return asphodelJob; }
            }

            return WanderOrWaitJob(pawn, comp, target, range);
        }

        private static Job EngageJob(Pawn pawn, CompApocrypha comp, Pawn target)
        {
            if (pawn.Position.InHorDistOf(target.Position, EngageRange))
            {
                pawn.rotationTracker.FaceTarget(target);
                return WaitJob(EngageHoldTicks);
            }

            return ChargeJob(pawn, comp, target, EngageRange, VVJobDefOf.VV_ApocryphaPursue, allowSkip: false);
        }

        private static Job TryGiveAsphodelJob(Pawn pawn, CompApocrypha comp, Thing target, float range)
        {
            if (target != null)
            {
                comp.EnsureApproachRadius(target);
            }

            var urgentApproach = target != null && comp.Bond.IsDecaying;
            if (target != null && (urgentApproach || GenTicks.TicksGame >= comp.NextApproachTick) && comp.TrySkipToStealTarget(target, range))
            {
                comp.SyncApproachRadiusAfterSkip(target, range);
                comp.Notify_Approached();
                return WaitJob(SkipRecoverTicks);
            }

            if (urgentApproach)
            {
                comp.SyncApproachRadiusToCurrent(target);
                if (comp.BlastBreach.InProgress && comp.BlastBreach.TryContinue(out var urgentStandCell))
                {
                    return BreachJob(pawn, urgentStandCell);
                }

                return DirectApproachJob(pawn, comp, target, range);
            }

            if (comp.Targeting.TryFindPursuitCell(out var pursuitCell))
            {
                return MakeMoveJob(VVJobDefOf.VV_ApocryphaPursue, pursuitCell, 300);
            }

            if (target == null) { return null; }

            if (comp.BlastBreach.InProgress)
            {
                return comp.BlastBreach.TryContinue(out var standCell) ? BreachJob(pawn, standCell) : null;
            }

            if (GenTicks.TicksGame < comp.NextApproachTick) { return null; }

            if (comp.Targeting.InCombat)
            {
                comp.Notify_Approached();
                return null;
            }

            comp.AdvanceApproach(target);

            var originDist = pawn.Position.DistanceTo(target.Position);
            if (comp.ApproachRadius > range && originDist <= comp.ApproachRadius + 1f)
            {
                return null;
            }

            var approachJob = ApproachJob(pawn, comp, target, range, requireGoalProgress: true);
            if (approachJob != null)
            {
                return approachJob;
            }

            var maxBreachDist = comp.Props.approachDistance + BreachRangeBuffer;
            if (comp.BlastBreach.TryStart(target, maxBreachDist, out var breachCell))
            {
                return BreachJob(pawn, breachCell);
            }

            return ApproachJob(pawn, comp, target, range, requireGoalProgress: false);
        }

        private static Job WaitJob(int ticks)
        {
            var job = JobMaker.MakeJob(VVJobDefOf.VV_ApocryphaWait);
            job.expiryInterval = ticks;
            return job;
        }

        private static Job MakeMoveJob(JobDef def, IntVec3 target, int expiryTicks)
        {
            var job = JobMaker.MakeJob(def, target);
            job.locomotionUrgency = LocomotionUrgency.Jog;
            job.expiryInterval = expiryTicks;
            job.checkOverrideOnExpire = true;
            return job;
        }

        private static Job DirectApproachJob(Pawn pawn, CompApocrypha comp, Thing target, float range)
        {
            var map = pawn.Map;
            var origin = pawn.Position;
            var distance = origin.DistanceTo(target.Position);
            if (distance < 1f) { return null; }

            bool Reachable(IntVec3 c) =>
                c.InBounds(map)
                && c != origin
                && c.Standable(map)
                && pawn.CanReach(c, PathEndMode.OnCell, Danger.Deadly);

            var edge = EdgeCellToward(pawn, target, range);
            var cell = Reachable(edge) && edge.InHorDistOf(target.Position, range)
                ? edge
                : CellFinder.StandableCellNear(edge, map, 3f, c => Reachable(c) && c.InHorDistOf(target.Position, range));
            if (cell.IsValid)
            {
                return MakeMoveJob(VVJobDefOf.VV_ApocryphaApproach, cell, UrgentApproachRepathTicks);
            }

            if (comp.BlastBreach.TryStart(target, 0f, out var breachCell))
            {
                return BreachJob(pawn, breachCell);
            }

            var direction = (target.Position - origin).ToVector3();
            direction.y = 0f;
            direction.Normalize();

            var maxStep = Mathf.Min(comp.Props.approachDistance, distance);
            for (var step = maxStep; !cell.IsValid && step >= 3f; step -= 3f)
            {
                var point = (origin.ToVector3Shifted() + direction * step).ToIntVec3().ClampInsideMap(map);
                cell = CellFinder.StandableCellNear(point, map, 4f, c => Reachable(c) && c.DistanceTo(target.Position) < distance - 1f);
            }

            if (!cell.IsValid && maxStep > 0f && maxStep < 3f)
            {
                var point = (origin.ToVector3Shifted() + direction * maxStep).ToIntVec3().ClampInsideMap(map);
                cell = CellFinder.StandableCellNear(point, map, 4f, c => Reachable(c) && c.DistanceTo(target.Position) < distance - 1f);
            }

            return cell.IsValid
                ? MakeMoveJob(VVJobDefOf.VV_ApocryphaApproach, cell, UrgentApproachRepathTicks)
                : WaitJob(BreachRecheckTicks);
        }

        private static Job ApproachJob(Pawn pawn, CompApocrypha comp, Thing target, float range, bool requireGoalProgress)
        {
            var map = pawn.Map;
            var origin = pawn.Position;
            var distance = origin.DistanceTo(target.Position);
            if (distance < 1f) { return null; }

            var targetRadius = Mathf.Max(0f, comp.ApproachRadius);
            var desiredRadius = Mathf.Max(targetRadius, distance - comp.Props.approachDistance);
            var safeMin = targetRadius > range ? range + StealRangeMargin : 0f;
            var maxGoalDistance = desiredRadius + ApproachGoalTolerance;

            bool Reachable(IntVec3 c) =>
                c.InBounds(map)
                && c != origin
                && c.Standable(map)
                && pawn.CanReach(c, PathEndMode.OnCell, Danger.Deadly)
                && (safeMin <= 0f || c.DistanceTo(target.Position) >= safeMin);

            bool MeetsGoal(IntVec3 c)
            {
                if (!Reachable(c)) { return false; }

                var d = c.DistanceTo(target.Position);
                if (requireGoalProgress)
                {
                    if (desiredRadius <= range)
                    {
                        return d <= range;
                    }

                    return d <= maxGoalDistance && d < distance - 1f;
                }

                return d < distance - 1.5f;
            }

            var cell = IntVec3.Invalid;
            if (desiredRadius <= range)
            {
                cell = CellFinder.StandableCellNear(EdgeCellToward(pawn, target, range), map, 3f, MeetsGoal);
            }

            if (!cell.IsValid)
            {
                var direction = (target.Position - origin).ToVector3();
                direction.Normalize();

                var maxStep = requireGoalProgress
                    ? Mathf.Min(comp.Props.approachDistance, Mathf.Max(0f, distance - desiredRadius))
                    : Mathf.Min(comp.Props.approachDistance, distance);

                for (var step = maxStep; !cell.IsValid && step >= 3f; step -= 3f)
                {
                    var point = (origin.ToVector3Shifted() + direction * step).ToIntVec3().ClampInsideMap(map);
                    cell = CellFinder.StandableCellNear(point, map, 4f, MeetsGoal);
                }

                if (!cell.IsValid && maxStep > 0f && maxStep < 3f)
                {
                    var point = (origin.ToVector3Shifted() + direction * maxStep).ToIntVec3().ClampInsideMap(map);
                    cell = CellFinder.StandableCellNear(point, map, 4f, MeetsGoal);
                }
            }

            return cell.IsValid ? MakeMoveJob(VVJobDefOf.VV_ApocryphaApproach, cell, 2500) : null;
        }

        private static Job BreachJob(Pawn pawn, IntVec3 standCell)
        {
            if (pawn.Position.InHorDistOf(standCell, 1.5f))
            {
                return WaitJob(BreachRecheckTicks);
            }

            return MakeMoveJob(VVJobDefOf.VV_ApocryphaApproach, standCell, BreachRecheckTicks * 5);
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

        private static Job WanderOrWaitJob(Pawn pawn, CompApocrypha comp, Thing target, float range)
        {
            var mindState = pawn.mindState;
            if (mindState.nextMoveOrderIsWait)
            {
                mindState.nextMoveOrderIsWait = false;
                return WaitJob(GetWanderWaitTicks(comp, target));
            }

            mindState.nextMoveOrderIsWait = true;

            var dest = comp.Phase == ApocryphaPhase.Asphodel && target != null
                ? FindDonutWanderDest(pawn, comp, target, range)
                : RCellFinder.RandomWanderDestFor(pawn, pawn.Position, comp.Props.wanderRadius, null, Danger.Deadly);

            if (!dest.IsValid || dest == pawn.Position)
            {
                return WaitJob(GetWanderWaitTicks(comp, target));
            }

            return MakeMoveJob(VVJobDefOf.VV_ApocryphaWander, dest, 2500);
        }

        private static int GetWanderWaitTicks(CompApocrypha comp, Thing target)
        {
            var ticks = comp.Props.wanderWaitTicks.RandomInRange;
            if (comp.Phase != ApocryphaPhase.Asphodel || target == null) { return ticks; }

            var remaining = comp.NextApproachTick - GenTicks.TicksGame;
            if (remaining <= 0) { return ticks; }

            if (comp.IsIdleForApproach && comp.Props.idleApproachSpeedFactor > 1f)
            {
                remaining = Mathf.CeilToInt(remaining / comp.Props.idleApproachSpeedFactor);
            }

            return Mathf.Min(ticks, Mathf.Max(60, remaining));
        }

        private static IntVec3 FindDonutWanderDest(Pawn pawn, CompApocrypha comp, Thing target, float stealRange)
        {
            comp.EnsureApproachRadius(target);

            var map = pawn.Map;
            var origin = pawn.Position;
            var center = target.Position;
            comp.GetApproachRing(stealRange, out var minRadius, out var maxRadius);

            var minSq = minRadius * minRadius;
            var maxSq = maxRadius * maxRadius;
            var safeMinSq = comp.ApproachRadius > stealRange ? (stealRange + StealRangeMargin) * (stealRange + StealRangeMargin) : 0f;
            var wanderRadius = comp.Props.wanderRadius;

            bool BaseValid(IntVec3 c)
            {
                if (!c.InBounds(map) || c == origin || !c.Standable(map)) { return false; }
                if (!pawn.CanReach(c, PathEndMode.OnCell, Danger.Deadly)) { return false; }

                return (c - center).LengthHorizontalSquared >= safeMinSq;
            }

            bool InDonut(IntVec3 c)
            {
                if (!BaseValid(c)) { return false; }

                var distSq = (c - center).LengthHorizontalSquared;
                return distSq >= minSq && distSq <= maxSq;
            }

            var dest = RCellFinder.RandomWanderDestFor(pawn, origin, wanderRadius, (p, c, r) => InDonut(c), Danger.Deadly);
            if (dest.IsValid && dest != origin && InDonut(dest))
            {
                return dest;
            }

            var targetRadius = Mathf.Clamp(comp.ApproachRadius, minRadius, maxRadius);
            var originDist = origin.DistanceTo(center);
            var originError = Mathf.Abs(originDist - targetRadius);
            if (originError > 1f)
            {
                bool CloserToRing(IntVec3 c)
                {
                    if (!BaseValid(c)) { return false; }

                    return Mathf.Abs(c.DistanceTo(center) - targetRadius) < originError - 1f;
                }

                if (originDist > 0.5f)
                {
                    var radial = (origin - center).ToVector3();
                    radial.y = 0f;
                    radial.Normalize();
                    var step = Mathf.Min(wanderRadius, originError);
                    var dir = originDist > targetRadius ? -radial : radial;
                    var point = (origin.ToVector3Shifted() + dir * step).ToIntVec3().ClampInsideMap(map);
                    dest = CellFinder.StandableCellNear(point, map, 4f, CloserToRing);
                    if (dest.IsValid && dest != origin)
                    {
                        return dest;
                    }
                }

                dest = RCellFinder.RandomWanderDestFor(pawn, origin, wanderRadius, (p, c, r) => CloserToRing(c), Danger.Deadly);
                if (dest.IsValid && dest != origin && CloserToRing(dest))
                {
                    return dest;
                }
            }

            dest = RCellFinder.RandomWanderDestFor(pawn, origin, wanderRadius, (p, c, r) => BaseValid(c), Danger.Deadly);
            return dest.IsValid && dest != origin && BaseValid(dest) ? dest : IntVec3.Invalid;
        }

        private static Job ChargeJob(Pawn pawn, CompApocrypha comp, Thing target, float range, JobDef moveDef, bool allowSkip)
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

            if (!found && allowSkip && comp.TrySkipToStealTarget(target, range))
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

            return MakeMoveJob(moveDef, goal, ChargeRepathTicks.RandomInRange);
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
