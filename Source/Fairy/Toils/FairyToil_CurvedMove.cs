using UnityEngine;
using Verse;

namespace VVRace
{
    public abstract class FairyToil_CurvedMove : FairyToil
    {
        protected const float DefaultSpeed = 40f;
        private const float RefreshInterval = 10f;
        private const float BezierWeight = 20f;
        private const int BezierLengthInterval = 4;

        private Vector3 moveStartPosition;
        private Vector3 moveEndPosition;
        private Vector3 curDirectionOutVector;
        private Vector3 curDirectionInVector;
        private float totalMoveDistance;
        private float curMoveDistance;
        private float appliedSpeed = DefaultSpeed;
        private bool moveInitialized;

        protected void SetMoveTarget(Vector3 end, float speed, float? curveOffsetOverride = null)
        {
            var fairy = Fairy;
            if (fairy == null || fairy.Destroyed)
            {
                moveInitialized = false;
                return;
            }

            moveStartPosition = fairy.RealPosition.Yto0();
            moveEndPosition = end.Yto0();
            appliedSpeed = speed > 0f ? speed : DefaultSpeed;

            var dir = moveEndPosition - moveStartPosition;
            if (dir.sqrMagnitude < 0.0001f) { dir = fairy.RealDirection; }
            if (dir.sqrMagnitude < 0.0001f) { dir = Vector3.forward; }
            dir = dir.normalized;

            curDirectionOutVector = fairy.RealDirection.sqrMagnitude > 0.0001f ? fairy.RealDirection.normalized : dir;
            curDirectionInVector = dir;
            ApplyFairyCurveVariation(fairy, dir, curveOffsetOverride);
            totalMoveDistance = CalculateBezierCurveLengthApproximate(
                moveStartPosition,
                moveEndPosition,
                curDirectionOutVector * BezierWeight,
                curDirectionInVector * BezierWeight);
            curMoveDistance = 0f;
            moveInitialized = true;
        }

        private void ApplyFairyCurveVariation(ViviFairy fairy, Vector3 baseDirection, float? curveOffsetOverride)
        {
            if (fairy == null)
            {
                return;
            }

            var side = new Vector3(-baseDirection.z, 0f, baseDirection.x);
            if (side.sqrMagnitude < 0.0001f)
            {
                return;
            }

            side.Normalize();
            float offset = curveOffsetOverride.HasValue ? curveOffsetOverride.Value : fairy.MotionCurveOffsetFactor;
            curDirectionOutVector = (curDirectionOutVector + side * offset).normalized;
            curDirectionInVector = (curDirectionInVector - side * offset * 0.65f).normalized;
        }

        protected FairyToilStatus AdvanceMove(int delta, bool registerTrail)
        {
            var fairy = Fairy;
            if (!moveInitialized || fairy == null || fairy.Destroyed || !fairy.Spawned || fairy.Map == null)
            {
                return FairyToilStatus.Complete;
            }

            float totalCost = delta * appliedSpeed;
            var position = fairy.RealPosition;

            while (totalCost > 0f)
            {
                float moves = Mathf.Clamp(RefreshInterval, 0f, totalCost);
                bool approach = false;
                float remaining = totalMoveDistance - curMoveDistance;
                if (moves >= remaining)
                {
                    moves = remaining;
                    approach = true;
                }

                if (approach)
                {
                    fairy.SetToilPosition(moveEndPosition, curDirectionInVector);
                    return OnArrived(position);
                }

                position = CalculateBezierCurvePoint(
                    moveStartPosition,
                    moveEndPosition,
                    curDirectionOutVector * BezierWeight,
                    curDirectionInVector * BezierWeight,
                    curMoveDistance / totalMoveDistance);

                if (registerTrail)
                {
                    fairy.RegisterToilTrail(position);
                }

                curMoveDistance += moves;
                totalCost -= moves;
            }

            var cell = position.ToIntVec3();
            if (!cell.InBounds(fairy.Map))
            {
                fairy.SetToilPosition(position, fairy.RealDirection);
                return OnOutOfBounds(position);
            }

            var direction = CalculateBezierCurveDerivative(
                moveStartPosition,
                moveEndPosition,
                curDirectionOutVector * BezierWeight,
                curDirectionInVector * BezierWeight,
                curMoveDistance / totalMoveDistance);
            fairy.SetToilPosition(position, direction);
            return FairyToilStatus.Running;
        }

        protected abstract FairyToilStatus OnArrived(Vector3 previousPosition);

        protected virtual FairyToilStatus OnOutOfBounds(Vector3 position)
        {
            return FairyToilStatus.Complete;
        }

        public override void Cancel()
        {
            var fairy = Fairy;
            if (fairy != null && (fairy.State == FairyState.Attacking || fairy.State == FairyState.MovingToRest))
            {
                fairy.EnterIdle();
            }
        }

        private static float CalculateBezierCurveLengthApproximate(Vector3 p1, Vector3 p2, Vector3 w1, Vector3 w2)
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
            return Mathf.Max(0.0001f, distance * 100f);
        }

        private static Vector3 CalculateBezierCurvePoint(Vector3 p1, Vector3 p2, Vector3 w1, Vector3 w2, float t)
        {
            var tInv = 1 - t;
            var c1 = p1 + w1 / 3f;
            var c2 = p2 - w2 / 3f;
            return Mathf.Pow(tInv, 3) * p1 +
                3 * tInv * tInv * t * c1 +
                3 * tInv * t * t * c2 +
                t * t * t * p2;
        }

        private static Vector3 CalculateBezierCurveDerivative(Vector3 p1, Vector3 p2, Vector3 w1, Vector3 w2, float t)
        {
            var tInv = 1 - t;
            var c1 = p1 + w1 / 3f;
            var c2 = p2 - w2 / 3f;
            return -3 * tInv * tInv * p1 +
                3 * tInv * (1 - 3 * t) * c1 +
                3 * t * (2 - 3 * t) * c2 +
                3 * t * t * p2;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref moveStartPosition, "moveStartPosition");
            Scribe_Values.Look(ref moveEndPosition, "moveEndPosition");
            Scribe_Values.Look(ref curDirectionOutVector, "curDirectionOutVector");
            Scribe_Values.Look(ref curDirectionInVector, "curDirectionInVector");
            Scribe_Values.Look(ref totalMoveDistance, "totalMoveDistance");
            Scribe_Values.Look(ref curMoveDistance, "curMoveDistance");
            Scribe_Values.Look(ref appliedSpeed, "appliedSpeed", DefaultSpeed);
            Scribe_Values.Look(ref moveInitialized, "moveInitialized");
        }
    }
}
