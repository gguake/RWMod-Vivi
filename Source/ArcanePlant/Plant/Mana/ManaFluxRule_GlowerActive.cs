using Verse;

namespace VVRace
{
    public class ManaFluxRule_GlowerActive : ManaFluxRule
    {
        public int mana;

        public override IntRange ApproximateManaFlux => new IntRange(0, mana);

        public override string GetRuleString(bool inverse) =>
            LocalizeString_Stat.VV_StatsReport_ManaFluxRule_GlowerActive_Desc.Translate(
            inverse ? -mana : mana);

        public override int CalcManaFlux(ManaAcceptor plant)
        {
            if (!plant.Spawned || plant.Destroyed) { return 0; }

            var compGlower = plant.TryGetComp<CompGlowerArcanePlant>();
            if (compGlower.Glows)
            {
                return mana;
            }

            return 0;
        }
    }
}
