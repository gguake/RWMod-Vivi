using RimWorld;
using Verse;

namespace VVRace.Honey
{
    public static class GatherUtility
    {
        public static bool CanGatherable(this Thing thing, StatDef gatherYieldStat, StatDef gatherableCooldownStat = null)
        {
            if (thing is Plant plant && plant.Growth < thing.GetStatValue(VVStatDefOf.VV_MinGrowthPlantGatherable))
            {
                return false;
            }

            var yieldStat = thing.GetStatValue(gatherYieldStat);
            if (yieldStat <= 0f) { return false; }

            if (gatherableCooldownStat != null)
            {
                var comp = thing.TryGetComp<CompGatherable>();
                if (comp == null || comp.IsCooldown())
                {
                    return false;
                }
            }

            return true;
        }
    }
}
