using RimWorld;
using Verse;

namespace VVRace
{
    public class CompProperties_StalitflowerGlower : CompProperties_Glower
    {
        public CompProperties_StalitflowerGlower()
        {
            compClass = typeof(CompStalitflowerGlower);
        }
    }

    public class CompStalitflowerGlower : CompManaGlower
    {
        protected override bool ShouldBeLitNow
        {
            get
            {
                if (!base.ShouldBeLitNow) { return false; }

                if (parent.Map.Biome.inVacuum)
                {
                    var vaccum = parent.GetRoom()?.Vacuum ?? 1f;
                    return vaccum >= 0.5f;
                }
                else
                {
                    var dayPct = GenLocalDate.DayPercent(parent.Map);
                    return dayPct < 0.2f || dayPct > 0.7f;
                }
            }
        }
    }
}
