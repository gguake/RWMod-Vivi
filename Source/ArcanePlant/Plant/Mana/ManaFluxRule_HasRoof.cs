using Verse;

namespace VVRace
{
    public class ManaFluxRule_HasRoof : ManaFluxRule
    {
        public int mana;

        public override IntRange ApproximateManaFlux => new IntRange(0, mana);

        public override string GetRuleString(bool inverse) =>
            LocalizeString_Stat.VV_StatsReport_ManaFluxRule_HasRoof_Desc.Translate(
                mana);

        public override int CalcManaFlux(ManaAcceptor plant)
        {
            if (!plant.Spawned || plant.Destroyed) { return 0; }

            if (plant.Position.Roofed(plant.Map))
            {
                return mana;
            }

            return 0;
        }
    }
}
