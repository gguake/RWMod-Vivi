using Verse;

namespace VVRace
{
    // 실제 피해 배율은 CompApocrypha가 적용한다. 이 헤디프는 꽂힌 바늘에 따른 배율 표시와 이동 속도 보정만 담당한다.
    public class Hediff_ApocryphaCarapace : HediffWithComps
    {
        private CompApocrypha BossComp => pawn?.TryGetComp<CompApocrypha>();

        public override string LabelInBrackets
        {
            get
            {
                var comp = BossComp;
                if (comp == null) { return base.LabelInBrackets; }

                return LocalizeString_EndingBoss.VV_Hediff_ApocryphaCarapaceDamageFactor.Translate(comp.IncomingDamageFactor.ToStringPercent());
            }
        }

        public override string SeverityLabel => null;

        public override string TipStringExtra
        {
            get
            {
                var comp = BossComp;
                var tip = base.TipStringExtra;
                if (comp == null) { return tip; }

                var needles = LocalizeString_EndingBoss.VV_Hediff_ApocryphaCarapaceTip.Translate(
                    comp.StuckNeedleCount, comp.Props.unboundNeedleCount, comp.IncomingDamageFactor.ToStringPercent());
                return tip.NullOrEmpty() ? (string)needles : tip + "\n" + needles;
            }
        }
    }
}
