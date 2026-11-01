using Verse;

namespace VVRace
{
    // 2페이즈에 거대 엘텍스 바늘이 꽂힐 때마다 한 겹씩 쌓인다(최대 3). 겹 수는 CompApocrypha가 꽂힌 바늘 수에 맞춰 갱신하며,
    // 실제 받는 피해 배율도 CompApocrypha가 적용한다. 겹 수와 배율은 툴팁에 표시한다.
    public class Hediff_ApocryphaStuckNeedle : HediffWithComps
    {
        private CompApocrypha BossComp => pawn?.TryGetComp<CompApocrypha>();

        public override string SeverityLabel => null;

        public override string TipStringExtra
        {
            get
            {
                var comp = BossComp;
                var tip = base.TipStringExtra;
                if (comp == null) { return tip; }

                var needles = LocalizeString_EndingBoss.VV_Hediff_ApocryphaStuckNeedleTip.Translate(
                    comp.StuckNeedleCount, comp.Props.unboundNeedleCount, comp.IncomingDamageFactor.ToStringPercent());
                return tip.NullOrEmpty() ? (string)needles : tip + "\n" + needles;
            }
        }
    }
}
