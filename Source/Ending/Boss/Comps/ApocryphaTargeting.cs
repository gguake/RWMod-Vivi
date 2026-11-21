using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class ApocryphaTargeting : IExposable
    {
        private const int EnemyDetectInterval = 30;
        private const int PursuitInterruptIntervalTicks = 300;
        private const float MinPursuitDistance = 3.9f;

        private static readonly List<Thing> _tmpTargets = new List<Thing>();

        private readonly CompApocrypha _comp;

        private Thing _rangedAttacker;
        private int _rangedAttackerUntilTick = -1;
        private Pawn _engageTarget;
        private int _lastCombatTick = -99999;
        private bool _pendingPursuitCheck;
        private int _lastPursuitInterruptTick = -99999;

        private Pawn Pawn => _comp.Pawn;
        private CompProperties_Apocrypha Props => _comp.Props;

        public Thing RangedAttacker
        {
            get
            {
                var attacker = _rangedAttacker;
                if (attacker == null || _comp.Phase != ApocryphaPhase.Asphodel || GenTicks.TicksGame > _rangedAttackerUntilTick) { return null; }
                if (attacker.Destroyed || !attacker.Spawned || attacker.Map != Pawn.Map) { return null; }
                if (attacker is Pawn pawn && (pawn.Dead || pawn.Downed)) { return null; }
                if (!Pawn.HostileTo(attacker)) { return null; }

                return attacker;
            }
        }

        public Pawn EngageTarget => IsTarget(_engageTarget, Props.engageRange) ? _engageTarget : null;

        public bool InCombat => GenTicks.TicksGame - _lastCombatTick <= Props.combatMemoryTicks;

        public ApocryphaTargeting(CompApocrypha comp)
        {
            _comp = comp;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref _rangedAttacker, "rangedAttacker");
            Scribe_Values.Look(ref _rangedAttackerUntilTick, "rangedAttackerUntilTick", -1);
            Scribe_References.Look(ref _engageTarget, "engageTarget");
            Scribe_Values.Look(ref _lastCombatTick, "lastCombatTick", -99999);
        }

        public void Tick(int now)
        {
            if (!_pendingPursuitCheck) { return; }

            _pendingPursuitCheck = false;
            if (_comp.Phase == ApocryphaPhase.Asphodel)
            {
                InterruptIdleJobForPursuit(now);
            }
        }

        public void TickEnemyDetection()
        {
            if (!Pawn.IsHashIntervalTick(EnemyDetectInterval)) { return; }

            var escortJob = ApocryphaUtility.FindEscortLord(Pawn)?.LordJob as LordJob_ApocryphaEscort;
            var target = escortJob?.FocusTarget;
            if (target == null || !IsTarget(target, Props.engageRange) || (!(target is Pawn) && AnyTargetPawn(Props.engageRange)))
            {
                target = FindNearestTarget();
            }

            if (target == null) { return; }

            if (_comp.Phase == ApocryphaPhase.Asphodel)
            {
                _lastCombatTick = GenTicks.TicksGame;
            }

            escortJob?.SetFocus(target, GenTicks.TicksGame + Props.escortFocusTicks);
        }

        public void TickQueenEngagement()
        {
            if (!Pawn.IsHashIntervalTick(EnemyDetectInterval)) { return; }

            CollectTargets(_tmpTargets, Props.queenEngageRadius, Pawn.Position);

            Pawn nearest = null;
            var nearestDistance = int.MaxValue;
            var origin = Pawn.Position;
            for (int i = 0; i < _tmpTargets.Count; i++)
            {
                if (!(_tmpTargets[i] is Pawn pawn)) { continue; }

                var distance = (pawn.Position - origin).LengthHorizontalSquared;
                if (distance < nearestDistance)
                {
                    nearest = pawn;
                    nearestDistance = distance;
                }
            }

            _tmpTargets.Clear();

            _engageTarget = nearest ?? EngageTarget;

            if (_engageTarget != null)
            {
                _lastCombatTick = GenTicks.TicksGame;
            }
        }

        public void ClearEngageTarget()
        {
            _engageTarget = null;
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

            _lastCombatTick = GenTicks.TicksGame;
            if (_comp.Phase != ApocryphaPhase.Asphodel) { return; }
            if (instigator != _rangedAttacker && IsTarget(instigator, Props.engageRange)) { return; }

            _rangedAttacker = instigator;
            _rangedAttackerUntilTick = GenTicks.TicksGame + Props.rangedAttackerMemoryTicks;
            _pendingPursuitCheck = true;
        }

        public bool TryFindPursuitCell(out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            var attacker = RangedAttacker;
            if (attacker == null || IsTarget(attacker, Props.engageRange)) { return false; }

            var map = Pawn.Map;
            var origin = Pawn.Position;
            var toBoss = EndingVfxUtility.SafeNormalizeHorizontal((origin - attacker.Position).ToVector3(), Vector3.forward);

            bool Reachable(IntVec3 c) => c.InBounds(map) && c != origin && Pawn.CanReach(c, PathEndMode.OnCell, Danger.Deadly);
            bool CanAttackFrom(IntVec3 c) => Reachable(c) && (attacker is Pawn p ? IsTargetPawn(p, Props.engageRange, c) : IsTargetTurret(attacker, Props.engageRange, c));

            var maxDistance = Mathf.Min(Props.engageRange - 3f, origin.DistanceTo(attacker.Position));
            for (var distance = maxDistance; distance >= MinPursuitDistance; distance -= 2f)
            {
                var point = (attacker.Position.ToVector3Shifted() + toBoss * distance).ToIntVec3();
                cell = CellFinder.StandableCellNear(point, map, 3f, CanAttackFrom);
                if (cell.IsValid) { return true; }
            }

            cell = CellFinder.StandableCellNear(attacker.Position, map, 6f, Reachable);
            return cell.IsValid;
        }

        public bool TryFindTargetCells(float range, out IntVec3 first, out IntVec3 second)
        {
            CollectTargets(_tmpTargets, range, Pawn.Position);
            first = IntVec3.Invalid;
            second = IntVec3.Invalid;
            if (_tmpTargets.Count > 0)
            {
                var attacker = RangedAttacker;
                var picked = attacker != null && _tmpTargets.Contains(attacker) ? attacker : _tmpTargets.RandomElement();
                _tmpTargets.Remove(picked);
                first = picked.Position;
                if (_tmpTargets.TryRandomElement(out var other))
                {
                    second = other.Position;
                }
            }

            _tmpTargets.Clear();
            return first.IsValid;
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

        private Thing FindNearestTarget()
        {
            CollectTargets(_tmpTargets, Props.engageRange, Pawn.Position);

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

        internal bool IsTargetPawn(Pawn target, float range, IntVec3 origin)
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
