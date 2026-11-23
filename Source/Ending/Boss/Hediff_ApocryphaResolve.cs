using UnityEngine;
using Verse;

namespace VVRace
{
    public class Hediff_ApocryphaResolve : HediffWithComps
    {
        public override string SeverityLabel => null;

        public int Stacks => Mathf.Clamp(Mathf.RoundToInt(Severity), 1, Mathf.RoundToInt(def.maxSeverity));

        public override string LabelInBrackets => Stacks > 1 ? $"x{Stacks}" : null;

        public override string TipStringExtra
        {
            get
            {
                var tip = base.TipStringExtra;
                var comp = pawn?.TryGetComp<CompApocrypha>();
                var stacks = Stacks;
                var maxStacks = comp?.Props.resolveMaxStacks ?? Mathf.RoundToInt(def.maxSeverity);
                var needleDamageBonus = stacks * (comp?.Props.resolveNeedleDamagePerStack ?? 0.5f);
                var needleAimReduction = stacks * (comp?.Props.resolveNeedleAimSpeedPerStack ?? 0.15f);

                var extra = LocalizeString_Etc.VV_Hediff_ApocryphaResolveTip.Translate(
                    stacks,
                    maxStacks,
                    needleDamageBonus.ToStringPercent(),
                    needleAimReduction.ToStringPercent());
                return EndingVfxUtility.AppendTipLine(tip, extra);
            }
        }
    }
}
