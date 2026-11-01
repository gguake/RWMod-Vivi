using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VVRace
{
    public class HediffCompProperties_BloodLossRecoveryFactor : HediffCompProperties
    {
        // 단계별 혈액 손실 회복 배율: 1은 정상 회복, 0은 회복 중단. 목록이 짧으면 마지막 값을 사용한다.
        public List<float> factorByStage = new List<float>();

        public HediffCompProperties_BloodLossRecoveryFactor()
        {
            compClass = typeof(HediffComp_BloodLossRecoveryFactor);
        }
    }

    // 바닐라 HediffGiver_Bleeding은 출혈률 합이 0.1 미만이면 60틱마다 혈액 손실을 0.000333씩 회복한다.
    // 같은 주기에 회복량의 (1 - 배율)을 되돌려 멸망꽃 저주의 회복 감속을 적용한다.
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

        // Formatted는 첫 글자를 대문자로 바꿔 배율의 x가 X가 되므로 라벨만 번역한 뒤 수치를 붙인다.
        public override string CompTipStringExtra
            => "  - " + LocalizeString_EndingGimmicks.VV_Hediff_BloodLossRecoveryFactor.Translate().CapitalizeFirst()
                + ": " + Factor.ToStringByStyle(ToStringStyle.PercentZero, ToStringNumberSense.Factor);

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);

            // 바닐라 Pawn_HealthTracker와 같은 폰 해시로 60틱 주기를 맞춘다.
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
