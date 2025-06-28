using RimWorld;
using Verse;

namespace VVRace
{
    public class ManaFluxRule_PlantGrowth : ManaFluxRule
    {
        public IntRange manaFromGrowth;

        public override IntRange FluxRangeForDisplay => new IntRange(manaFromGrowth.min, manaFromGrowth.max);

        public override string GetRuleString() =>
            LocalizeString_Stat.VV_StatsReport_ManaFluxRule_PlantGrowth_Desc.Translate(
                manaFromGrowth.TrueMax.ToString("+0;-#"));

        public override int CalcManaFlux(Thing thing)
        {
            if (!thing.Spawned || thing.Destroyed) { return 0; }

            if (thing is Plant plant && !plant.Blighted)
            {
                return manaFromGrowth.Lerped(plant.Growth);
            }

            return 0;
        }
    }
}
