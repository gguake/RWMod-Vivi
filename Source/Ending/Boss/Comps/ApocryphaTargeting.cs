using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class ApocryphaTargeting : IExposable
    {
        private const int EnemyDetectInterval = 30;
        private const int PursuitInterruptIntervalTicks = 300;

        private static readonly List<Thing> _tmpTargets = new List<Thing>();

        private readonly CompApocrypha _comp;

        private Thing _rangedAttacker;
        private int _rangedAttackerUntilTick = -1;
        private bool _pendingPursuitCheck;
        private int _lastPursuitInterruptTick = -99999;

        private Pawn Pawn => _comp.Pawn;
        private CompProperties_Apocrypha Props => _comp.Props;

        public Thing RangedAttacker
        {
            get
            {
                var attacker = _rangedAttacker;
                if (attacker == null || _comp.Phase != ApocryphaPhase.Bound || GenTicks.TicksGame > _rangedAttackerUntilTick) { return null; }
                if (attacker.Destroyed || !attacker.Spawned || attacker.Map != Pawn.Map) { return null; }
                if (attacker is Pawn pawn && (pawn.Dead || pawn.Downed)) { return null; }
                if (!Pawn.HostileTo(attacker)) { return null; }

                return attacker;
            }
        }

        public ApocryphaTargeting(CompApocrypha comp)
        {
            _comp = comp;
        }

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving && (_rangedAttacker == null || _rangedAttacker.Destroyed || !_rangedAttacker.Spawned))
            {
                _rangedAttacker = null;
            }

            Scribe_References.Look(ref _rangedAttacker, "rangedAttacker");
            Scribe_Values.Look(ref _rangedAttackerUntilTick, "rangedAttackerUntilTick", -1);
        }

        public void Tick(int now)
        {
            if (!_pendingPursuitCheck) { return; }

            _pendingPursuitCheck = false;
            if (_comp.Phase == ApocryphaPhase.Bound)
            {
                InterruptIdleJobForPursuit(now);
            }
        }

        // 가장 가까운 적이 바뀔 때마다 호위가 오가지 않도록 현재 적이 계속 보이면 지정 시간만 연장한다.
        public void TickEnemyDetection()
        {
            if (!Pawn.IsHashIntervalTick(EnemyDetectInterval)) { return; }
            if (!(ApocryphaUtility.FindEscortLord(Pawn)?.LordJob is LordJob_ApocryphaEscort escortJob)) { return; }

            var target = escortJob.FocusTarget;
            if (target == null || !IsTarget(target, Props.shellRange) || (!(target is Pawn) && AnyTargetPawn(Props.shellRange)))
            {
                target = FindNearestTarget();
            }

            if (target != null)
            {
                escortJob.SetFocus(target, GenTicks.TicksGame + Props.escortFocusTicks);
            }
        }

        public void NoteAttacker(Thing instigator)
        {
            if (instigator == null || instigator == Pawn || !instigator.Spawned || instigator.Map != Pawn.Map) { return; }
            if (instigator is Pawn pawn)
            {
                if (pawn.Dead || pawn.Downed || pawn.IsPsychologicallyInvisible()) { return; }
            }
            else if (!(instigator is Building_Turret) && !(instigator is ArcanePlant_Turret))
            {
                return;
            }

            if (!Pawn.HostileTo(instigator)) { return; }
            if (instigator != _rangedAttacker && IsTarget(instigator, Props.shellRange)) { return; }

            _rangedAttacker = instigator;
            _rangedAttackerUntilTick = GenTicks.TicksGame + Props.rangedAttackerMemoryTicks;
            _pendingPursuitCheck = true;
        }

        public bool TryFindPursuitCell(out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            var attacker = RangedAttacker;
            if (attacker == null || IsTarget(attacker, Props.shellRange)) { return false; }

            var map = Pawn.Map;
            var origin = Pawn.Position;
            var toBoss = (origin - attacker.Position).ToVector3();
            toBoss.y = 0f;
            toBoss = toBoss.sqrMagnitude > 0.01f ? toBoss.normalized : Vector3.forward;

            bool Reachable(IntVec3 c) => c.InBounds(map) && c != origin && Pawn.CanReach(c, PathEndMode.OnCell, Danger.Deadly);
            bool CanShellFrom(IntVec3 c) => Reachable(c) && (attacker is Pawn p ? IsTargetPawn(p, Props.shellRange, c) : IsTargetTurret(attacker, Props.shellRange, c));

            var maxDistance = Mathf.Min(Props.shellRange - 3f, origin.DistanceTo(attacker.Position));
            for (var distance = maxDistance; distance >= Props.shellMinRange + 1f; distance -= 2f)
            {
                var point = (attacker.Position.ToVector3Shifted() + toBoss * distance).ToIntVec3();
                cell = CellFinder.StandableCellNear(point, map, 3f, CanShellFrom);
                if (cell.IsValid) { return true; }
            }

            cell = CellFinder.StandableCellNear(attacker.Position, map, 6f, Reachable);
            return cell.IsValid;
        }

        public bool TryFindTargetCell(float range, System.Predicate<IntVec3> isAimed, System.Predicate<IntVec3> excluded, out IntVec3 cell)
        {
            CollectTargets(_tmpTargets, range, Pawn.Position);
            if (excluded != null)
            {
                _tmpTargets.RemoveAll(v => excluded(v.Position));
            }

            cell = _tmpTargets.Count > 0 ? PickAttackTarget(_tmpTargets, isAimed).Position : IntVec3.Invalid;

            _tmpTargets.Clear();
            return cell.IsValid;
        }

        private void InterruptIdleJobForPursuit(int now)
        {
            if (now - _lastPursuitInterruptTick < PursuitInterruptIntervalTicks || Pawn.stances.stunner.Stunned) { return; }

            var jobDef = Pawn.CurJobDef;
            if (jobDef != VVJobDefOf.VV_ApocryphaWait && jobDef != VVJobDefOf.VV_ApocryphaWander && jobDef != VVJobDefOf.VV_ApocryphaApproach) { return; }
            if (!TryFindPursuitCell(out _)) { return; }

            _lastPursuitInterruptTick = now;
            Pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
        }

        // 공격이 한곳에 몰리지 않도록 기존 조준에 걸린 적은 다른 후보가 없을 때만 선택한다. 최근 원거리 공격자를 우선한다.
        private Thing PickAttackTarget(List<Thing> candidates, System.Predicate<IntVec3> isAimed)
        {
            var attacker = RangedAttacker;
            if (attacker != null && candidates.Contains(attacker) && !isAimed(attacker.Position))
            {
                return attacker;
            }

            return candidates.Where(v => !isAimed(v.Position)).TryRandomElement(out var target) ? target : candidates.RandomElement();
        }

        private Thing FindNearestTarget()
        {
            CollectTargets(_tmpTargets, Props.shellRange, Pawn.Position);

            Thing best = null;
            var bestDistance = int.MaxValue;
            var origin = Pawn.Position;
            for (int i = 0; i < _tmpTargets.Count; i++)
            {
                var distance = (_tmpTargets[i].Position - origin).LengthHorizontalSquared;
                if (distance < bestDistance)
                {
                    best = _tmpTargets[i];
                    bestDistance = distance;
                }
            }

            _tmpTargets.Clear();
            return best;
        }

        private void CollectTargets(List<Thing> outTargets, float range, IntVec3 origin)
        {
            outTargets.Clear();

            var map = Pawn.Map;
            var pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                if (IsTargetPawn(pawns[i], range, origin))
                {
                    outTargets.Add(pawns[i]);
                }
            }

            if (outTargets.Count > 0) { return; }

            var attackTargets = map.listerThings.ThingsInGroup(ThingRequestGroup.AttackTarget);
            for (int i = 0; i < attackTargets.Count; i++)
            {
                if (!(attackTargets[i] is Pawn) && IsTargetTurret(attackTargets[i], range, origin))
                {
                    outTargets.Add(attackTargets[i]);
                }
            }
        }

        private bool AnyTargetPawn(float range)
        {
            var pawns = Pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                if (IsTargetPawn(pawns[i], range, Pawn.Position)) { return true; }
            }

            return false;
        }

        private bool IsTarget(Thing thing, float range)
        {
            if (thing == null || !thing.Spawned || thing.Map != Pawn.Map) { return false; }

            return thing is Pawn pawn ? IsTargetPawn(pawn, range, Pawn.Position) : IsTargetTurret(thing, range, Pawn.Position);
        }

        private bool IsTargetPawn(Pawn target, float range, IntVec3 origin)
        {
            if (target == Pawn || target.Dead || target.Downed || target.IsPsychologicallyInvisible()) { return false; }
            if ((target.Position - origin).LengthHorizontalSquared > range * range) { return false; }
            if (!Pawn.HostileTo(target)) { return false; }

            return GenSight.LineOfSight(origin, target.Position, Pawn.Map, skipFirstCell: true);
        }

        private bool IsTargetTurret(Thing building, float range, IntVec3 origin)
        {
            if (building.Destroyed) { return false; }
            if (!(building is Building_Turret) && !(building is ArcanePlant_Turret)) { return false; }
            if ((building.Position - origin).LengthHorizontalSquared > range * range) { return false; }
            if (!Pawn.HostileTo(building)) { return false; }

            return GenSight.LineOfSightToThing(origin, building, Pawn.Map, skipFirstCell: true);
        }
    }
}
