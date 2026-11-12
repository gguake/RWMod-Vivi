using RimWorld;
using Verse;

namespace VVRace
{
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
