using RimWorld;
using RPEF;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Noise;

namespace VVRace
{
    public class NeedleProperties : ProjectileProperties
    {
        public int maxAttackCount;
        public float targettingRadius;
        public IntRange overrunTicks;

        public float maxAngleVelocity;
        public float forceDirectingRadiusSqr;

        public int maxTargettingTicks = 1000;
    }

    public class Needle : Projectile
    {
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
        private CompTrailRenderer _trailRenderer;

        private Vector3 _realPosition;
        private Vector3 _realDirection;

        private Thing _curTargetThing;
        private Vector3 _moveStartPosition;
        private Vector3 _moveEndPosition;
        private Vector3 _curDirectionOutVector;
        private Vector3 _curDirectionInVector;
        private float _totalMoveDistance;
        private float _curMoveDistance;

        public override Vector3 DrawPos => _realPosition;

        public override Vector3 ExactPosition => _realPosition;
        public override Quaternion ExactRotation => Quaternion.LookRotation(_realDirection);

        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
        {
            usedTarget = intendedTarget;

            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);

            _realPosition = launcher.Position.ToVector3().Yto0();
            _realDirection = (usedTarget.Thing.TrueCenter() - launcher.TrueCenter()).Yto0().normalized;

            _curTargetThing = usedTarget.Thing;
            _moveStartPosition = _realPosition;
            _moveEndPosition = usedTarget.HasThing ? usedTarget.Thing.TrueCenter().Yto0() : usedTarget.Cell.ToVector3().Yto0();
            _curDirectionOutVector = _realDirection;
            _curDirectionInVector = (_curTargetThing.Position.ToVector3() - launcher.Position.ToVector3()).Yto0().normalized;
            _totalMoveDistance = CalculateBezierCurveLengthApproximate(_moveStartPosition, _moveEndPosition, _curDirectionOutVector, _curDirectionInVector);
            _curMoveDistance = 0f;
        }

        protected override void TickInterval(int delta)
        {
            var totalCost = delta * def.projectile.speed;

            var position = _realPosition;
            while (totalCost > 0)
            {
                var moves = Mathf.Clamp(10, 0, totalCost);

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

                    var previousTargetThing = _curTargetThing;
                    Impact(_curTargetThing);
                    _curTargetThing = null;

                    SearchNewTarget(previousTargetThing, _moveEndPosition);

                    if (_curTargetThing == null)
                    {
                        Destroy();
                    }
                }
                else
                {
                    position = CalculateBezierCurvePoint(_moveStartPosition, _moveEndPosition, _curDirectionOutVector, _curDirectionInVector, _curMoveDistance / _totalMoveDistance);
                    TrailRenderer.RegisterNewTrail(position);

                    _curMoveDistance += moves;
                }

                totalCost -= moves;
            }

            _realPosition = position;
            _realDirection = CalculateBezierCurveDerivative(_moveStartPosition, _moveEndPosition, _curDirectionOutVector, _curDirectionInVector, _curMoveDistance / _totalMoveDistance);
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
            }

            Log.Message($"impact to : {hitThing}");
        }

        protected override void ImpactSomething()
        {
            base.ImpactSomething();
        }

        private void SetTarget(Thing thing)
        {
            var nearEdges = thing.OccupiedRect().ExpandedBy(1).EdgeCellsNoCorners.Where(v => v.InBounds(Map));
            
            _curTargetThing = thing;
            _moveStartPosition = _realPosition;
            _moveEndPosition = thing.TrueCenter().Yto0();
            _curDirectionOutVector = _realDirection;
            _curDirectionInVector = (thing.TrueCenter() - nearEdges.RandomElement().ToVector3()).Yto0();
            _totalMoveDistance = CalculateBezierCurveLengthApproximate(_moveStartPosition, _moveEndPosition, _curDirectionOutVector, _curDirectionInVector);
            _curMoveDistance = 0f;
        }

        private void SearchNewTarget(Thing previousTarget, Vector3 targetSearchPosition)
        {
            var caster = launcher as IAttackTargetSearcher;
            var cellPosition = targetSearchPosition.ToIntVec3();
            foreach (var offset in GenRadial.RadialPatternInRadius(6f))
            {
                if (offset == IntVec3.Zero) { continue; }

                var cell = offset + cellPosition;
                if (!cell.InBounds(Map)) { continue; }

                var things = cellPosition.GetThingList(Map);
                foreach (var thing in things)
                {
                    if (thing is IAttackTarget attackTarget && 
                        !thing.Fogged() && 
                        GenSight.LineOfSightToThing(launcher.PositionHeld, thing, Map, true) &&
                        GenHostility.IsActiveThreatTo(attackTarget, launcher.Faction) &&
                        !attackTarget.ThreatDisabled(caster))
                    {
                        SetTarget(thing);
                        return;
                    }
                }
            };

            if (previousTarget is IAttackTarget previousAttackTarget &&
                !previousTarget.Fogged() &&
                GenSight.LineOfSightToThing(launcher.PositionHeld, previousTarget, Map, true) &&
                GenHostility.IsActiveThreatTo(previousAttackTarget, launcher.Faction) &&
                !previousAttackTarget.ThreatDisabled(caster))
            {
                SetTarget(previousTarget);
                return;
            }
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
            return distance;
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
