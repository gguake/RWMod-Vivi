using RimWorld;
using RPEF;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VVRace
{
    public class NeedleProperties : ProjectileProperties
    {
        public SimpleCurve bonusSpeedByPsychicSensitivity;
        public int maxAttackCount;

        public float targettingRadius;
        public SimpleCurve bonusAttackCountByPsychicSensitivity;
    }

    public class Needle : Projectile
    {
        private const float RefreshInterval = 25;
        private const float BezierWeight = 20;

        public CompTrailRenderer TrailRenderer
        {
            get
            {
                if (_trailRenderer == null)
                {
                    _trailRenderer = GetComp<CompTrailRenderer>();
                }
                return _trailRenderer;
            }
        }
        [Unsaved]
        private CompTrailRenderer _trailRenderer;

        public NeedleProperties NeedleProperties
        {
            get
            {
                if (_needleProps == null)
                {
                    _needleProps = def.projectile as NeedleProperties;
                }
                return _needleProps;
            }
        }
        [Unsaved]
        private NeedleProperties _needleProps;

        private float _appliedSpeed;
        private int _remainedAttackCount;

        private Vector3 _initialTargettingPosition;

        private Vector3 _realPosition;
        private Vector3 _realDirection;

        private Thing _curTargetThing;
        private Vector3 _moveStartPosition;
        private Vector3 _moveEndPosition;
        private Vector3 _curDirectionOutVector;
        private Vector3 _curDirectionInVector;
        private float _totalMoveDistance;
        private float _curMoveDistance;
        private bool _isReturning;
        private Dictionary<int, int> _attackedCounter = new Dictionary<int, int>();

        public override Vector3 DrawPos => _realPosition;

        public override Vector3 ExactPosition => _realPosition;
        public override Quaternion ExactRotation => Quaternion.LookRotation(_realDirection);

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref _appliedSpeed, "appliedSpeed");
            Scribe_Values.Look(ref _remainedAttackCount, "remainedAttackCount");

            Scribe_Values.Look(ref _initialTargettingPosition, "initialTargettingPosition");
            Scribe_Values.Look(ref _realPosition, "realPosition");
            Scribe_Values.Look(ref _realDirection, "realDirection");

            Scribe_References.Look(ref _curTargetThing, "curTargetThing");
            Scribe_Values.Look(ref _moveStartPosition, "moveStartPosition");
            Scribe_Values.Look(ref _moveEndPosition, "moveEndPosition");
            Scribe_Values.Look(ref _curDirectionOutVector, "curDirectionOutVector");
            Scribe_Values.Look(ref _curDirectionInVector, "curDirectionInVector");
            Scribe_Values.Look(ref _totalMoveDistance, "totalMoveDistance");
            Scribe_Values.Look(ref _curMoveDistance, "curMoveDistance");
            Scribe_Values.Look(ref _isReturning, "isReturning");

            Scribe_Collections.Look(ref _attackedCounter, "attackedCounter", LookMode.Value, LookMode.Value);
        }

        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
        {
            usedTarget = intendedTarget;

            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);

            _realPosition = origin.Yto0();
            _realDirection = (usedTarget.Thing.TrueCenter() - launcher.TrueCenter()).Yto0().normalized;

            _initialTargettingPosition = usedTarget.Thing.PositionHeld.ToVector3();

            _curTargetThing = usedTarget.Thing;
            _moveStartPosition = _realPosition;
            _moveEndPosition = usedTarget.HasThing ? usedTarget.Thing.TrueCenter().Yto0() : usedTarget.Cell.ToVector3().Yto0();
            _curDirectionOutVector = _realDirection;
            _curDirectionInVector = (_curTargetThing.Position.ToVector3() - launcher.Position.ToVector3()).Yto0().normalized;
            _totalMoveDistance = CalculateBezierCurveLengthApproximate(_moveStartPosition, _moveEndPosition, _curDirectionOutVector * BezierWeight, _curDirectionInVector * BezierWeight);
            _curMoveDistance = 0f;

            var psychicSensitivityFactor = Mathf.Clamp(launcher.GetStatValue(StatDefOf.PsychicSensitivity), 0.5f, 10f);
            _appliedSpeed = NeedleProperties.speed + NeedleProperties.bonusSpeedByPsychicSensitivity.Evaluate(psychicSensitivityFactor);
            _remainedAttackCount = (int)(NeedleProperties.maxAttackCount + NeedleProperties.bonusAttackCountByPsychicSensitivity.Evaluate(psychicSensitivityFactor));
        }

        protected override void TickInterval(int delta)
        {
            if (!launcher.Spawned || launcher.Destroyed || (launcher is Pawn pawn && pawn.DeadOrDowned))
            {
                ReturnNeedle();
                return;
            }

            var totalCost = delta * _appliedSpeed;

            var position = _realPosition;
            while (totalCost > 0)
            {
                var moves = Mathf.Clamp(RefreshInterval, 0, totalCost);

                bool approach = false;
                if (moves >= _totalMoveDistance - _curMoveDistance)
                {
                    moves = _totalMoveDistance - _curMoveDistance;
                    approach = true;
                }

                if (approach)
                {
                    _realPosition = _moveEndPosition;
                    _realDirection = _curDirectionInVector;

                    TrailRenderer.RegisterNewTrail(position);

                    if (_isReturning)
                    {
                        ReturnNeedle();
                        return;
                    }
                    else
                    {
                        var previousTargetThing = _curTargetThing;
                        if (_curTargetThing != null)
                        {
                            Impact(_curTargetThing);
                        }

                        _curTargetThing = null;
                        if (_remainedAttackCount > 0)
                        {
                            _remainedAttackCount--;
                            SearchNewTarget(previousTargetThing, _moveEndPosition);
                        }

                        if (_curTargetThing == null)
                        {
                            if (launcher.Spawned && !launcher.Destroyed)
                            {
                                _isReturning = true;
                                SetTarget(launcher);
                            }
                            else
                            {
                                Destroy();
                            }
                        }
                    }
                }
                else
                {
                    position = CalculateBezierCurvePoint(_moveStartPosition, _moveEndPosition, _curDirectionOutVector * BezierWeight, _curDirectionInVector * BezierWeight, _curMoveDistance / _totalMoveDistance);
                    TrailRenderer.RegisterNewTrail(position);

                    _curMoveDistance += moves;
                }

                totalCost -= moves;
            }

            var positionCell = position.ToIntVec3();
            if (!positionCell.InBounds(Map))
            {
                ReturnNeedle();
            }
            else
            {
                Position = position.ToIntVec3();

                _realPosition = position;
                _realDirection = CalculateBezierCurveDerivative(_moveStartPosition, _moveEndPosition, _curDirectionOutVector * BezierWeight, _curDirectionInVector * BezierWeight, _curMoveDistance / _totalMoveDistance);
            }
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            if (!hitThing.Spawned) { return; }
            if (hitThing == launcher) { return; }
            if (hitThing.Map != Map) { return; }

            GenClamor.DoClamor(this, 12f, ClamorDefOf.Impact);

            BattleLogEntry_RangedImpact battleLogEntry_RangedImpact = new BattleLogEntry_RangedImpact(launcher, hitThing, intendedTarget.Thing, equipmentDef, def, targetCoverDef);
            Find.BattleLog.Add(battleLogEntry_RangedImpact);

            if (hitThing != null)
            {
                bool instigatorGuilty = !(launcher is Pawn pawn) || !pawn.Drafted;
                DamageInfo dinfo = new DamageInfo(
                    DamageDef, 
                    DamageAmount, 
                    ArmorPenetration, 
                    ExactRotation.eulerAngles.y, 
                    launcher, 
                    null, 
                    equipmentDef, 
                    DamageInfo.SourceCategory.ThingOrUnknown, 
                    intendedTarget.Thing, 
                    instigatorGuilty);

                dinfo.SetWeaponQuality(equipmentQuality);
                hitThing.TakeDamage(dinfo).AssociateWithLog(battleLogEntry_RangedImpact);

                if (_attackedCounter.TryGetValue(hitThing.thingIDNumber, out var count))
                {
                    _attackedCounter[hitThing.thingIDNumber] = count++;
                }
                else
                {
                    _attackedCounter.Add(hitThing.thingIDNumber, 1);
                }
            }
        }

        private void SetTarget(Thing thing)
        {
            var nearEdges = thing.OccupiedRect().ExpandedBy(1).EdgeCellsNoCorners.Where(v => v.InBounds(Map));
            
            _curTargetThing = thing;
            _moveStartPosition = _realPosition;
            _moveEndPosition = thing.TrueCenter().Yto0();

            _curDirectionOutVector = _realDirection;
            _curDirectionInVector = (thing.TrueCenter() - nearEdges.RandomElement().ToVector3()).Yto0();
            _totalMoveDistance = CalculateBezierCurveLengthApproximate(_moveStartPosition, _moveEndPosition, _curDirectionOutVector * BezierWeight, _curDirectionInVector * BezierWeight);
            _curMoveDistance = 0f;
        }

        private void SearchNewTarget(Thing previousTarget, Vector3 targetSearchPosition)
        {
            var caster = launcher as IAttackTargetSearcher;
            var cellPosition = targetSearchPosition.ToIntVec3();

            var potentialTargets = Map.attackTargetsCache.GetPotentialTargetsFor(caster);

            var tmp = (_initialTargettingPosition - targetSearchPosition).sqrMagnitude;
            var target = GenClosest.ClosestThing_Global(
                cellPosition,
                potentialTargets,
                maxDistance: NeedleProperties.targettingRadius,
                validator: (Thing t) =>
                {
                    if (t == previousTarget) { return false; }

                    var attackTarget = t as IAttackTarget;
                    if (attackTarget == null) { return false; }

                    if (!t.Spawned) { return false; }
                    if (t.Fogged()) { return false; }
                    if (t.HostileTo(launcher.Faction) == false) { return false; }
                    if (GenSight.LineOfSightToThing(launcher.PositionHeld, t, Map, true) == false) { return false; }
                    if (GenHostility.IsActiveThreatTo(attackTarget, launcher.Faction) == false) { return false; }
                    
                    return true;
                },
                priorityGetter: (Thing t) => 
                    (_attackedCounter.TryGetValue(t.thingIDNumber, out var attackedCount) ? attackedCount * -12 : 0) + 
                    Mathf.Abs((int)((t.Position.ToVector3() - targetSearchPosition).sqrMagnitude - tmp)) - 
                    launcher.Position.DistanceToSquared(t.Position));

            if (target != null)
            {
                SetTarget(target);
            }
            else
            {
                if (previousTarget != null &&
                    previousTarget.Spawned &&
                    previousTarget is IAttackTarget previousAttackTarget &&
                    previousTarget.Fogged() == false &&
                    previousTarget.Position.DistanceTo(cellPosition) < NeedleProperties.targettingRadius &&
                    GenSight.LineOfSightToThing(launcher.PositionHeld, previousTarget, Map, true) &&
                    GenHostility.IsActiveThreatTo(previousAttackTarget, launcher.Faction))
                {
                    SetTarget(previousTarget);
                }
            }

        }

        private void ReturnNeedle()
        {
            var pawn = launcher as Pawn;
            if (pawn.Spawned && !pawn.DeadOrDowned)
            {
                var stance = pawn?.stances?.curStance as Stance_Cooldown;
                if (stance != null && stance.verb is Verb_LaunchProjectile)
                {
                    pawn.stances.CancelBusyStanceHard();
                }
            }

            Destroy();
        }

        private const int BezierLengthInterval = 4;
        private float CalculateBezierCurveLengthApproximate(Vector3 p1, Vector3 p2, Vector3 w1, Vector3 w2)
        {
            var distance = 0f;

            var point = p1;
            for (int i = 1; i < BezierLengthInterval; ++i)
            {
                var newPoint = CalculateBezierCurvePoint(p1, p2, w1, w2, i / (float)BezierLengthInterval);
                distance += (newPoint - point).magnitude;
                point = newPoint;
            }

            distance += (p2 - point).magnitude;
            return distance * 100;
        }

        private Vector3 CalculateBezierCurvePoint(Vector3 p1, Vector3 p2, Vector3 w1, Vector3 w2, float t)
        {
            var tInv = 1 - t;
            var c1 = p1 + w1 / 3f;
            var c2 = p2 - w2 / 3f;

            /// p1, w1 : start coordinate, derivative
            /// p2, w2 : dest coordinate, derivative
            /// t : [0,1]
            return Mathf.Pow(tInv, 3) * p1 +
                3 * tInv * tInv * t * c1 +
                3 * tInv * t * t * c2 +
                t * t * t * p2;
        }

        private Vector3 CalculateBezierCurveDerivative(Vector3 p1, Vector3 p2, Vector3 w1, Vector3 w2, float t)
        {
            var tInv = 1 - t;
            var c1 = p1 + w1 / 3f;
            var c2 = p2 - w2 / 3f;

            /// p1, w1 : start coordinate, derivative
            /// p2, w2 : dest coordinate, derivative
            /// t : [0,1]
            return -3 * tInv * tInv * p1 +
                3 * tInv * (1 - 3 * t) * c1 + 
                3 * t * (2 - 3 * t) * c2 + 
                3 * t * t * p2;
        }
    }
}
