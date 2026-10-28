using Verse;

namespace VVRace
{
    // 2페이즈 〈시조의 외피〉. 실제 피해 감소는 CompApocrypha가 처리하고, 이 헤디프는 현재 감소율 표시와 이동속도 보정만 맡는다.
    public class Hediff_ApocryphaCarapace : HediffWithComps
    {
        private CompApocrypha BossComp => pawn?.TryGetComp<CompApocrypha>();

        public override string LabelInBrackets
        {
            get
            {
                var comp = BossComp;
                if (comp == null) { return base.LabelInBrackets; }

                return LocalizeString_EndingBoss.VV_Hediff_ApocryphaCarapaceReduction.Translate(comp.DamageReduction.ToStringPercent());
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

                var stacks = LocalizeString_EndingBoss.VV_Hediff_ApocryphaCarapaceTip.Translate(comp.ResonanceStacks, comp.DamageReduction.ToStringPercent());
                return tip.NullOrEmpty() ? (string)stacks : tip + "\n" + stacks;
            }
        }
    }
}
