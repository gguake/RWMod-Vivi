using UnityEngine;
using Verse;

namespace VVRace
{
    public class ManaFluxRule_Fertility : ManaFluxRule
    {
        public SimpleCurve manaFromFertility;

        public override IntRange FluxRangeForDisplay => new IntRange((int)manaFromFertility.MinY, (int)manaFromFertility.MaxY);

        public override string GetRuleString() =>
            LocalizeString_Stat.VV_StatsReport_ManaFluxRule_Fertility_Desc.Translate(((int)manaFromFertility.MaxY).ToString("+0;-#"));

        public override int CalcManaFlux(Thing thing)
        {
            if (!thing.Spawned || thing.Destroyed) { return 0; }

            var fertility = thing.Position.GetFirstThing<ArcanePlantPot>(thing.Map) != null ?
                1f :
                thing.Position.GetFertility(thing.Map);

            return Mathf.RoundToInt(manaFromFertility.Evaluate(fertility));
        }
    }
}
