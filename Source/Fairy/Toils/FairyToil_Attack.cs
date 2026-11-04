using RimWorld;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class FairyToil_Attack : FairyToil_CurvedMove
    {
        private const float AttackCurveOffsetMin = 0.45f;
        private const float AttackCurveOffsetMax = 1.25f;
        private const float AttackSpeedGlobalFactor = 0.9f;
        private const float AttackSpeedFactorMin = 0.5f;
        private const float AttackSpeedFactorMax = 1.0f;

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

            // 이동 경로는 시작할 때 한 번 정해 저장되므로 곡률과 속도도 여기서만 뽑는다.
            float attackCurveOffset = Rand.Range(AttackCurveOffsetMin, AttackCurveOffsetMax);
            if (Rand.Value < 0.5f)
            {
                attackCurveOffset *= -1f;
            }
            float attackSpeedFactor = Rand.Range(AttackSpeedFactorMin, AttackSpeedFactorMax);

            SetMoveTarget(target.TrueCenter().Yto0(), DefaultSpeed * AttackSpeedGlobalFactor * attackSpeedFactor, attackCurveOffset);
            fairy.BeginAttackMotion();
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
            fairy.RegisterToilTrail(fairy.RealPosition);

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
