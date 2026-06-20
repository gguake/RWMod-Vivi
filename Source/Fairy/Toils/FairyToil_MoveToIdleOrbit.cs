using UnityEngine;
using Verse;

namespace VVRace
{
    public class FairyToil_MoveToIdleOrbit : FairyToil_CurvedMove
    {
        private const float MinCompletionDistance = 0.02f;
        private const float TeleportDistance = 9f;
        private const float StepPerTick = 0.1f;

        private Vector3 targetPosition;
        private int teleportTicksLeft;
        private bool curvedReturn;
        private bool targetConfigured;
        private bool completeWhenNear;
        private float speed = DefaultSpeed;
        private float completionDistance = 0.08f;

        public FairyToil_MoveToIdleOrbit() { }

        public FairyToil_MoveToIdleOrbit(Vector3 targetPosition, float speed = DefaultSpeed)
        {
            this.targetPosition = targetPosition.Yto0();
            this.speed = speed > 0f ? speed : DefaultSpeed;
            curvedReturn = true;
            targetConfigured = true;
        }

        public FairyToil_MoveToIdleOrbit(bool completeWhenNear, float completionDistance = 0.08f)
        {
            this.completeWhenNear = completeWhenNear;
            this.completionDistance = Mathf.Max(MinCompletionDistance, completionDistance);
        }

        public void ConfigureStepTarget(Vector3 targetPosition)
        {
            var fairy = Fairy;
            this.targetPosition = fairy != null ? fairy.ClampPositionToMap(targetPosition) : targetPosition.Yto0();
            targetConfigured = true;
        }

        public bool IsNearStepTarget(float maxDistance)
        {
            var fairy = Fairy;
            if (fairy == null || !targetConfigured)
            {
                return false;
            }

            return (targetPosition.Yto0() - fairy.RealPosition.Yto0()).magnitude <= maxDistance;
        }

        protected override void OnStarted()
        {
            if (!curvedReturn) { return; }

            var fairy = Fairy;
            if (fairy == null || fairy.Destroyed || fairy.Map == null) { return; }

            SetMoveTarget(targetPosition, speed);
            fairy.BeginToilMotion(FairyState.MovingToRest);
        }

        protected override FairyToilStatus TickAction(int delta)
        {
            if (curvedReturn)
            {
                var result = AdvanceMove(delta, registerTrail: false);
                if (result == FairyToilStatus.Complete)
                {
                    Fairy?.EnterIdle();
                }
                return result;
            }

            if (!targetConfigured || delta <= 0)
            {
                return FairyToilStatus.Running;
            }

            return TickFollowStep(delta);
        }

        private FairyToilStatus TickFollowStep(int delta)
        {
            var fairy = Fairy;
            if (fairy == null || fairy.Destroyed || fairy.Map == null) { return FairyToilStatus.Complete; }

            if (teleportTicksLeft > 0)
            {
                teleportTicksLeft -= delta;
                fairy.SetTimedStateTicks(Mathf.Max(0, teleportTicksLeft));
                if (teleportTicksLeft <= 0)
                {
                    fairy.EnterIdle();
                }
                return FairyToilStatus.Running;
            }

            if (fairy.State == FairyState.Attacking || fairy.State == FairyState.MovingToRest)
            {
                fairy.EnterIdle();
            }
            if (fairy.State != FairyState.Idle) { return FairyToilStatus.Running; }

            Vector3 cur = fairy.ClampPositionToMap(fairy.RealPosition);
            Vector3 tar = fairy.ClampPositionToMap(targetPosition);
            targetPosition = tar;
            Vector3 diff = tar - cur;
            float dist = diff.magnitude;

            if (dist > TeleportDistance)
            {
                var cell = tar.ToIntVec3();
                if (!cell.IsValid || !cell.InBounds(fairy.Map))
                {
                    var owner = fairy.Owner;
                    cell = owner != null && owner.Spawned ? owner.Position : fairy.Position;
                }
                fairy.TeleportTo(cell);
                teleportTicksLeft = ViviFairy.TeleportDurationTicks;
                return FairyToilStatus.Running;
            }

            if (completeWhenNear && dist <= completionDistance)
            {
                return FairyToilStatus.Complete;
            }

            if (dist > MinCompletionDistance)
            {
                float step = StepPerTick * delta;
                var next = cur + (step >= dist ? diff : diff.normalized * step);
                fairy.SetToilPosition(next, diff);
            }

            return FairyToilStatus.Running;
        }

        protected override FairyToilStatus OnArrived(Vector3 previousPosition)
        {
            return FairyToilStatus.Complete;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref targetPosition, "targetPosition");
            Scribe_Values.Look(ref teleportTicksLeft, "teleportTicksLeft");
            Scribe_Values.Look(ref curvedReturn, "curvedReturn");
            Scribe_Values.Look(ref targetConfigured, "targetConfigured");
            Scribe_Values.Look(ref completeWhenNear, "completeWhenNear");
            Scribe_Values.Look(ref speed, "speed", DefaultSpeed);
            Scribe_Values.Look(ref completionDistance, "completionDistance", 0.08f);
        }
    }
}
