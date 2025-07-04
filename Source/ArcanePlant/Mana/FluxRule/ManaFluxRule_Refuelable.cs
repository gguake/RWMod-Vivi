using RimWorld;
using Verse;

namespace VVRace
{
    public class ManaFluxRule_Refuelable : ManaFluxRule
    {
        public int mana;
        public ThingDef fuelDef;

        public override IntRange FluxRangeForDisplay => new IntRange(0, mana);

        public override string GetRuleString() =>
            LocalizeString_Stat.VV_StatsReport_ManaFluxRule_Refuelable_Desc.Translate(mana.ToString("+0;-#"), fuelDef.LabelCap);

        public override int CalcManaFlux(Thing thing)
        {
            if (!thing.Spawned || thing.Destroyed) { return 0; }

            var compRefuelable = thing.TryGetComp<CompRefuelable>();
            if (compRefuelable.HasFuel)
            {
                return mana;
            }

            return 0;
        }
    }
}
