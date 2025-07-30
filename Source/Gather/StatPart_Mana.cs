using RimWorld;
using Verse;

namespace VVRace
{
    public class StatPart_Mana : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (req.HasThing && req.Thing.TryGetComp<CompMana>(out var compMana))
            {
                if (compMana.Props.manaCapacity > 0)
                {
                    val *= 0.5f + (compMana.StoredPct / 2f);
                }
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (req.HasThing && req.Thing.TryGetComp<CompMana>(out var compMana))
            {
                var multiplier = 0.5f + (compMana.StoredPct / 2f);
                return LocalizeString_Stat.VV_StatReport_Mana_Multiplier.Translate(multiplier.ToStringPercent());
            }
            return null;
        }
    }
}
