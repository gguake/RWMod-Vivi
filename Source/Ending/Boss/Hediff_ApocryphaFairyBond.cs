using Verse;

namespace VVRace
{
    // 아포크리파의 〈요정 비비 결속〉. 플레이어 비비의 결속 헤디프와 표기만 같으며, 수치는 보스 컴프에서만 관리한다.
    // 14억이 넘는 값은 float 심각도로 표현할 수 없으므로(1 감소가 반영되지 않음) 심각도는 1로 고정하고 라벨만 덮어쓴다.
    public class Hediff_ApocryphaFairyBond : HediffWithComps
    {
        private CompApocrypha BossComp => pawn?.TryGetComp<CompApocrypha>();

        // 다른 괄호 문구(단계/컴프)가 붙지 않도록 완전히 덮어쓴다.
        public override string LabelInBrackets
        {
            get
            {
                var comp = BossComp;
                return comp == null ? null : comp.BondCount.ToString("N0");
            }
        }

        public override string SeverityLabel => null;

        public override string TipStringExtra
        {
            get
            {
                var comp = BossComp;
                if (comp == null) { return base.TipStringExtra; }

                return LocalizeString_EndingBoss.VV_Hediff_ApocryphaFairyBondTip.Translate(
                    comp.BondCount.ToString("N0"),
                    comp.InitialBondCount.ToString("N0"));
            }
        }
    }
}
