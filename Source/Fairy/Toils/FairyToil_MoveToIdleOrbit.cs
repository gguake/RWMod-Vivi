using UnityEngine;
using Verse;

namespace VVRace
{
    public class FairyToil_MoveToIdleOrbit : FairyToil_CurvedMove
    {
        private Vector3 targetPosition;
        private int teleportTicksLeft;
        private bool curvedReturn;

        public FairyToil_MoveToIdleOrbit() { }

        public FairyToil_MoveToIdleOrbit(Vector3 targetPosition, float speed = DefaultSpeed)
        {
            this.targetPosition = targetPosition.Yto0();
            curvedReturn = true;
        }

        public void ConfigureStepTarget(Vector3 targetPosition)
        {
            this.targetPosition = targetPosition.Yto0();
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

            TickFollowStep(delta);
            return FairyToilStatus.Running;
        }

        private void TickFollowStep(int delta)
        {
            var fairy = Fairy;
            if (fairy == null || fairy.Destroyed || fairy.Map == null) { return; }

            if (teleportTicksLeft > 0)
            {
                teleportTicksLeft -= delta;
                fairy.SetTimedStateTicks(Mathf.Max(0, teleportTicksLeft));
                if (teleportTicksLeft <= 0)
                {
                    fairy.EnterIdleFromToil();
                }
                return;
            }

            if (fairy.State != FairyState.Idle) { return; }

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
                return;
            }

            if (dist > 0.02f)
            {
                float step = 0.1f * delta;
                var next = cur + (step >= dist ? diff : diff.normalized * step);
                fairy.SetToilPosition(next, diff);
            }
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
        }
    }
}
