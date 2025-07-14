using Verse;

namespace VVRace
{
    public class ManaFluxRule_InVaccum : ManaFluxRule
    {
        public int mana;

        public override IntRange FluxRangeForDisplay => new IntRange(0, mana);

        public override string GetRuleString() =>
            LocalizeString_Stat.VV_StatsReport_ManaFluxRule_InVaccum_Desc.Translate(mana.ToString("+0;-#"));

        public override int CalcManaFlux(Thing thing)
        {
            if (!thing.Spawned || thing.Destroyed || !thing.Map.Biome.inVacuum) { return 0; }

            var vaccum = thing.GetRoom()?.Vacuum ?? 1f;
            return vaccum >= 0.5f ? mana : 0;
        }
    }
}
