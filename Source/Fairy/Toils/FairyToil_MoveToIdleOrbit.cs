using UnityEngine;
using Verse;

namespace VVRace
{
    public class FairyToil_MoveToIdleOrbit : FairyToil_CurvedMove
    {
        private Vector3 targetPosition;
        private int teleportTicksLeft;
        private bool curvedReturn;
        private bool targetConfigured;
        private bool completeWhenNear;
        private float completionDistance = 0.08f;

        public FairyToil_MoveToIdleOrbit() { }

        public FairyToil_MoveToIdleOrbit(Vector3 targetPosition, float speed = DefaultSpeed)
        {
            this.targetPosition = targetPosition.Yto0();
            curvedReturn = true;
            targetConfigured = true;
        }

        public FairyToil_MoveToIdleOrbit(bool completeWhenNear, float completionDistance = 0.08f)
        {
            this.completeWhenNear = completeWhenNear;
            this.completionDistance = Mathf.Max(0.02f, completionDistance);
        }

        public void ConfigureStepTarget(Vector3 targetPosition)
        {
            this.targetPosition = targetPosition.Yto0();
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

            SetMoveTarget(targetPosition, DefaultSpeed);
            fairy.BeginToilMotion(FairyState.MovingToRest);
        }

        protected override FairyToilStatus TickAction(int delta)
        {
            if (curvedReturn)
            {
                var result = AdvanceMove(delta, registerTrail: false);
                if (result == FairyToilStatus.Complete)
                {
                    Fairy?.EnterIdleFromToil();
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
                    fairy.EnterIdleFromToil();
                }
                return FairyToilStatus.Running;
            }

            if (fairy.State == FairyState.Attacking || fairy.State == FairyState.MovingToRest)
            {
                fairy.EnterIdleFromToil();
            }
            if (fairy.State != FairyState.Idle) { return FairyToilStatus.Running; }

            Vector3 cur = fairy.RealPosition.Yto0();
            Vector3 tar = targetPosition.Yto0();
            Vector3 diff = tar - cur;
            float dist = diff.magnitude;

            if (dist > 9f)
            {
                var cell = tar.ToIntVec3();
                if (!cell.InBounds(fairy.Map))
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

            if (dist > 0.02f)
            {
                float step = 0.1f * delta;
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
            Scribe_Values.Look(ref completionDistance, "completionDistance", 0.08f);
        }
    }
}
