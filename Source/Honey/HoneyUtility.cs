using RimWorld;
using Verse;

namespace VVRace.Honey
{
    public static class HoneyUtility
    {
        public static bool CanHarvestHoney(this Thing thing)
            => thing is Plant plant && !plant.Blighted && plant.Growth >= 0.7f && plant.Growth < 1.0f;
    }
}
