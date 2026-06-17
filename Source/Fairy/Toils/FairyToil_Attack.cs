using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class FairyToil_Attack : FairyToil_CurvedMove
    {
        private Thing target;
        private bool invalid;

        public FairyToil_Attack() { }

        public FairyToil_Attack(Thing target)
        {
            this.target = target;
        }

        protected override void OnStarted()
        {
            var fairy = Fairy;
            if (fairy == null || fairy.Destroyed || fairy.Map == null || target == null)
            {
                invalid = true;
                return;
            }

            SetMoveTarget(target.TrueCenter().Yto0(), DefaultSpeed);
            fairy.BeginToilMotion(FairyState.Attacking);
        }

        protected override FairyToilStatus TickAction(int delta)
        {
            if (invalid)
            {
                return FairyToilStatus.Complete;
            }

            var fairy = Fairy;
            if (fairy == null || fairy.Destroyed || !fairy.Spawned || fairy.Map == null)
            {
                return FairyToilStatus.Complete;
            }
            if (fairy.State == FairyState.Dematerializing)
            {
                return FairyToilStatus.Complete;
            }

            return AdvanceMove(delta, registerTrail: true);
        }

        protected override FairyToilStatus OnArrived(Vector3 previousPosition)
        {
            var fairy = Fairy;
            if (fairy == null || fairy.Destroyed)
            {
                return FairyToilStatus.Complete;
            }

            fairy.RegisterToilTrail(previousPosition);

            var hit = target;
            target = null;
            if (hit != null && hit.Spawned && hit.Map == fairy.Map)
            {
                fairy.ImpactToilTarget(hit);
            }

            return FairyToilStatus.Complete;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref target, "target");
            Scribe_Values.Look(ref invalid, "invalid");
        }
    }
}
