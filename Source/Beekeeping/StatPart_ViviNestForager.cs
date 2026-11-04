using RimWorld;
using Verse;

namespace VVRace
{
    // 둥지 비비는 꽃꿀만으로 무리를 먹여 살리는 숙련된 채집꾼이라 채집량이 늘어난다(둥지를 떠나면 사라진다).
    public class StatPart_ViviNestForager : StatPart
    {
        public float factor = 1f;

        public override void TransformValue(StatRequest req, ref float val)
        {
            if (IsNestForager(req))
            {
                val *= factor;
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            return IsNestForager(req) ? LocalizeString_Stat.VV_StatReport_NestForager.Translate(factor.ToStringByStyle(ToStringStyle.PercentZero, ToStringNumberSense.Factor)) : null;
        }

        private static bool IsNestForager(StatRequest req)
        {
            return req.Thing is Pawn pawn && pawn.GetNestLordJob() != null;
        }
    }
}
