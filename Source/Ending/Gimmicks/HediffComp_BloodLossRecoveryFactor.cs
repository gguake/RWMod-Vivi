using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class HediffCompProperties_BloodLossRecoveryFactor : HediffCompProperties
    {
        // 단계별 혈액 손실 회복 속도 배율(1 = 그대로, 0 = 회복하지 않음). 단계 수보다 짧으면 마지막 값을 쓴다.
        public List<float> factorByStage = new List<float>();

        public HediffCompProperties_BloodLossRecoveryFactor()
        {
            compClass = typeof(HediffComp_BloodLossRecoveryFactor);
        }
    }

    // 혈액 손실 회복을 늦춘다(멸망꽃 저주: 역병). 바닐라 HediffGiver_Bleeding은 출혈률 합이 0.1 미만인 동안
    // 폰의 60틱 간격마다 혈액 손실을 0.000333씩 줄이므로, 같은 간격으로 그 회복량의 (1 - 배율)만큼 되돌린다.
    public class HediffComp_BloodLossRecoveryFactor : HediffComp
    {
        private const int RecoveryIntervalTicks = 60;
        private const float VanillaRecoveryPerInterval = 0.00033333333f;
        private const float VanillaBleedingThreshold = 0.1f;

        public HediffCompProperties_BloodLossRecoveryFactor Props => (HediffCompProperties_BloodLossRecoveryFactor)props;

        public float Factor
        {
            get
            {
                var factors = Props.factorByStage;
                if (factors.NullOrEmpty()) { return 1f; }

                return factors[Mathf.Clamp(parent.CurStageIndex, 0, factors.Count - 1)];
            }
        }

        // 바닐라 단계 효과 줄("  - 항체 생성 속도: x50%")과 같은 모양으로 쓴다.
        // 값을 번역 인자로 넣으면 Formatted가 문장 첫 글자를 대문자로 바꿔 "X20%"가 되므로 라벨만 번역해 잇는다.
        public override string CompTipStringExtra
            => "  - " + LocalizeString_EndingGimmicks.VV_Hediff_BloodLossRecoveryFactor.Translate().CapitalizeFirst()
                + ": " + Factor.ToStringByStyle(ToStringStyle.PercentZero, ToStringNumberSense.Factor);

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);

            // 바닐라 회복과 같은 틱에 맞춘다(Pawn_HealthTracker도 폰 해시로 60틱 간격을 센다).
            if (!Pawn.IsHashIntervalTick(RecoveryIntervalTicks, delta)) { return; }

            var slowdown = 1f - Factor;
            if (slowdown <= 0f) { return; }

            var hediffSet = Pawn.health.hediffSet;
            if (hediffSet.BleedRateTotal >= VanillaBleedingThreshold) { return; }

            var bloodLoss = hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
            if (bloodLoss == null) { return; }

            bloodLoss.Severity += VanillaRecoveryPerInterval * Mathf.Min(slowdown, 1f);
        }
    }
}
